param([string]$BaseUrl = 'http://localhost:5086')

# Uses the local development database. All uniquely named test accounts are removed afterward.
$ErrorActionPreference='Stop'
$prefix='trainer-management-' + [Guid]::NewGuid().ToString('N')
$password='Ht5!' + [Guid]::NewGuid().ToString('N')
$database=New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$database.Open()
$script:passed=0
function Query([string]$sql, [hashtable]$parameters=@{}) {
    $command=$database.CreateCommand(); $command.CommandText=$sql
    foreach ($key in $parameters.Keys) { [void]$command.Parameters.AddWithValue('@'+$key,$parameters[$key]) }
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Assert($condition,[string]$message) {
    if (-not $condition) { throw $message }
    $script:passed++; Write-Output "PASS: $message"
}
function Get-Page($session,[string]$path) { return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path) -WebSession $session }
function Submit($session,[string]$getPath,[string]$postPath,[hashtable]$fields) {
    $page=Get-Page $session $getPath
    $match=[regex]::Match($page.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw 'Missing antiforgery token' }
    $fields.__RequestVerificationToken=[System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$postPath) -WebSession $session -Method Post -Body $fields
}
function ExpectStatus([int]$status,[scriptblock]$request,[string]$message) {
    try { $null=& $request; throw "Expected HTTP $status" }
    catch { if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $status) { throw }; Assert $true $message }
}
try {
    $adminEmail="$prefix-admin@example.test"; $trainerEmail="$prefix-trainer@example.test"; $clientEmail="$prefix-client@example.test"
    foreach ($email in @($adminEmail,$clientEmail)) {
        $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $null=Submit $session '/Account/Register' '/Account/Register' @{FullName='Test account'; Email=$email; Password=$password; ConfirmPassword=$password}
    }
    $adminId=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$adminEmail}
    $clientId=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$clientEmail}
    $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name='Admin';" @{id=$adminId}
    $admin=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $client=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $null=Submit $admin '/Account/Login' '/Account/Login' @{Email=$adminEmail;Password=$password}
    $null=Submit $client '/Account/Login' '/Account/Login' @{Email=$clientEmail;Password=$password}
    $dashboard=(Get-Page $admin '/Dashboard/Admin').Content
    Assert ($dashboard.Contains('href="/AdminTrainers/Create"')) 'Admin dashboard links to Create Trainer form'
    foreach ($label in @('Total Users','Total Clients','Total Trainers','Active Users','Total Appointments')) {
        Assert ($dashboard.Contains($label)) "Dashboard preserves $label"
    }
    Assert (-not (Get-Page $client '/Dashboard/Client').Content.Contains('/AdminTrainers')) 'Client navigation excludes Trainers'
    ExpectStatus 403 { Get-Page $client '/AdminTrainers' } 'Client cannot view trainer management'
    ExpectStatus 403 { Submit $client '/Dashboard/Client' '/AdminTrainers/Create' @{} } 'Client cannot create trainers'
    $fields=@{FullName='New Trainer'; Email=$trainerEmail; Password=$password; ConfirmPassword=$password; Role='Admin'; IsActive='false'}
    $bad=$fields.Clone(); $bad.ConfirmPassword='mismatch'
    $page=Submit $admin '/AdminTrainers/Create' '/AdminTrainers/Create' $bad
    Assert ($page.Content.Contains('passwords do not match') -and $page.Content.Contains('Create Trainer') -and -not $page.Content.Contains('<script')) 'Password confirmation validated by server on Create Trainer page'
    $bad=$fields.Clone(); $bad.Password='weak'; $bad.ConfirmPassword='weak'
    $null=Submit $admin '/AdminTrainers/Create' '/AdminTrainers/Create' $bad
    Assert ((Query 'SELECT COUNT(*) FROM AspNetUsers WHERE Email=@email' @{email=$trainerEmail}) -eq 0) 'Weak password cannot create an account'
    $page=Submit $admin '/AdminTrainers/Create' '/AdminTrainers/Create' $fields
    $trainerId=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$trainerEmail}
    Assert ($trainerId -and $page.Content.Contains('Trainer account created') -and $page.Content.Contains('Admin Dashboard')) 'Trainer created and returned to dashboard'
    Assert ($page.Content.Contains($trainerEmail) -and $page.Content.IndexOf('trainer-list-heading') -gt $page.Content.IndexOf('total-appointments')) 'New trainer appears below dashboard cards'
    $created=Query 'SELECT CreatedAt FROM AspNetUsers WHERE Id=@id' @{id=$trainerId}
    Assert ($page.Content.Contains('Created Date') -and $page.Content.Contains($created.ToString('yyyy-MM-dd'))) 'Dashboard displays trainer creation date'
    Assert ($page.Content.Contains('href="/AdminTrainers/Edit/') -and $page.Content.Contains('/AdminTrainers/SetActive/')) 'Dashboard includes MVC Edit and status controls'
    Assert ((Query 'SELECT IsActive FROM AspNetUsers WHERE Id=@id' @{id=$trainerId}) -eq $true) 'Trainer is active despite forged IsActive value'
    Assert ((Query 'SELECT r.Name FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=@id' @{id=$trainerId}) -eq 'Trainer') 'Trainer role assigned despite forged Admin role'
    $hash=Query 'SELECT PasswordHash FROM AspNetUsers WHERE Id=@id' @{id=$trainerId}
    Assert ($hash -and $hash -ne $password) 'Identity stores a password hash'
    Assert ((Get-Page $admin '/Dashboard/Admin').Content.Contains('Admin Dashboard')) 'Creating trainer preserves Admin sign-in'
    $duplicate=$fields.Clone(); $duplicate.Email=$trainerEmail.ToUpperInvariant()
    $page=Submit $admin '/AdminTrainers/Create' '/AdminTrainers/Create' $duplicate
    Assert ($page.Content.Contains('email already exists') -and (Query 'SELECT COUNT(*) FROM AspNetUsers WHERE Email=@email' @{email=$trainerEmail}) -eq 1) 'Case-insensitive duplicate email rejected'
    Assert ($page.Content.Contains($duplicate.Email)) 'Entered email remains visible after validation failure'
    $duplicate.Email=$clientEmail
    $page=Submit $admin '/AdminTrainers/Create' '/AdminTrainers/Create' $duplicate
    Assert ($page.Content.Contains('email already exists')) 'Existing Client email cannot be reused or promoted'
    $trainer=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page=Submit $trainer '/Account/Login' '/Account/Login' @{Email=$trainerEmail;Password=$password}
    Assert ($page.Content.Contains('Trainer Dashboard')) 'New Trainer can log in'
    ExpectStatus 403 { Get-Page $trainer '/AdminTrainers' } 'Trainer cannot manage trainers'
    foreach ($path in @('/Dashboard/Admin','/Admin/Clients','/Admin/Users','/Admin/Appointments','/AdminTrainers/Create',"/AdminTrainers/Edit/$trainerId")) {
        Assert ((Get-Page $admin $path).StatusCode -eq 200) "Admin can open $path"
        ExpectStatus 403 { Get-Page $client $path } "Client denied $path"
        ExpectStatus 403 { Get-Page $trainer $path } "Trainer denied $path"
    }
    Assert ((Get-Page $client '/Appointments/Book').Content.Contains($trainerEmail)) 'Active trainer appears in booking'
    $page=Submit $admin "/AdminTrainers/Edit/$trainerId" "/AdminTrainers/Edit/$trainerId" @{FullName='Edited Trainer';Email=$clientEmail}
    Assert ($page.Content.Contains('email already exists') -and $page.Content.Contains('Edit Trainer') -and -not $page.Content.Contains('<script')) 'Trainer edit rejects duplicate email on MVC form'
    $editedEmail="$prefix-edited@example.test"
    $page=Submit $admin '/Dashboard/Admin' '/AdminTrainers/Edit' @{id=$trainerId;FullName='Edited Trainer';Email=$editedEmail;IsActive='false';Role='Admin'}
    Assert ($page.Content.Contains('Admin Dashboard') -and $page.Content.Contains($editedEmail)) 'MVC edit returns to updated dashboard list'
    Assert ((Query 'SELECT FullName FROM AspNetUsers WHERE Id=@id' @{id=$trainerId}) -eq 'Edited Trainer') 'Trainer name updated'
    Assert ((Query 'SELECT Email FROM AspNetUsers WHERE Id=@id' @{id=$trainerId}) -eq $editedEmail) 'Trainer email updated'
    Assert ((Query 'SELECT PasswordHash FROM AspNetUsers WHERE Id=@id' @{id=$trainerId}) -eq $hash) 'Editing preserves password hash'
    Assert ((Get-Page $client '/Appointments/Book').Content.Contains($editedEmail)) 'Edited trainer appears in booking'
    $trainerEmail=$editedEmail
    ExpectStatus 403 { Submit $client '/Dashboard/Client' "/AdminTrainers/SetActive/$trainerId" @{isActive='false'} } 'Non-admin status mutation blocked'
    ExpectStatus 404 { Submit $admin '/AdminTrainers' "/AdminTrainers/SetActive/$clientId" @{isActive='false'} } 'Non-trainer ID cannot be deactivated through trainer management'
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/AdminTrainers/SetActive/$trainerId" -WebSession $admin -Method Post -Body @{isActive='false'} } 'Status mutation requires antiforgery token'
    $null=Submit $admin '/AdminTrainers' "/AdminTrainers/SetActive/$trainerId" @{isActive='false'}
    Assert ((Query 'SELECT IsActive FROM AspNetUsers WHERE Id=@id' @{id=$trainerId}) -eq $false) 'Admin deactivates trainer'
    Assert (-not (Get-Page $client '/Appointments/Book').Content.Contains($trainerEmail)) 'Inactive trainer excluded from booking'
    Assert ((Get-Page $trainer '/Dashboard/Trainer').Content.Contains('name="Password"')) 'Deactivation rejects existing trainer session'
    $page=Submit $trainer '/Account/Login' '/Account/Login' @{Email=$trainerEmail;Password=$password}
    Assert ($page.Content.Contains('Unable to sign in')) 'Inactive trainer cannot log in'
    $null=Submit $admin '/AdminTrainers' "/AdminTrainers/SetActive/$trainerId" @{isActive='true'}
    $page=Submit $trainer '/Account/Login' '/Account/Login' @{Email=$trainerEmail;Password=$password}
    Assert ($page.Content.Contains('Trainer Dashboard')) 'Reactivated trainer can log in'
    Assert ((Get-Page $client '/Appointments/Book').Content.Contains($trainerEmail)) 'Reactivated trainer returns to booking'
    Write-Output "All $script:passed trainer management checks passed."
}
finally {
    $null=Query 'DELETE FROM AspNetUsers WHERE Email LIKE @prefix' @{prefix=($prefix+'%')}
    $database.Dispose()
}

