param([string]$BaseUrl = 'https://localhost:7041')
$ErrorActionPreference = 'Stop'
if ($BaseUrl -ne 'https://localhost:7041') { throw 'Only the isolated demo instance is permitted.' }
$db = New-Object System.Data.SqlClient.SqlConnection('Server=.\SQLEXPRESS;Database=HealthTrack_LocalDemoOnly;Trusted_Connection=True;TrustServerCertificate=True;')
$db.Open()
$script:passed = 0
$demoLabel = 'DEMO PAYMENT ' + [char]0x2013 + ' NO REAL MONEY'
function Assert($condition, $message) { if (!$condition) { throw $message }; $script:passed++; Write-Output "PASS: $message" }
function Query($sql, $order = '') { $c=$db.CreateCommand(); $c.CommandText=$sql; [void]$c.Parameters.AddWithValue('@order',$order); try { return $c.ExecuteScalar() } finally { $c.Dispose() } }
function Page($session,$path) { Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $session }
function Token($page) { $m=[regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"'); if(!$m.Success){throw 'Missing antiforgery token'}; return [Net.WebUtility]::HtmlDecode($m.Groups[1].Value) }
function Post($session,$path,$fields) { $session.Headers.Clear(); Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $session -Method Post -Headers @{Origin=$BaseUrl} -Body $fields }
function Expect($status,$action) { try { $null=& $action; throw "Expected $status" } catch { if(!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $status){throw}; Assert $true "Rejected with HTTP $status" } }
function Login($name) { $s=New-Object Microsoft.PowerShell.Commands.WebRequestSession; $p=Page $s '/Account/Login'; $null=Post $s '/Account/Login' @{Email="$name@healthtrack.test";Password='LocalDemo!2026';__RequestVerificationToken=(Token $p)}; return $s }
try {
 $client=Login 'client'; $other=Login 'client2'; $admin=Login 'admin'
 $package=Query 'SELECT TOP 1 Id FROM MembershipPackages WHERE IsActive=1 ORDER BY Id'
 $anonymous=New-Object Microsoft.PowerShell.Commands.WebRequestSession
 $loginPage=Page $anonymous '/Payments/Demo?orderId=missing'
 Assert ($loginPage.BaseResponse.ResponseUri.AbsolutePath -eq '/Account/Login') 'Anonymous demo access requires authentication'
 Expect 403 { Page $admin '/Payments/Demo?orderId=missing' }
 $checkout=Page $client "/Payments/Checkout?packageId=$package"
 Assert ([Net.WebUtility]::HtmlDecode($checkout.Content).Contains($demoLabel)) 'Checkout is clearly labelled DEMO PAYMENT - NO REAL MONEY'
 foreach($outcome in @('failed','cancelled','success','success')) {
  $page=Post $client '/Payments/Initiate' @{packageId=$package;amount='0.01';__RequestVerificationToken=(Token $checkout)}
  $order=[regex]::Match($page.Content,'DEMO-[a-f0-9]{32}').Value
  Assert ($order.Length -gt 0) 'Checkout stays on the local demo page'
  Assert ([Net.WebUtility]::HtmlDecode($page.Content).Contains($demoLabel)) 'Simulator page clearly states no real money'
  Assert ((Query 'SELECT COUNT(*) FROM PaymentTransactions t JOIN MembershipPackages p ON t.MembershipPackageId=p.Id WHERE t.OrderId=@order AND t.AmountLKR=p.PriceLKR AND t.IsDemo=1' $order) -eq 1) 'Server price overrides tampered input'
  $fields=@{orderId=$order;outcome=$outcome;__RequestVerificationToken=(Token $page)}
  Expect 400 { Post $client '/Payments/CompleteDemo' @{orderId=$order;outcome='success'} }
  $otherPage=Page $other "/Payments/Checkout?packageId=$package"
  Expect 404 { Post $other '/Payments/CompleteDemo' @{orderId=$order;outcome='success';__RequestVerificationToken=(Token $otherPage)} }
  Expect 404 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Payments/CompleteDemo" -WebSession $client -Method Post -Headers @{Origin=$BaseUrl;'X-Forwarded-For'='203.0.113.4'} -Body $fields }
  Expect 404 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Payments/CompleteDemo" -WebSession $client -Method Post -Headers @{Origin='https://example.ngrok-free.app'} -Body $fields }
  Expect 400 { Post $client '/Payments/CompleteDemo' @{orderId=$order;outcome='unknown';__RequestVerificationToken=(Token $page)} }
  $null=Post $client '/Payments/CompleteDemo' $fields
  $expected=@{success=2;failed=3;cancelled=4}[$outcome]
  Assert ((Query 'SELECT Status FROM PaymentTransactions WHERE OrderId=@order' $order) -eq $expected) "Simulated $outcome persisted"
  $count=Query 'SELECT COUNT(*) FROM ClientSubscriptions s JOIN PaymentTransactions t ON s.PaymentTransactionId=t.Id WHERE t.OrderId=@order' $order
  Assert ($count -eq [int]($outcome -eq 'success')) 'Only successful demo payments activate membership'
  $fields.outcome='success'; $null=Post $client '/Payments/CompleteDemo' $fields
  Assert ((Query 'SELECT COUNT(*) FROM ClientSubscriptions s JOIN PaymentTransactions t ON s.PaymentTransactionId=t.Id WHERE t.OrderId=@order' $order) -eq $count) 'Replay cannot activate failed orders or duplicate memberships'
  Assert ((Query 'SELECT COUNT(*) FROM PaymentTransactions WHERE OrderId=@order AND SignatureVerified=0 AND PayHerePaymentId IS NULL' $order) -eq 1) 'Demo never claims PayHere verification'
  $receipt=Page $client "/Payments/Receipt/$order"
  Assert ($receipt.Content -match 'not evidence of real settlement') 'Receipt explicitly disclaims real settlement'
  $history=Page $client '/Payments/History'
  Assert ($history.Content.Contains($order) -and $history.Content -match 'No Real Money') 'Client history includes the labelled demo order'
  $otherHistory=Page $other '/Payments/History'
  Assert (!$otherHistory.Content.Contains($order)) 'Other client history excludes this order'
 }
 Assert ((Query 'SELECT COUNT(*) FROM ClientSubscriptions s JOIN PaymentTransactions t ON s.PaymentTransactionId=t.Id WHERE t.IsDemo=1 AND s.Status=3') -gt 0) 'Renewal preserves subscription history'
 Assert ((Query 'SELECT COALESCE(SUM(AmountLKR),0) FROM PaymentTransactions WHERE Status=2 AND IsDemo=0') -eq 0) 'Demo database has zero real revenue'
 $null=Page $admin '/AdminPayments'
 $null=Page $admin '/Dashboard/Admin'
 $null=Page $client '/Payments/Membership'
 $null=Page $client '/Appointments'
 $null=Page $client '/Measurements'
 $null=Page $client '/Progress'
 Assert $true 'Payment reports, membership, booking and health tracking pages remain accessible'
 Write-Output "Passed $script:passed demo smoke checks. Demo records retained; no database reset or deletion."
} finally { $db.Dispose() }
