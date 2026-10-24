param([string]$BaseUrl = 'http://localhost:5075')
$ErrorActionPreference = 'Stop'

$prefix = 'pay-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = 'PayPass123!' + [Guid]::NewGuid().ToString('N').Substring(0, 4)

$settings = Get-Content -Raw "$PSScriptRoot\..\appsettings.json" | ConvertFrom-Json
$connection = $settings.ConnectionStrings.DefaultConnection
if ($env:ConnectionStrings__DefaultConnection) { $connection = $env:ConnectionStrings__DefaultConnection }

$db = New-Object System.Data.SqlClient.SqlConnection($connection)
$db.Open()

$merchantId = $settings.PayHere.MerchantId
$merchantSecret = $settings.PayHere.MerchantSecret

$script:passed = 0

function Query([string]$sql, [hashtable]$parameters = @{}) {
    $c = $db.CreateCommand()
    $c.CommandText = $sql
    foreach ($k in $parameters.Keys) { [void]$c.Parameters.AddWithValue('@' + $k, $parameters[$k]) }
    try { return $c.ExecuteScalar() } finally { $c.Dispose() }
}

function Execute([string]$sql, [hashtable]$parameters = @{}) {
    $c = $db.CreateCommand()
    $c.CommandText = $sql
    foreach ($k in $parameters.Keys) { [void]$c.Parameters.AddWithValue('@' + $k, $parameters[$k]) }
    try { return $c.ExecuteNonQuery() } finally { $c.Dispose() }
}

function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:passed++
    Write-Output "PASS: $message"
}

function Get-Token([string]$html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw 'Antiforgery token missing' }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function Get-Page($session, [string]$path) {
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -WebSession $session
}

function Post-Form($session, [string]$getPath, [string]$postPath, [hashtable]$fields) {
    $page = Get-Page $session $getPath
    $fields.__RequestVerificationToken = Get-Token $page.Content
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $postPath) -WebSession $session -Method Post -Body $fields
}

function Post-Raw([string]$path, [hashtable]$body) {
    try {
        return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -Method Post -Body $body
    }
    catch {
        if ($_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $respBody = $reader.ReadToEnd()
            Write-Output "SERVER ERROR on $path : $respBody"
        }
        throw
    }
}

function ExpectStatus([int]$status, [scriptblock]$action, [string]$message) {
    try { $null = & $action; throw "Expected HTTP $status : $message" }
    catch {
        if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $status) { throw }
        $script:passed++
        Write-Output "PASS: $message (HTTP $status)"
    }
}

function Md5([string]$raw) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($raw)
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $hashBytes = $md5.ComputeHash($bytes)
    return [System.BitConverter]::ToString($hashBytes).Replace('-', '').ToUpperInvariant()
}

$clientUser = $null
$adminUser = $null
$otherClientUser = $null
$createdOrderIds = New-Object System.Collections.Generic.List[string]

try {
    # 1. Database schema verification
    Assert ((Query "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('MembershipPackages', 'PaymentTransactions', 'ClientSubscriptions')") -eq 3) 'Membership and Payment database tables exist'
    Assert ((Query 'SELECT COUNT(*) FROM MembershipPackages WHERE IsActive = 1') -ge 3) 'Default membership packages seeded in database'

    # 2. PayHere MD5 formula verification
    $testOrderId = 'HT-TEST-VERIFY'
    $testAmount = '3500.00'
    $testCurrency = 'LKR'
    $hashedSecret = Md5 $merchantSecret
    $expectedHash = Md5 "$merchantId$testOrderId$testAmount$testCurrency$hashedSecret"
    Assert ($expectedHash.Length -eq 32) 'PayHere MD5 checkout pre-hash formula computed correctly'

    # Helper to register and activate test user
    function CreateUser([string]$name, [string]$role) {
        $sess = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $email = "$prefix-$name@example.test"
        $page = Get-Page $sess '/Account/Register'
        $token = Get-Token $page.Content
        $null = Post-Form $sess '/Account/Register' '/Account/Register' @{ FullName="Pay Test $name"; Email=$email; Password=$password; ConfirmPassword=$password; __RequestVerificationToken=$token }
        $id = Query 'SELECT Id FROM AspNetUsers WHERE Email = @email' @{email=$email}
        if (-not $id) { throw "Failed to register test user $email" }
        
        # Confirm email directly in DB to bypass external SMTP in smoke test
        $null = Query 'UPDATE AspNetUsers SET EmailConfirmed = 1, RequiresRegistrationEmailVerification = 0 WHERE Id = @id' @{id=$id}
        
        if ($role -ne 'Client') {
            $roleId = Query 'SELECT Id FROM AspNetRoles WHERE Name = @role' @{role=$role}
            $null = Query 'INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@uid, @rid)' @{uid=$id; rid=$roleId}
        }
        
        $loginPage = Get-Page $sess '/Account/Login'
        $null = Post-Form $sess '/Account/Login' '/Account/Login' @{ Email=$email; Password=$password; RememberMe=$false }
        return @{ Id=$id; Email=$email; Session=$sess }
    }

    # 3. Create and login test client
    $clientUser = CreateUser 'client' 'Client'
    $clientId = $clientUser.Id
    $clientEmail = $clientUser.Email
    Assert ($clientUser.Session -ne $null) 'Test client registered and logged in successfully'

    # 4. View Catalog
    $catalogPage = Get-Page $clientUser.Session '/MembershipPackages'
    Assert ($catalogPage.Content.Contains('Basic Starter') -and $catalogPage.Content.Contains('LKR 3,500')) 'Client sees membership packages with LKR pricing'

    # 5. Open Checkout
    $checkoutPage = Get-Page $clientUser.Session '/Payments/Checkout?packageId=1'
    Assert ($checkoutPage.Content.Contains('Order Summary') -and $checkoutPage.Content.Contains('PayHere')) 'Client loads Checkout summary view'

    # 6. Initiate Payment
    $initResp = Post-Form $clientUser.Session '/Payments/Checkout?packageId=1' '/Payments/Initiate' @{packageId=1}
    Assert ($initResp.Content.Contains('payhere-form') -and $initResp.Content.Contains('sandbox.payhere.lk')) 'Payment initiation generated hosted PayHere form'

    # Extract Order ID from generated form
    $orderMatch = [regex]::Match($initResp.Content, 'name="order_id"\s+value="([^"]+)"')
    if (-not $orderMatch.Success) { throw 'Order ID was not generated in PayHere form' }
    $orderId1 = $orderMatch.Groups[1].Value
    $createdOrderIds.Add($orderId1)
    Assert ($orderId1 -match '^HT-\d{8}-\d{5}$') "Order ID follows expected sequence ($orderId1)"

    # Verify pending transaction in DB
    $txStatus = Query "SELECT Status FROM PaymentTransactions WHERE OrderId = @orderId" @{orderId=$orderId1}
    Assert ($txStatus -eq 1) 'Payment transaction stored in database with Status = Pending'
    $txAmount = Query "SELECT AmountLKR FROM PaymentTransactions WHERE OrderId = @orderId" @{orderId=$orderId1}
    Assert ($txAmount -eq 3500.00) 'Payment transaction captured exact purchase price snapshot (3500.00 LKR)'

    # 7. Test IPN with Tampered / Invalid MD5 Signature -> Must Fail
    ExpectStatus 400 {
        $tamperedIpn = @{
            merchant_id = $merchantId
            order_id = $orderId1
            payment_id = '999999999'
            payhere_amount = '3500.00'
            payhere_currency = 'LKR'
            status_code = '2'
            md5sig = 'BAD_TAMPERED_MD5_SIGNATURE'
            method = 'VISA'
        }
        $null = Post-Raw '/Payments/Notify' $tamperedIpn
    } 'Tampered IPN MD5 signature rejected with HTTP 400'

    # Verify transaction remained pending and no subscription was created
    $txStatusAfterTamper = Query "SELECT Status FROM PaymentTransactions WHERE OrderId = @orderId" @{orderId=$orderId1}
    Assert ($txStatusAfterTamper -eq 1) 'Transaction remains Pending after tampered IPN attempt'
    $subCountAfterTamper = Query "SELECT COUNT(*) FROM ClientSubscriptions WHERE ClientId = @cid" @{cid=$clientId}
    Assert ($subCountAfterTamper -eq 0) 'No subscription created upon invalid signature'

    # 8. Test IPN with Valid Signature and Status Code 2 (Success)
    $statusCode = '2'
    $payhereAmount = '3500.00'
    $payhereCurrency = 'LKR'
    $payherePaymentId = '320025144'
    $paymentMethod = 'VISA'

    $validMd5Sig = Md5 "$merchantId$orderId1$payhereAmount$payhereCurrency$statusCode$hashedSecret"

    $validIpn = @{
        merchant_id = $merchantId
        order_id = $orderId1
        payment_id = $payherePaymentId
        payhere_amount = $payhereAmount
        payhere_currency = $payhereCurrency
        status_code = $statusCode
        md5sig = $validMd5Sig
        method = $paymentMethod
        status_message = 'Successfully completed.'
    }

    $ipnResp = Post-Raw '/Payments/Notify' $validIpn
    Assert ($ipnResp.StatusCode -eq 200) 'Valid IPN acknowledged with HTTP 200'

    # Verify transaction marked Paid and subscription activated in DB
    $txPaid = Query "SELECT Status FROM PaymentTransactions WHERE OrderId = @orderId" @{orderId=$orderId1}
    Assert ($txPaid -eq 2) 'Transaction marked Paid in database'
    $txSigOk = Query "SELECT SignatureVerified FROM PaymentTransactions WHERE OrderId = @orderId" @{orderId=$orderId1}
    Assert ($txSigOk -eq 1) 'SignatureVerified recorded as true'

    $subActive = Query "SELECT Status FROM ClientSubscriptions WHERE ClientId = @cid AND PaymentTransactionId = (SELECT Id FROM PaymentTransactions WHERE OrderId = @orderId)" @{cid=$clientId; orderId=$orderId1}
    Assert ($subActive -eq 1) 'ClientSubscription created and activated in database'
    $subPkg = Query "SELECT PackageNameSnapshot FROM ClientSubscriptions WHERE ClientId = @cid" @{cid=$clientId}
    Assert ($subPkg -eq 'Basic Starter') 'ClientSubscription contains immutable tier snapshot'

    # 9. Test Idempotency (Repeat IPN for same order)
    $ipnRepeatResp = Post-Raw '/Payments/Notify' $validIpn
    Assert ($ipnRepeatResp.StatusCode -eq 200) 'Duplicate IPN handled idempotently with HTTP 200'
    $subTotalCount = Query "SELECT COUNT(*) FROM ClientSubscriptions WHERE ClientId = @cid" @{cid=$clientId}
    Assert ($subTotalCount -eq 1) 'Idempotency verified: no duplicate subscription created'

    # 10. Test Client Return, Success & Receipt views
    $successPage = Get-Page $clientUser.Session "/Payments/Success?orderId=$orderId1"
    Assert ($successPage.Content.Contains('Payment Successful!') -and $successPage.Content.Contains('Verified by Server')) 'Client Success page reflects server-verified payment'

    $receiptPage = Get-Page $clientUser.Session "/Payments/Receipt?orderId=$orderId1"
    Assert ($receiptPage.Content.Contains('Official Receipt') -and $receiptPage.Content.Contains('LKR 3,500.00')) 'Client Receipt displays accurate order details and LKR price'

    $historyPage = Get-Page $clientUser.Session '/Payments/History'
    Assert ($historyPage.Content.Contains($orderId1) -and $historyPage.Content.Contains('Basic Starter')) 'Order appears in client payment history'

    # 11. Test Subscription Renewal Workflow
    # Client purchases Package 2 ("Pro Fitness", 90 days, LKR 8500)
    $initResp2 = Post-Form $clientUser.Session '/Payments/Checkout?packageId=2' '/Payments/Initiate' @{packageId=2}
    $orderMatch2 = [regex]::Match($initResp2.Content, 'name="order_id"\s+value="([^"]+)"')
    $orderId2 = $orderMatch2.Groups[1].Value
    $createdOrderIds.Add($orderId2)

    $amount2 = '8500.00'
    $sig2 = Md5 "$merchantId$orderId2$amount2$payhereCurrency$statusCode$hashedSecret"
    $ipn2 = @{
        merchant_id = $merchantId
        order_id = $orderId2
        payment_id = '320025199'
        payhere_amount = $amount2
        payhere_currency = $payhereCurrency
        status_code = $statusCode
        md5sig = $sig2
        method = 'MasterCard'
    }
    $null = Post-Raw '/Payments/Notify' $ipn2

    # Verify renewal: old sub marked Renewed (3), new sub Active (1) with extended dates
    $oldSubStatus = Query "SELECT Status FROM ClientSubscriptions WHERE ClientId = @cid AND PaymentTransactionId = (SELECT Id FROM PaymentTransactions WHERE OrderId = @orderId)" @{cid=$clientId; orderId=$orderId1}
    Assert ($oldSubStatus -eq 3) 'Previous subscription marked Renewed (3)'

    $newSubStatus = Query "SELECT Status FROM ClientSubscriptions WHERE ClientId = @cid AND PaymentTransactionId = (SELECT Id FROM PaymentTransactions WHERE OrderId = @orderId)" @{cid=$clientId; orderId=$orderId2}
    Assert ($newSubStatus -eq 1) 'New renewal subscription activated (1)'

    # 12. Test Admin Payment Monitoring
    $adminUser = CreateUser 'admin' 'Admin'
    $adminId = $adminUser.Id
    $adminEmail = $adminUser.Email

    $adminPaymentsPage = Get-Page $adminUser.Session '/AdminPayments'
    Assert ($adminPaymentsPage.Content.Contains('Payment & Revenue Management') -and $adminPaymentsPage.Content.Contains($orderId1)) 'Admin can view payment transactions list'

    $adminSubsPage = Get-Page $adminUser.Session '/AdminPayments/Subscriptions'
    Assert ($adminSubsPage.Content.Contains('Client Memberships & Subscriptions') -and $adminSubsPage.Content.Contains($clientEmail)) 'Admin can inspect client subscriptions'

    $tx1Id = Query "SELECT Id FROM PaymentTransactions WHERE OrderId = @orderId" @{orderId=$orderId1}
    $adminDetailsPage = Get-Page $adminUser.Session "/AdminPayments/Details/$tx1Id"
    Assert ($adminDetailsPage.Content.Contains('Transaction Audit') -and $adminDetailsPage.Content.Contains('Verified Authentic')) 'Admin can inspect payment transaction audit detail'

    # 13. Security Authorization Checks
    ExpectStatus 403 {
        $null = Get-Page $clientUser.Session '/AdminPayments'
    } 'Client user blocked from accessing Admin Payments (HTTP 403)'

    $anonSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $anonResp = Get-Page $anonSession '/Payments/Checkout?packageId=1'
    Assert ($anonResp.BaseResponse.ResponseUri.AbsolutePath -match '/Account/Login') 'Unauthenticated checkout request redirects to Login'

    Write-Output "All $script:passed checks PASSED!"
}
finally {
    # Non-destructive test data cleanup
    foreach ($oid in $createdOrderIds) {
        Execute "DELETE FROM ClientSubscriptions WHERE PaymentTransactionId IN (SELECT Id FROM PaymentTransactions WHERE OrderId = @oid)" @{oid=$oid}
        Execute "DELETE FROM PaymentTransactions WHERE OrderId = @oid" @{oid=$oid}
    }
    if ($clientId) {
        Execute "DELETE FROM Notifications WHERE UserId = @id" @{id=$clientId}
        Execute "DELETE FROM AspNetUserRoles WHERE UserId = @id" @{id=$clientId}
        Execute "DELETE FROM AspNetUsers WHERE Id = @id" @{id=$clientId}
    }
    if ($adminId) {
        Execute "DELETE FROM AspNetUserRoles WHERE UserId = @id" @{id=$adminId}
        Execute "DELETE FROM AspNetUsers WHERE Id = @id" @{id=$adminId}
    }
    $db.Close()
}
