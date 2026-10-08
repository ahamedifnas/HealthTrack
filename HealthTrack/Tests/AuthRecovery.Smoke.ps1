param([string]$BaseUrl='http://localhost:5092')
# Requires AuthSmtp.Fixture.cjs and app configured for its SMTP port (2526).
# Uses the existing development LocalDB and removes only uniquely named test accounts.
$ErrorActionPreference='Stop'
$prefix='auth-'+[Guid]::NewGuid().ToString('N')
$password='Ht9!'+[Guid]::NewGuid().ToString('N')
$newPassword='Ht8!'+[Guid]::NewGuid().ToString('N')
$db=New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$db.Open()
function Query($sql,$parameters=@{}) {
    $c=$db.CreateCommand(); $c.CommandText=$sql
    foreach($key in $parameters.Keys) { [void]$c.Parameters.AddWithValue('@'+$key,$parameters[$key]) }
    try { $c.ExecuteScalar() } finally { $c.Dispose() }
}
function Assert($ok,$message) { if(-not $ok) { throw $message }; Write-Output "PASS: $message" }
function Session { New-Object Microsoft.PowerShell.Commands.WebRequestSession }
function Get($s,$path) { Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $s }
function Post($s,$path,$fields,$getPath=$path) {
    $page=Get $s $getPath
    $fields.__RequestVerificationToken=[System.Net.WebUtility]::HtmlDecode([regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value)
    Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $s -Method Post -Body $fields
}
function Messages { @(Invoke-RestMethod http://127.0.0.1:2527/) }
try {
    $null=Invoke-RestMethod http://127.0.0.1:2527/resume
    $s=Session
    foreach($role in @('Client','Trainer','Admin','HealthcareProfessional')) {
        $email="$prefix-$role@example.test"
        $s=Session
        $page=Post $s '/Account/Register' @{FullName="Auth $role";Email=$email;Password=$password;ConfirmPassword=$password}
        Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Dashboard/Client') "Registration creates a Client account ($role test)"
        $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email=@email); INSERT INTO AspNetUserRoles SELECT u.Id,r.Id FROM AspNetUsers u CROSS JOIN AspNetRoles r WHERE u.Email=@email AND r.Name=@role' @{email=$email;role=$role}
        $s=Session
        $page=Post $s '/Account/Login' @{Email=$email;Password=$password;RememberMe='true';ReturnUrl='https://example.test/evil'}
        $expected=switch($role) { 'Client' {'/Dashboard/Client'} 'Trainer' {'/Dashboard/Trainer'} 'Admin' {'/Dashboard/Admin'} 'HealthcareProfessional' {'/Appointments'} }
        Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq $expected) "$role login preserves dashboard redirect and rejects external return URL"
        $cookie=$s.Cookies.GetCookies([uri]$BaseUrl) | Where-Object Name -eq '.AspNetCore.Identity.Application'
        Assert ($cookie -and $cookie.Expires -gt [DateTime]::Now) "$role Remember Me persists authentication cookie"
    }
    $email="$prefix-Client@example.test"
    $s=Session
    $page=Post $s '/Account/ForgotPassword' @{Email='invalid'}
    Assert ($page.Content -match 'not a valid e-mail address') 'Invalid recovery email rejected'
    try { $null=Invoke-WebRequest -UseBasicParsing "$BaseUrl/Account/ForgotPassword" -Method Post -Body @{Email=$email}; throw 'CSRF accepted' }
    catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 400) 'Recovery POST requires antiforgery token' }
    $before=(Messages).Count
    $unknown=Post $s '/Account/ForgotPassword' @{Email="$prefix-missing@example.test"}
    Assert ((Messages).Count -eq $before) 'Unknown account does not send email'
    $known=Post $s '/Account/ForgotPassword' @{Email=$email}
    Assert ($known.Content -eq $unknown.Content) 'Known and unknown emails receive identical generic confirmation'
    $messages=Messages
    Assert ($messages.Count -eq ($before+1)) 'SMTP receiver accepted a real reset message'
    $mail=$messages[-1] -replace '=\r\n',''
    $mail=[regex]::Replace($mail,'=([0-9A-Fa-f]{2})',{param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value,16)})
    $url=[System.Net.WebUtility]::HtmlDecode([regex]::Match($mail,'href="([^"]+)"').Groups[1].Value)
    Assert ($url.StartsWith($BaseUrl+'/Account/ResetPassword?')) 'Reset link uses configured trusted public URL'
    Add-Type -AssemblyName System.Web
    $query=[System.Web.HttpUtility]::ParseQueryString(([uri]$url).Query)
    $token=$query['token']
    Assert (-not [string]::IsNullOrWhiteSpace($token)) 'Email contains URL-safe Identity token'
    $resetPath=([uri]$url).PathAndQuery
    $page=Post $s '/Account/ResetPassword' @{Email=$email;Token=$token;Password='weak';ConfirmPassword='weak'} $resetPath
    Assert ($page.Content -match 'Passwords must') 'Identity password rules reject weak reset password'
    $page=Post $s '/Account/ResetPassword' @{Email=$email;Token=$token;Password=$newPassword;ConfirmPassword='mismatch'} $resetPath
    Assert ($page.Content -match 'passwords do not match') 'Reset confirmation must match'
    $oldSession=Session
    $null=Post $oldSession '/Account/Login' @{Email=$email;Password=$password}
    $page=Post $s '/Account/ResetPassword' @{Email=$email;Token=$token;Password=$newPassword;ConfirmPassword=$newPassword} $resetPath
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Account/ResetPasswordConfirmation') 'Valid token resets password successfully'
    Assert ((Get $s '/Dashboard').Content -match 'name="Password"') 'Password reset does not automatically sign in'
    Assert ((Get $oldSession '/Dashboard').Content -match 'name="Password"') 'Password reset invalidates existing authenticated session'
    $page=Post $s '/Account/Login' @{Email=$email;Password=$password}
    Assert ($page.Content -match 'Unable to sign in') 'Old password no longer works'
    $page=Post $s '/Account/Login' @{Email=$email;Password=$newPassword}
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Dashboard/Client') 'New password signs in successfully'
    $s=Session
    $page=Post $s '/Account/ResetPassword' @{Email=$email;Token=$token;Password=$password;ConfirmPassword=$password} $resetPath
    Assert ($page.Content -match 'invalid or has expired') 'Used reset token cannot be replayed'
    foreach($path in @('/Account/ResetPassword','/Account/ResetPassword?email=a%40example.test&token=%25%25%25')) {
        Assert ((Get $s $path).Content -match 'invalid or has expired') 'Missing or malformed reset URL displays friendly guidance'
    }
    $page=Post $s '/Account/ResetPassword' @{Email=$email;Token='aW52YWxpZA';Password=$newPassword;ConfirmPassword=$newPassword}
    Assert ($page.Content -match 'invalid or has expired') 'Well-formed but invalid token is rejected'
    try { $null=Invoke-WebRequest -UseBasicParsing "$BaseUrl/Account/ResetPassword" -Method Post -Body @{Email=$email}; throw 'CSRF accepted' }
    catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 400) 'Reset POST requires antiforgery token' }
    $null=Invoke-RestMethod http://127.0.0.1:2527/fail
    $failed=Post $s '/Account/ForgotPassword' @{Email=$email}
    Assert ($failed.Content -eq $unknown.Content) 'SMTP failure preserves generic response without claiming successful delivery'
    $null=Invoke-RestMethod http://127.0.0.1:2527/resume
    $null=Query 'UPDATE AspNetUsers SET IsActive=0 WHERE Email=@email' @{email=$email}
    $before=(Messages).Count
    $page=Post $s '/Account/ForgotPassword' @{Email=$email}
    Assert ($page.Content -eq $unknown.Content -and (Messages).Count -eq $before) 'Inactive recovery receives same response without email'
    $page=Post $s '/Account/Login' @{Email=$email;Password=$newPassword}
    Assert ($page.Content -match 'Unable to sign in') 'Inactive login remains blocked'
    $null=Query 'UPDATE AspNetUsers SET IsActive=1 WHERE Email=@email' @{email=$email}
    1..5 | ForEach-Object { $null=Post $s '/Account/Login' @{Email=$email;Password='wrong'} }
    $page=Post $s '/Account/Login' @{Email=$email;Password=$newPassword}
    Assert ($page.Content -match 'Unable to sign in') 'Five failed attempts retain Identity lockout'
    Write-Output 'All authentication recovery checks passed.'
} finally {
    $null=Invoke-RestMethod http://127.0.0.1:2527/resume
    $null=Query 'DELETE FROM AspNetUsers WHERE Email LIKE @prefix' @{prefix=$prefix+'%'}
    $db.Dispose()
}
