param([string]$BaseUrl='http://localhost:5075')
# Opt-in REAL Gmail test. Uses a unique plus-address in the configured Gmail inbox.
# Secrets, mail content, tokens and generated passwords remain in memory only.
# Deletes only its disposable HealthTrack account, never any mailbox messages.
$ErrorActionPreference='Stop'
$db=$null; $client=$null; $ssl=$null; $reader=$null; $writer=$null
$email=$null; $created=$false; $stage='configuration'
function Assert($ok,$message) { if(-not $ok) { throw $message }; Write-Output "PASS: $message" }
function Get($s,$path) { Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $s }
function Post($s,$path,$fields,$getPath=$path) {
    $page=Get $s $getPath
    $fields.__RequestVerificationToken=[System.Net.WebUtility]::HtmlDecode([regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value)
    Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $s -Method Post -Body $fields
}
function QuoteImap([string]$value) { '"'+$value.Replace('\','\\').Replace('"','\"')+'"' }
$script:tag=0
function Imap([string]$command) {
    $script:tag++; $tag='ht'+$script:tag
    $writer.WriteLine($tag+' '+$command); $writer.Flush()
    $lines=New-Object 'System.Collections.Generic.List[string]'
    while($true) {
        $line=$reader.ReadLine()
        if($null -eq $line) { throw 'IMAP connection closed' }
        if($line.StartsWith($tag+' ')) {
            if(-not $line.StartsWith($tag+' OK')) { throw 'IMAP command rejected' }
            break
        }
        $lines.Add($line)
    }
    return ($lines -join "`r`n")
}
try {
    $settings=Get-Content -Raw appsettings.json | ConvertFrom-Json
    $secretPath=Join-Path $env:APPDATA 'Microsoft/UserSecrets/HealthTrack-Development-Identity/secrets.json'
    $secrets=Get-Content -Raw -LiteralPath $secretPath | ConvertFrom-Json
    function Setting($key) {
        $value=$secrets."EmailSettings:$key"
        if($null -eq $value -and $secrets.EmailSettings) { $value=$secrets.EmailSettings.$key }
        return [string]$value
    }
    $username=Setting 'Username'; $appPassword=Setting 'Password'
    Assert ((Setting 'Host') -eq 'smtp.gmail.com' -and (Setting 'Port') -eq '587' -and (Setting 'EnableSsl') -eq 'true') 'Gmail settings use port 587 and TLS'
    $address=New-Object System.Net.Mail.MailAddress($username)
    $email=$address.User.Split('+')[0]+'+healthtrack-'+[Guid]::NewGuid().ToString('N')+'@'+$address.Host
    $password='Ht9!'+[Guid]::NewGuid().ToString('N')
    $newPassword='Ht8!'+[Guid]::NewGuid().ToString('N')
    $connection=$settings.ConnectionStrings.DefaultConnection
    if($env:ConnectionStrings__DefaultConnection) { $connection=$env:ConnectionStrings__DefaultConnection }
    $db=New-Object System.Data.SqlClient.SqlConnection($connection); $db.Open()
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $stage='disposable account registration'
    $page=Post $session '/Account/Register' @{FullName='Gmail recovery test';Email=$email;Password=$password;ConfirmPassword=$password}
    $command=$db.CreateCommand(); $command.CommandText='SELECT COUNT(*) FROM AspNetUsers WHERE Email=@email'; [void]$command.Parameters.AddWithValue('@email',$email)
    $created=([int]$command.ExecuteScalar() -eq 1); $command.Dispose()
    Assert $created 'Disposable Gmail plus-address account created'
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $stage='forgot password submission'
    $page=Post $session '/Account/ForgotPassword' @{Email=$email}
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Account/ForgotPasswordConfirmation') 'Real recovery request reached generic confirmation (not proof of delivery)'

    $stage='Gmail inbox verification over IMAP'
    $client=New-Object System.Net.Sockets.TcpClient
    $client.ReceiveTimeout=15000; $client.SendTimeout=15000
    $client.Connect('imap.gmail.com',993)
    $ssl=New-Object System.Net.Security.SslStream($client.GetStream(),$false)
    $ssl.AuthenticateAsClient('imap.gmail.com')
    $reader=New-Object System.IO.StreamReader($ssl)
    $writer=New-Object System.IO.StreamWriter($ssl)
    $writer.NewLine="`r`n"
    $null=$reader.ReadLine()
    $null=Imap ('LOGIN '+(QuoteImap $username)+' '+(QuoteImap $appPassword))
    $null=Imap 'EXAMINE INBOX'
    $messageId=$null
    for($attempt=0;$attempt -lt 10;$attempt++) {
        $search=Imap ('UID SEARCH HEADER To '+(QuoteImap $email))
        $match=[regex]::Match($search,'(?m)^\* SEARCH ([0-9 ]+)')
        if($match.Success) { $messageId=($match.Groups[1].Value.Trim() -split ' ')[-1]; break }
        Start-Sleep -Seconds 3
    }
    Assert ($null -ne $messageId) 'Real reset email arrived in Gmail inbox'
    $mail=Imap ('UID FETCH '+$messageId+' BODY.PEEK[]')
    $mail=$mail -replace '=\r\n',''
    $mail=[regex]::Replace($mail,'=([0-9A-Fa-f]{2})',{param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value,16)})
    $url=[System.Net.WebUtility]::HtmlDecode([regex]::Match($mail,'href="([^"]+/Account/ResetPassword\?[^"]+)"').Groups[1].Value)
    Assert (-not [string]::IsNullOrWhiteSpace($url)) 'Received email includes real reset link'
    Assert ($mail.Contains('HealthTrack Team') -and $mail.Contains('Reset Password')) 'Received email contains HealthTrack branding and reset button'
    Assert (([uri]$url).GetLeftPart([UriPartial]::Authority) -eq $BaseUrl) 'Reset link uses the expected local test origin'
    Add-Type -AssemblyName System.Web
    $query=[System.Web.HttpUtility]::ParseQueryString(([uri]$url).Query)
    $resetPath=([uri]$url).PathAndQuery
    $stage='reset and login verification'
    $page=Post $session '/Account/ResetPassword' @{Email=$email;Token=$query['token'];Password=$newPassword;ConfirmPassword=$newPassword} $resetPath
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Account/ResetPasswordConfirmation') 'Link received through Gmail resets password successfully'
    $page=Post $session '/Account/Login' @{Email=$email;Password=$password}
    Assert ($page.Content.Contains('Unable to sign in')) 'Old password fails after Gmail recovery'
    $page=Post $session '/Account/Login' @{Email=$email;Password=$newPassword}
    Assert ($page.BaseResponse.ResponseUri.AbsolutePath -eq '/Dashboard/Client') 'New password signs in after Gmail recovery'
    Write-Output 'All real Gmail recovery checks passed.'
} catch {
    # Never print exception payloads: network errors can include credentials or message data.
    Write-Output "FAIL: Gmail recovery test stopped during $stage. No secrets or exception details were printed."
    $failed=$true
} finally {
    if($writer) { $writer.Dispose() }; if($reader) { $reader.Dispose() }; if($ssl) { $ssl.Dispose() }; if($client) { $client.Dispose() }
    if($db -and $db.State -eq 'Open' -and $created) {
        $command=$db.CreateCommand(); $command.CommandText='DELETE FROM AspNetUsers WHERE Email=@email'
        [void]$command.Parameters.AddWithValue('@email',$email); $null=$command.ExecuteNonQuery(); $command.Dispose()
        Write-Output 'Disposable Gmail test account removed; existing accounts were unchanged.'
    }
    if($db) { $db.Dispose() }
}
if($failed) { exit 1 }
