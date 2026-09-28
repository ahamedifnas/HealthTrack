param(
    [string]$BaseUrl = 'http://localhost:5088',
    [string]$AppDll = 'bin/AdminLoginCheck/HealthTrack.dll'
)

# Repairs the configured development Admin by starting the app; does not create test Client/Trainer accounts.
$ErrorActionPreference = 'Stop'
$secretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\HealthTrack-Development-Identity\secrets.json'
$settings = Get-Content -Raw -LiteralPath $secretPath | ConvertFrom-Json
$email = $settings.'Admin:Email'
$password = $settings.'Admin:Password'
if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($password)) { throw 'Admin:Email and Admin:Password are required.' }
$connection = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$connection.Open()
function Query([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    [void]$command.Parameters.AddWithValue('@email', $email.ToUpperInvariant())
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    Write-Output "PASS: $message"
}
$originalEnvironment = $env:ASPNETCORE_ENVIRONMENT
$appProcess = $null
try {
    # Keep snapshots in memory only; never print hashes or credentials.
    $otherUsers = Query 'SELECT * FROM AspNetUsers WHERE NormalizedEmail <> @email OR NormalizedEmail IS NULL ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES'
    $otherRoles = Query 'SELECT ur.* FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id=ur.UserId WHERE u.NormalizedEmail <> @email OR u.NormalizedEmail IS NULL ORDER BY ur.UserId, ur.RoleId FOR JSON PATH'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $resolvedDll = (Resolve-Path -LiteralPath $AppDll).Path
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        $appProcess = Start-Process -FilePath 'dotnet' -ArgumentList @(('"' + $resolvedDll + '"'), '--urls', $BaseUrl, '--Logging:LogLevel:Default=Warning') -WindowStyle Hidden -PassThru
        $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $page = $null
        for ($retry = 0; $retry -lt 30; $retry++) {
            if ($appProcess.HasExited) { throw 'Application exited before login became available.' }
            try { $page = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Account/Login" -WebSession $session; break }
            catch { Start-Sleep -Milliseconds 500 }
        }
        if ($null -eq $page) { throw 'Application did not become ready.' }
        $match = [regex]::Match($page.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
        if (-not $match.Success) { throw 'Missing login antiforgery token.' }
        $response = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Account/Login" -WebSession $session -Method Post -Body @{
            Email = $email
            Password = $password
            __RequestVerificationToken = [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
        }
        Assert ($response.BaseResponse.ResponseUri.AbsolutePath -eq '/Dashboard/Admin') "Startup $attempt permits configured Admin login and redirects to Admin dashboard"
        Assert ((Query 'SELECT COUNT(*) FROM AspNetUsers WHERE NormalizedEmail=@email AND IsActive=1') -eq 1) 'Configured Admin exists exactly once and is active'
        Assert ((Query "SELECT COUNT(*) FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE u.NormalizedEmail=@email AND r.Name='Admin'") -eq 1) 'Admin role is assigned'
        $storedHash = Query 'SELECT PasswordHash FROM AspNetUsers WHERE NormalizedEmail=@email'
        Assert ($storedHash -and $storedHash -ne $password) 'Database stores a password hash rather than plaintext'
        if ($attempt -eq 1) { $firstHash = $storedHash }
        else { Assert ($storedHash -ne $firstHash) 'Existing Admin password is reset through Identity on Development restart' }
        Assert ((Query 'SELECT * FROM AspNetUsers WHERE NormalizedEmail <> @email OR NormalizedEmail IS NULL ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES') -eq $otherUsers) 'All other user records are unchanged'
        Assert ((Query 'SELECT ur.* FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id=ur.UserId WHERE u.NormalizedEmail <> @email OR u.NormalizedEmail IS NULL ORDER BY ur.UserId, ur.RoleId FOR JSON PATH') -eq $otherRoles) 'All other role assignments are unchanged'
        Stop-Process -Id $appProcess.Id
        $appProcess.WaitForExit()
        $appProcess = $null
    }
}
finally {
    if ($null -ne $appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id }
    $env:ASPNETCORE_ENVIRONMENT = $originalEnvironment
    $connection.Dispose()
    $password = $null
    $settings = $null
}
