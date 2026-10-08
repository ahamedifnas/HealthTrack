param([string]$BaseUrl='http://localhost:5092')
# Registration-only integration checks. Requires AuthSmtp.Fixture.cjs and the app
# using that fixture. Only uniquely named test accounts are changed/deleted.
$ErrorActionPreference='Stop'
$prefix='otp-'+[Guid]::NewGuid().ToString('N')
$password='Ht9!'+[Guid]::NewGuid().ToString('N')
$settings=Get-Content -Raw "$PSScriptRoot/../appsettings.json" | ConvertFrom-Json
$connection=$settings.ConnectionStrings.DefaultConnection
if($env:ConnectionStrings__DefaultConnection) { $connection=$env:ConnectionStrings__DefaultConnection }
$db=New-Object System.Data.SqlClient.SqlConnection($connection); $db.Open()
$script:passed=0
function Query($sql,$parameters=@{}) {
    $c=$db.CreateCommand(); $c.CommandText=$sql
    foreach($key in $parameters.Keys) { [void]$c.Parameters.AddWithValue('@'+$key,$parameters[$key]) }
    try { $c.ExecuteScalar() } finally { $c.Dispose() }
}
function Assert($ok,$message) { if(-not $ok) { throw $message }; $script:passed++; Write-Output "PASS: $message" }
function Session { New-Object Microsoft.PowerShell.Commands.WebRequestSession }
function Get($s,$path) { Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $s }
function Post($s,$path,$fields,$getPath=$path) {
    $page=Get $s $getPath
    $fields.__RequestVerificationToken=[System.Net.WebUtility]::HtmlDecode([regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value)
    Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $s -Method Post -Body $fields
}
function Messages { @(Invoke-RestMethod http://127.0.0.1:2527/) }
function LatestCode {
    $mail=(Messages)[-1] -replace '=\r\n',''
    $mail=[regex]::Replace($mail,'=([0-9A-Fa-f]{2})',{param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value,16)})
    $code=[regex]::Match($mail,'>([0-9]{6})</p>').Groups[1].Value
    if($code.Length -ne 6) { throw 'No six-digit code found in test SMTP message' }
    return $code
}
function Register($name) {
    $s=Session; $email="$prefix-$name@example.test"
    $page=Post $s '/Account/Register' @{FullName='OTP test';Email=$email;Password=$password;ConfirmPassword=$password}
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Account/VerifyEmailOtp') 'Registration redirects to verification without a query string'
    $id=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}
    if(-not $id) { throw 'Test account missing' }
    return @{Session=$s;Email=$email;Id=$id;Page=$page}
}
function Verify($account,$code) { Post $account.Session '/Account/VerifyEmailOtp' @{Code=$code} }
function Resend($account) { Post $account.Session '/Account/ResendEmailOtp' @{} '/Account/VerifyEmailOtp' }
function AdvanceCooldown($account) { $null=Query 'UPDATE EmailVerificationOtps SET LastSentAt=DATEADD(second,-61,SYSDATETIMEOFFSET()) WHERE UserId=@id' @{id=$account.Id} }
try {
    $null=Invoke-RestMethod http://127.0.0.1:2527/resume
    $s=Session; $count=(Messages).Count
    $page=Post $s '/Account/Register' @{FullName='OTP test';Email='invalid';Password=$password;ConfirmPassword=$password}
    Assert ($page.Content.Contains('not a valid e-mail address') -and (Messages).Count -eq $count) 'Invalid registration does not send OTP'
    $a=Register 'success'; $code=LatestCode
    Assert (-not $a.Page.Content.Contains($code)) 'OTP is not rendered in browser source'
    Assert ($a.Page.Content.Contains('autocomplete="one-time-code"') -and $a.Page.Content.Contains('maxlength="6"')) 'OTP field supports autofill and six digits'
    Assert ((Query 'SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=@id AND r.Name=@role' @{id=$a.Id;role='Client'}) -eq 1) 'Public registration retains Client role'
    Assert ((Query 'SELECT COUNT(*) FROM AspNetUsers WHERE Id=@id AND EmailConfirmed=0 AND RequiresRegistrationEmailVerification=1' @{id=$a.Id}) -eq 1) 'New registration is explicitly pending verification'
    $hash=Query 'SELECT OtpHash FROM EmailVerificationOtps WHERE UserId=@id' @{id=$a.Id}
    Assert ($hash.Length -gt 50 -and $hash -ne $code) 'Only a salted password-style OTP hash is stored'
    Assert ((Query 'SELECT DATEDIFF(second,CreatedAt,ExpiresAt) FROM EmailVerificationOtps WHERE UserId=@id' @{id=$a.Id}) -eq 300) 'OTP expires exactly five minutes after creation'
    Assert ((Get $a.Session '/Dashboard').Content.Contains('name="Password"')) 'Registration does not authenticate the new user'
    $pending=$a.Session.Cookies.GetCookies([uri]$BaseUrl) | Where-Object Name -eq 'HealthTrack.Registration'
    Assert ($pending.HttpOnly) 'Pending registration cookie is HttpOnly'
    $login=Session
    $page=Post $login '/Account/Login' @{Email=$a.Email;Password='wrong'}
    Assert (-not $page.Content.Contains('Please verify your email')) 'Wrong password does not reveal pending verification'
    $page=Post $login '/Account/Login' @{Email=$a.Email;Password=$password}
    Assert ($page.Content.Contains('Please verify your email address before signing in.')) 'Pending registration cannot sign in and gets verification guidance'
    Assert ((Get $login '/Account/VerifyEmailOtp').Content.Contains('Verification Code')) 'Correct password recovers verification session on another browser'
    $count=(Messages).Count; $page=Resend $a
    Assert ($page.Content.Contains('Please wait') -and (Messages).Count -eq $count) 'Immediate resend is rejected without another email'
    foreach($route in @('VerifyEmailOtp','ResendEmailOtp')) {
        try { $null=Invoke-WebRequest -UseBasicParsing "$BaseUrl/Account/$route" -Method Post -WebSession $a.Session; throw 'CSRF accepted' }
        catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 400) "$route requires antiforgery token" }
    }
    $wrong=if($code -eq '000000') {'111111'} else {'000000'}
    $page=Verify $a $wrong
    Assert ($page.Content.Contains('That code is incorrect')) 'Incorrect OTP produces safe validation message'
    Assert (-not $page.Content.Contains('value="'+$wrong+'"')) 'Submitted incorrect OTP is never echoed into input value'
    Assert ((Query 'SELECT AttemptCount FROM EmailVerificationOtps WHERE UserId=@id' @{id=$a.Id}) -eq 1) 'Incorrect attempt is stored server-side'
    $page=Verify $a $code
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Account/Login' -and $page.Content.Contains('Your email has been verified')) 'Correct OTP completes registration and returns to Login'
    Assert ((Query 'SELECT COUNT(*) FROM AspNetUsers WHERE Id=@id AND EmailConfirmed=1 AND RequiresRegistrationEmailVerification=0' @{id=$a.Id}) -eq 1) 'Successful verification confirms email and clears pending flag'
    Assert ((Query 'SELECT COUNT(*) FROM EmailVerificationOtps WHERE UserId=@id AND IsUsed=1 AND OtpHash=@empty' @{id=$a.Id;empty=''}) -eq 1) 'Consumed OTP is marked used and its hash cleared'
    Assert ((Get $a.Session '/Dashboard').Content.Contains('name="Password"')) 'Verification does not automatically sign in'
    $page=Verify @{Session=$login} $code
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Account/Login') 'Previously issued session cannot reuse a consumed OTP'
    $page=Post $a.Session '/Account/Login' @{Email=$a.Email;Password=$password;RememberMe='true'}
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Dashboard/Client') 'Verified Client can login normally'

    $b=Register 'expiry'; $old=LatestCode
    $null=Query 'UPDATE EmailVerificationOtps SET ExpiresAt=DATEADD(second,-1,SYSDATETIMEOFFSET()) WHERE UserId=@id' @{id=$b.Id}
    Assert ((Verify $b $old).Content.Contains('has expired')) 'Server rejects expired OTP (test record expiry advanced)'
    AdvanceCooldown $b; $page=Resend $b; $new=LatestCode
    Assert ($new -ne $old -and $page.Content.Contains('A new verification code has been sent')) 'Resend after cooldown sends a different code'
    Assert ((Verify $b $old).Content.Contains('That code is incorrect')) 'Previous OTP is invalid after resend'
    Assert ((Verify $b $new).BaseResponse.ResponseUri.AbsolutePath -eq '/Account/Login') 'Latest resent code completes verification'

    $c=Register 'attempts'; $code=LatestCode; $wrong=if($code -eq '000000') {'111111'} else {'000000'}
    1..5 | ForEach-Object { $null=Verify $c $wrong }
    Assert ((Verify $c $code).Content.Contains('Too many incorrect attempts')) 'Correct code is rejected after five wrong attempts'
    Assert ((Query 'SELECT AttemptCount FROM EmailVerificationOtps WHERE UserId=@id' @{id=$c.Id}) -eq 5) 'Attempt count is capped at five'
    AdvanceCooldown $c; $null=Resend $c
    Assert ((Query 'SELECT AttemptCount FROM EmailVerificationOtps WHERE UserId=@id' @{id=$c.Id}) -eq 0) 'Resend starts a new limited attempt window'
    Assert ((Verify $c (LatestCode)).BaseResponse.ResponseUri.AbsolutePath -eq '/Account/Login') 'Resend recovers an attempt-limited registration'

    $null=Invoke-RestMethod http://127.0.0.1:2527/fail
    $d=Register 'delivery'
    Assert ($d.Page.Content.Contains("couldn't send") -and -not $d.Page.Content.Contains('We sent a 6-digit')) 'SMTP failure is honest and recoverable without exposing internals'
    Assert ((Query 'SELECT COUNT(*) FROM EmailVerificationOtps WHERE UserId=@id AND IsUsed=1 AND OtpHash=@empty' @{id=$d.Id;empty=''}) -eq 1) 'Failed delivery invalidates the undelivered OTP'
    $null=Invoke-RestMethod http://127.0.0.1:2527/resume
    AdvanceCooldown $d; $null=Resend $d
    Assert ((Verify $d (LatestCode)).BaseResponse.ResponseUri.AbsolutePath -eq '/Account/Login') 'Registration recovers after SMTP becomes available'

    # Simulate a pre-migration account: the new marker defaults false even if EmailConfirmed is false.
    $null=Query 'UPDATE AspNetUsers SET EmailConfirmed=0 WHERE Id=@id' @{id=$a.Id}
    foreach($role in @('Client','Trainer','Admin','HealthcareProfessional')) {
        $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name=@role' @{id=$a.Id;role=$role}
        $legacy=Session; $page=Post $legacy '/Account/Login' @{Email=$a.Email;Password=$password}
        $expected=switch($role) {'Client' {'/Dashboard/Client'} 'Trainer' {'/Dashboard/Trainer'} 'Admin' {'/Dashboard/Admin'} 'HealthcareProfessional' {'/Appointments'}}
        Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq $expected) "Legacy $role with EmailConfirmed=false retains normal sign-in"
    }
    $tampered=Session; $tampered.Cookies.Add((New-Object System.Net.Cookie('HealthTrack.Registration','tampered','/',([uri]$BaseUrl).Host)))
    Assert ((Get $tampered '/Account/VerifyEmailOtp').BaseResponse.ResponseUri.AbsolutePath -eq '/Account/Login') 'Tampered verification context is rejected safely'
    Write-Output "All $script:passed registration OTP checks passed."
} finally {
    $null=Invoke-RestMethod http://127.0.0.1:2527/resume
    $null=Query 'DELETE FROM AspNetUsers WHERE Email LIKE @prefix' @{prefix=$prefix+'%'}
    $db.Dispose()
}
