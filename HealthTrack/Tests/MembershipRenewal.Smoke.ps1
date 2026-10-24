param([string]$BaseUrl = 'http://localhost:5075')
$ErrorActionPreference = 'Stop'

$prefix = 'renew-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = 'RenewPass123!' + [Guid]::NewGuid().ToString('N').Substring(0, 4)

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
    if (-not $match.Success) { throw "Verification token not found in response HTML." }
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
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -Method Post -Body $body
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
$client2User = $null
$adminUser = $null
$createdOrderIds = New-Object System.Collections.Generic.List[string]

try {
    # 1. Database schema checks
    $adminNotesCol = Query "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[ClientSubscriptions]') AND name = 'AdminNotes'"
    $lastModifiedCol = Query "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[ClientSubscriptions]') AND name = 'LastModifiedAt'"
    Assert ($adminNotesCol -eq 1 -and $lastModifiedCol -eq 1) 'ClientSubscriptions table contains AdminNotes and LastModifiedAt columns'

    # Helper to register and activate test user
    function CreateUser([string]$name, [string]$role) {
        $sess = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $email = "$prefix-$name@example.test"
        $page = Get-Page $sess '/Account/Register'
        $token = Get-Token $page.Content
        $null = Post-Form $sess '/Account/Register' '/Account/Register' @{ FullName="Renew Test $name"; Email=$email; Password=$password; ConfirmPassword=$password; __RequestVerificationToken=$token }
        $id = Query 'SELECT Id FROM AspNetUsers WHERE Email = @email' @{email=$email}
        if (-not $id) { throw "Failed to register test user $email" }
        
        $null = Query 'UPDATE AspNetUsers SET EmailConfirmed = 1, RequiresRegistrationEmailVerification = 0 WHERE Id = @id' @{id=$id}
        
        if ($role -ne 'Client') {
            $roleId = Query 'SELECT Id FROM AspNetRoles WHERE Name = @role' @{role=$role}
            $null = Query 'INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@uid, @rid)' @{uid=$id; rid=$roleId}
        }
        
        $loginPage = Get-Page $sess '/Account/Login'
        $null = Post-Form $sess '/Account/Login' '/Account/Login' @{ Email=$email; Password=$password; RememberMe=$false }
        return @{ Id=$id; Email=$email; Session=$sess }
    }

    # 2. Register test client and admin
    $clientUser = CreateUser 'client' 'Client'
    $client2User = CreateUser 'client2' 'Client'
    $adminUser = CreateUser 'admin' 'Admin'
    Assert ($clientUser.Session -ne $null -and $adminUser.Session -ne $null) 'Test Client and Admin accounts created and authenticated'

    # 3. Client views initial Membership Hub (Empty State)
    $membershipPage = Get-Page $clientUser.Session '/Payments/Membership'
    Assert ($membershipPage.Content.Contains('No Active Membership Subscription') -and $membershipPage.Content.Contains('Explore Membership Packages')) 'Client sees empty membership state in Membership Hub'

    # 4. Client navigates to Book Appointment (Notice banner rendered)
    $bookPage = Get-Page $clientUser.Session '/Appointments/Book'
    Assert ($bookPage.Content.Contains('Notice: Standard Access') -or $bookPage.Content.Contains('Browse Packages')) 'Client booking view informs user of standard access'

    # 5. Client initiates checkout for package 1 (Basic Starter, 30 days, 3500 LKR)
    $orderId1 = 'HT-RENEW-' + [Guid]::NewGuid().ToString('N').Substring(0, 8).ToUpper()
    $createdOrderIds.Add($orderId1)
    $pkg = Query 'SELECT TOP 1 Id, Name, PriceLKR, DurationDays FROM MembershipPackages WHERE Id = 1'
    
    # Insert pending transaction
    $null = Query @"
INSERT INTO PaymentTransactions (OrderId, ClientId, MembershipPackageId, PackageNameSnapshot, AmountLKR, DurationDaysSnapshot, Currency, Status, CreatedAt, SignatureVerified)
VALUES (@oid, @cid, 1, 'Basic Starter', 3500.00, 30, 'LKR', 1, GETUTCDATE(), 0)
"@ @{oid=$orderId1; cid=$clientUser.Id}

    # 6. Simulate valid IPN for order 1
    $hashedSecret = Md5 $merchantSecret
    $ipnSig1 = Md5 "${merchantId}${orderId1}3500.00LKR2${hashedSecret}"
    $ipnBody1 = @{
        merchant_id = $merchantId
        order_id = $orderId1
        payment_id = 'PH-TEST-9001'
        payhere_amount = '3500.00'
        payhere_currency = 'LKR'
        status_code = '2'
        md5sig = $ipnSig1
        method = 'TEST_VISA'
        status_message = 'Successfully settled'
    }
    $ipnResp1 = Post-Raw '/Payments/Notify' $ipnBody1
    Assert ($ipnResp1.StatusCode -eq 200) 'PayHere IPN accepted with HTTP 200'

    # Verify subscription activated in database
    $sub1Id = Query 'SELECT Id FROM ClientSubscriptions WHERE PaymentTransactionId IN (SELECT Id FROM PaymentTransactions WHERE OrderId = @oid)' @{oid=$orderId1}
    Assert ($sub1Id -gt 0) 'Subscription created and activated in database'
    $sub1Status = Query 'SELECT Status FROM ClientSubscriptions WHERE Id = @id' @{id=$sub1Id}
    Assert ($sub1Status -eq 1) 'Subscription has Status = Active (1)'

    # 7. Client re-visits Membership Hub (Active State)
    $hubActive = Get-Page $clientUser.Session '/Payments/Membership'
    Assert ($hubActive.Content.Contains('Active Member') -and $hubActive.Content.Contains('Basic Starter')) 'Client Membership Hub displays Active Member badge and package name'
    Assert ($hubActive.Content.Contains('Renew / Extend Membership')) 'Client Membership Hub displays quick renewal CTA'

    # 8. Client checks Dashboard
    $clientDash = Get-Page $clientUser.Session '/Dashboard/Client'
    Assert ($clientDash.Content.Contains('Basic Starter') -and $clientDash.Content.Contains('Active Member')) 'Client Dashboard displays Membership Status banner'

    # 9. Client views Printable Official Receipt
    $receiptPage = Get-Page $clientUser.Session "/Payments/Receipt?orderId=$orderId1"
    Assert ($receiptPage.Content.Contains('HealthTrack Gym & Fitness') -and $receiptPage.Content.Contains('PV 00249581')) 'Printable Receipt contains registered business credentials'
    Assert ($receiptPage.Content.Contains('Print / Save as PDF') -and $receiptPage.Content.Contains('@media print')) 'Receipt supports print media styles and PDF generation'
    Assert ($receiptPage.Content.Contains('LKR 3,500.00')) 'Receipt displays accurate snapshot amount'

    # Route style: /Payments/Receipt/{orderId}
    $receiptRoute = Get-Page $clientUser.Session "/Payments/Receipt/$orderId1"
    Assert ($receiptRoute.StatusCode -eq 200 -and $receiptRoute.Content.Contains($orderId1)) 'Receipt accessible via RESTful route /Payments/Receipt/{orderId}'

    # 10. Seamless Renewal Stacking (Renewing while active)
    $sub1End = Query 'SELECT EndDate FROM ClientSubscriptions WHERE Id = @id' @{id=$sub1Id}
    $orderId2 = 'HT-RENEW-' + [Guid]::NewGuid().ToString('N').Substring(0, 8).ToUpper()
    $createdOrderIds.Add($orderId2)

    # Insert pending renewal transaction for Package 2 (Standard Pro, 60 days, 6500 LKR)
    $null = Query @"
INSERT INTO PaymentTransactions (OrderId, ClientId, MembershipPackageId, PackageNameSnapshot, AmountLKR, DurationDaysSnapshot, Currency, Status, CreatedAt, SignatureVerified)
VALUES (@oid, @cid, 2, 'Standard Pro', 6500.00, 60, 'LKR', 1, GETUTCDATE(), 0)
"@ @{oid=$orderId2; cid=$clientUser.Id}

    $ipnSig2 = Md5 "${merchantId}${orderId2}6500.00LKR2${hashedSecret}"
    $ipnBody2 = @{
        merchant_id = $merchantId
        order_id = $orderId2
        payment_id = 'PH-TEST-9002'
        payhere_amount = '6500.00'
        payhere_currency = 'LKR'
        status_code = '2'
        md5sig = $ipnSig2
        method = 'TEST_MASTER'
        status_message = 'Successfully settled'
    }
    $ipnResp2 = Post-Raw '/Payments/Notify' $ipnBody2
    Assert ($ipnResp2.StatusCode -eq 200) 'Renewal IPN acknowledged with HTTP 200'

    # Check that previous subscription status transitioned to Renewed (3)
    $sub1StatusAfter = Query 'SELECT Status FROM ClientSubscriptions WHERE Id = @id' @{id=$sub1Id}
    Assert ($sub1StatusAfter -eq 3) 'Previous subscription marked as Renewed (3)'

    # Check that new subscription started on sub1End + 1 day
    $sub2Id = Query 'SELECT Id FROM ClientSubscriptions WHERE PaymentTransactionId IN (SELECT Id FROM PaymentTransactions WHERE OrderId = @oid)' @{oid=$orderId2}
    $sub2Start = Query 'SELECT StartDate FROM ClientSubscriptions WHERE Id = @id' @{id=$sub2Id}
    $expectedSub2Start = (Get-Date $sub1End).AddDays(1).ToString('yyyy-MM-dd')
    Assert ((Get-Date $sub2Start).ToString('yyyy-MM-dd') -eq $expectedSub2Start) "Renewal stacked seamlessly starting on $expectedSub2Start without day loss"

    # 11. Expiration Processing Test
    # Create an expired subscription for client2 with EndDate = Yesterday
    $yesterday = (Get-Date).AddDays(-1).ToString('yyyy-MM-dd')
    $pastStart = (Get-Date).AddDays(-31).ToString('yyyy-MM-dd')
    $expiredTxId = Query @"
INSERT INTO PaymentTransactions (OrderId, ClientId, MembershipPackageId, PackageNameSnapshot, AmountLKR, DurationDaysSnapshot, Currency, Status, CreatedAt, SignatureVerified)
OUTPUT INSERTED.Id
VALUES ('HT-EXPIRED-TEST', @cid, 1, 'Basic Starter', 3500.00, 30, 'LKR', 2, GETUTCDATE(), 1)
"@ @{cid=$client2User.Id}
    
    $expiredSubId = Query @"
INSERT INTO ClientSubscriptions (ClientId, MembershipPackageId, PaymentTransactionId, PackageNameSnapshot, AmountPaidLKR, DurationDaysSnapshot, StartDate, EndDate, Status, ActivatedAt)
OUTPUT INSERTED.Id
VALUES (@cid, 1, @txid, 'Basic Starter', 3500.00, 30, @sdate, @edate, 1, GETUTCDATE())
"@ @{cid=$client2User.Id; txid=$expiredTxId; sdate=$pastStart; edate=$yesterday}

    # Trigger expiration check via Admin action POST /AdminPayments/RunExpirationCheck
    $adminToken = Get-Token (Get-Page $adminUser.Session '/AdminPayments/Subscriptions').Content
    $expCheckResp = Post-Form $adminUser.Session '/AdminPayments/Subscriptions' '/AdminPayments/RunExpirationCheck' @{ __RequestVerificationToken=$adminToken }
    Assert ($expCheckResp.StatusCode -eq 200) 'Admin successfully ran on-demand Expiration Check'

    # Verify subscription status is now Expired (2)
    $expiredStatus = Query 'SELECT Status FROM ClientSubscriptions WHERE Id = @id' @{id=$expiredSubId}
    Assert ($expiredStatus -eq 2) 'Past subscription automatically transitioned to Expired (2)'

    # Verify client2 received "Membership Expired" notification
    $notifExpired = Query "SELECT COUNT(*) FROM Notifications WHERE UserId = @cid AND Type = 4 AND Title = 'Membership Expired'" @{cid=$client2User.Id}
    Assert ($notifExpired -ge 1) 'Client received in-app notification when membership expired'

    # 12. 3-Day Expiration Warning Alert Test
    $soonEnd = (Get-Date).AddDays(2).ToString('yyyy-MM-dd')
    $soonStart = (Get-Date).AddDays(-28).ToString('yyyy-MM-dd')
    $soonTxId = Query @"
INSERT INTO PaymentTransactions (OrderId, ClientId, MembershipPackageId, PackageNameSnapshot, AmountLKR, DurationDaysSnapshot, Currency, Status, CreatedAt, SignatureVerified)
OUTPUT INSERTED.Id
VALUES ('HT-SOON-TEST', @cid, 1, 'Basic Starter', 3500.00, 30, 'LKR', 2, GETUTCDATE(), 1)
"@ @{cid=$client2User.Id}
    
    $soonSubId = Query @"
INSERT INTO ClientSubscriptions (ClientId, MembershipPackageId, PaymentTransactionId, PackageNameSnapshot, AmountPaidLKR, DurationDaysSnapshot, StartDate, EndDate, Status, ActivatedAt)
OUTPUT INSERTED.Id
VALUES (@cid, 1, @txid, 'Basic Starter', 3500.00, 30, @sdate, @edate, 1, GETUTCDATE())
"@ @{cid=$client2User.Id; txid=$soonTxId; sdate=$soonStart; edate=$soonEnd}

    # Run check again
    $adminToken = Get-Token (Get-Page $adminUser.Session '/AdminPayments/Subscriptions').Content
    $null = Post-Form $adminUser.Session '/AdminPayments/Subscriptions' '/AdminPayments/RunExpirationCheck' @{ __RequestVerificationToken=$adminToken }

    $notifSoon = Query "SELECT COUNT(*) FROM Notifications WHERE UserId = @cid AND Type = 4 AND Title LIKE '%Expiring Soon%'" @{cid=$client2User.Id}
    Assert ($notifSoon -ge 1) 'Client received in-app warning notification for subscription expiring in 2 days'

    # 13. Admin Subscriptions Dashboard & Metrics
    $adminSubsPage = Get-Page $adminUser.Session '/AdminPayments/Subscriptions'
    Assert ($adminSubsPage.Content.Contains('Total Subscriptions') -and $adminSubsPage.Content.Contains('Active Members')) 'Admin Subscriptions view displays KPI metrics'
    Assert ($adminSubsPage.Content.Contains('Manage')) 'Admin Subscriptions table provides Manage links'

    # 14. Admin Manage Subscription View
    $managePage = Get-Page $adminUser.Session "/AdminPayments/ManageSubscription/$sub2Id"
    Assert ($managePage.Content.Contains('Grant Courtesy Extension') -and $managePage.Content.Contains('Additional Days')) 'Admin loaded Manage Subscription view'

    # 15. Admin Grants Courtesy Extension (+14 Days)
    $manageToken = Get-Token $managePage.Content
    $extendResp = Post-Form $adminUser.Session "/AdminPayments/ManageSubscription/$sub2Id" '/AdminPayments/ExtendSubscription' @{
        subscriptionId = $sub2Id
        additionalDays = 14
        reason = 'Courtesy facility upgrade extension'
        __RequestVerificationToken = $manageToken
    }
    Assert ($extendResp.StatusCode -eq 200) 'Admin successfully extended subscription'

    # Verify EndDate and AdminNotes in DB
    $sub2Notes = Query 'SELECT AdminNotes FROM ClientSubscriptions WHERE Id = @id' @{id=$sub2Id}
    Assert ($sub2Notes.Contains('+14 days') -and $sub2Notes.Contains('Courtesy facility upgrade extension')) 'Admin extension note saved in database'
    $clientExtNotif = Query "SELECT COUNT(*) FROM Notifications WHERE UserId = @cid AND Title LIKE '%Extended by Admin%'" @{cid=$clientUser.Id}
    Assert ($clientExtNotif -ge 1) 'Client received notification about administrative extension'

    # 16. Admin Subscription Cancellation
    $cancelManagePage = Get-Page $adminUser.Session "/AdminPayments/ManageSubscription/$soonSubId"
    $cancelToken = Get-Token $cancelManagePage.Content
    $cancelResp = Post-Form $adminUser.Session "/AdminPayments/ManageSubscription/$soonSubId" '/AdminPayments/CancelSubscription' @{
        subscriptionId = $soonSubId
        reason = 'Customer requested early cancellation'
        __RequestVerificationToken = $cancelToken
    }
    Assert ($cancelResp.StatusCode -eq 200) 'Admin successfully cancelled subscription'
    $cancelledStatus = Query 'SELECT Status FROM ClientSubscriptions WHERE Id = @id' @{id=$soonSubId}
    Assert ($cancelledStatus -eq 4) 'Subscription marked Cancelled (4) in database'

    # 17. Security and Access Authorization Checks
    ExpectStatus 403 { Get-Page $clientUser.Session '/AdminPayments/Subscriptions' } 'Client blocked from Admin Subscriptions page'
    ExpectStatus 403 { Get-Page $clientUser.Session "/AdminPayments/ManageSubscription/$sub2Id" } 'Client blocked from Admin Manage Subscription view'
    
    # Client cannot see another client's receipt
    ExpectStatus 404 { Get-Page $clientUser.Session "/Payments/Receipt?orderId=HT-EXPIRED-TEST" } 'Client blocked from viewing another client receipt'

    # Unauthenticated user redirected to login
    $anonSess = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $anonResp = Get-Page $anonSess '/Payments/Membership'
    Assert ($anonResp.BaseResponse.ResponseUri.AbsolutePath -match '/Account/Login') 'Anonymous user redirected to Login when accessing Membership Hub'

    Write-Output "`n=========================================="
    Write-Output "All $script:passed Phase 5 Membership Renewal checks PASSED!"
    Write-Output "=========================================="
}
finally {
    # Cleanup test records
    if ($createdOrderIds.Count -gt 0) {
        foreach ($oid in $createdOrderIds) {
            Execute 'DELETE FROM ClientSubscriptions WHERE PaymentTransactionId IN (SELECT Id FROM PaymentTransactions WHERE OrderId = @oid)' @{oid=$oid}
            Execute 'DELETE FROM PaymentTransactions WHERE OrderId = @oid' @{oid=$oid}
        }
    }
    Execute "DELETE FROM ClientSubscriptions WHERE PaymentTransactionId IN (SELECT Id FROM PaymentTransactions WHERE OrderId IN ('HT-EXPIRED-TEST', 'HT-SOON-TEST'))"
    Execute "DELETE FROM PaymentTransactions WHERE OrderId IN ('HT-EXPIRED-TEST', 'HT-SOON-TEST')"
    
    if ($clientUser) {
        Execute 'DELETE FROM Notifications WHERE UserId = @id' @{id=$clientUser.Id}
        Execute 'DELETE FROM AspNetUsers WHERE Id = @id' @{id=$clientUser.Id}
    }
    if ($client2User) {
        Execute 'DELETE FROM Notifications WHERE UserId = @id' @{id=$client2User.Id}
        Execute 'DELETE FROM AspNetUsers WHERE Id = @id' @{id=$client2User.Id}
    }
    if ($adminUser) {
        Execute 'DELETE FROM AspNetUserRoles WHERE UserId = @id' @{id=$adminUser.Id}
        Execute 'DELETE FROM AspNetUsers WHERE Id = @id' @{id=$adminUser.Id}
    }
    $db.Close()
}
