param([string]$BaseUrl = 'http://localhost:5075')

# Run against the local development app. Only the uniquely named test user is removed.
$ErrorActionPreference = 'Stop'
$email = 'phase1-' + [Guid]::NewGuid().ToString('N') + '@example.test'
$password = 'Ht1!' + [Guid]::NewGuid().ToString('N')
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$database = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$database.Open()

function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    Write-Output "PASS: $message"
}
function Get-Token([string]$html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw 'Missing antiforgery token.' }
    return $match.Groups[1].Value
}
function Get-Page([string]$path) {
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -WebSession $session
}
function Post-Form([string]$path, $fields) {
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -WebSession $session -Method Post -Body $fields
}
function Query([string]$sql) {
    $command = $database.CreateCommand()
    $command.CommandText = $sql
    [void]$command.Parameters.AddWithValue('@email', $email)
    return $command.ExecuteScalar()
}

try {
    Assert ((Query 'SELECT COUNT(*) FROM __EFMigrationsHistory') -ge 1) 'Initial migration applied'
    Assert ((Query "SELECT COUNT(*) FROM AspNetRoles WHERE Name IN ('Admin','Trainer','Client')") -eq 3) 'Three roles seeded'
    $page = Get-Page '/Account/Register'
    $fields = @{ FullName='Phase One Test'; Email=$email; Password='weak'; ConfirmPassword='weak'; __RequestVerificationToken=(Get-Token $page.Content) }
    $page = Post-Form '/Account/Register' $fields
    Assert ((Query 'SELECT COUNT(*) FROM AspNetUsers WHERE Email=@email') -eq 0) 'Weak password rejected'
    $fields.Password = $password
    $fields.ConfirmPassword = 'mismatch'
    $fields.__RequestVerificationToken = Get-Token $page.Content
    $page = Post-Form '/Account/Register' $fields
    Assert ((Query 'SELECT COUNT(*) FROM AspNetUsers WHERE Email=@email') -eq 0) 'Password mismatch rejected'
    $fields.ConfirmPassword = $password
    $fields.Role = 'Admin'
    $fields.IsActive = 'false'
    $fields.__RequestVerificationToken = Get-Token $page.Content
    $page = Post-Form '/Account/Register' $fields
    Assert ($page.Content.Contains('Hello, ' + $email)) 'Registration signs in'
    Assert ((Query 'SELECT r.Name FROM AspNetUserRoles ur JOIN AspNetUsers u ON u.Id=ur.UserId JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE u.Email=@email') -eq 'Client') 'Forged Admin role ignored; Client assigned'
    Assert ((Query 'SELECT IsActive FROM AspNetUsers WHERE Email=@email') -eq $true) 'Profile overposting ignored'
    Assert ((Query 'SELECT PasswordHash FROM AspNetUsers WHERE Email=@email') -ne $password) 'Password stored as hash'
    try { $null = Post-Form '/Account/Logout' @{}; throw 'Logout accepted missing antiforgery token' }
    catch { if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 400) { throw } }
    Write-Output 'PASS: Logout requires antiforgery token'
    $page = Post-Form '/Account/Logout' @{ __RequestVerificationToken=(Get-Token $page.Content) }
    Assert (-not $page.Content.Contains('Hello, ' + $email)) 'Logout clears sign-in'
    $page = Get-Page '/Account/Register'
    $fields.__RequestVerificationToken = Get-Token $page.Content
    $page = Post-Form '/Account/Register' $fields
    Assert ($page.Content.Contains('already taken')) 'Duplicate registration rejected'
    $page = Get-Page '/Account/Login'
    $login = @{ Email=$email; Password='Wrong1!password'; __RequestVerificationToken=(Get-Token $page.Content) }
    $page = Post-Form '/Account/Login' $login
    Assert ($page.Content.Contains('Unable to sign in')) 'Incorrect password rejected'
    $login.Password = $password
    $login.ReturnUrl = 'https://example.com'
    $login.__RequestVerificationToken = Get-Token $page.Content
    $page = Post-Form '/Account/Login' $login
    Assert ($page.Content.Contains('Hello, ' + $email)) 'Login succeeds and external return URL is ignored'
    $null = Query 'UPDATE AspNetUsers SET IsActive=0 WHERE Email=@email'
    $page = Get-Page '/'
    Assert (-not $page.Content.Contains('Hello, ' + $email)) 'Inactive user cookie rejected'
    $page = Get-Page '/Account/Login'
    $login.__RequestVerificationToken = Get-Token $page.Content
    $page = Post-Form '/Account/Login' $login
    Assert ($page.Content.Contains('Unable to sign in')) 'Inactive user login rejected'
    try { $null = Get-Page '/Account/AccessDenied'; throw 'Access denied did not return 403' }
    catch { if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 403) { throw } }
    Write-Output 'PASS: Access denied returns 403'
}
finally {
    $null = Query 'DELETE FROM AspNetUsers WHERE Email=@email'
    $database.Dispose()
}
