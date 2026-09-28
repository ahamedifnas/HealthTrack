param([string]$BaseUrl = 'http://localhost:5075')

# Integration test for the default development LocalDB. Temporary accounts and appointments
# are removed in finally. Past appointments are simulated only on this test's own records.
$ErrorActionPreference = 'Stop'
$prefix = 'phase4-' + [Guid]::NewGuid().ToString('N')
$password = 'Ht4!' + [Guid]::NewGuid().ToString('N')
$database = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$database.Open()
$settings = Get-Content -Raw "$PSScriptRoot\..\appsettings.json" | ConvertFrom-Json
$zoneId = $settings.Appointments.TimeZoneId
try { $zone = [TimeZoneInfo]::FindSystemTimeZoneById($zoneId) }
catch {
    # Windows PowerShell on .NET Framework uses the Windows ID for the default zone.
    if ($zoneId -ne 'Asia/Colombo') { throw }
    $zone = [TimeZoneInfo]::FindSystemTimeZoneById('Sri Lanka Standard Time')
}
$now = [TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow, $zone)
$today=$now.ToString('yyyy-MM-dd'); $tomorrow=$now.AddDays(1).ToString('yyyy-MM-dd'); $later=$now.AddDays(2).ToString('yyyy-MM-dd')
$script:passed=0

function Query([string]$sql, [hashtable]$parameters = @{}) {
    $command = $database.CreateCommand()
    $command.CommandText = $sql
    foreach ($key in $parameters.Keys) { [void]$command.Parameters.AddWithValue('@' + $key, $parameters[$key]) }
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:passed++
    Write-Output "PASS: $message"
}
function Get-Page($session, [string]$path) {
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -WebSession $session
}
function Hidden([string]$html, [string]$name) {
    $match = [regex]::Match($html, ('name="' + [regex]::Escape($name) + '"[^>]*value="([^"]+)"'))
    if (-not $match.Success) { throw "Missing field $name" }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}
function Submit($session, [string]$getPath, [string]$postPath, [hashtable]$fields) {
    $page = Get-Page $session $getPath
    $fields.__RequestVerificationToken = Hidden $page.Content '__RequestVerificationToken'
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $postPath) -WebSession $session -Method Post -Body $fields
}
function ExpectStatus([int]$status, [scriptblock]$request, [string]$message) {
    try { $null = & $request; throw "Expected HTTP $status : $message" }
    catch {
        if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $status) { throw }
        $script:passed++
        Write-Output "PASS: $message (HTTP $status)"
    }
}
function Register([string]$name) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $email = "$prefix-$name@example.test"
    $null = Submit $session '/Account/Register' '/Account/Register' @{FullName="$prefix $name"; Email=$email; Password=$password; ConfirmPassword=$password}
    $id=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}
    if (-not $id) { throw 'Test registration failed' }
    return @{Id=$id; Email=$email; Session=$session}
}
function Login($account) {
    $account.Session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $null=Submit $account.Session '/Account/Login' '/Account/Login' @{Email=$account.Email; Password=$password}
}
function Book($session, [string]$trainerId, [string]$date, [string]$time, [string]$reason) {
    $null=Submit $session '/Appointments/Book' '/Appointments/Book' @{TrainerId=$trainerId; AppointmentDate=$date; AppointmentTime=$time; Reason=$reason}
    $id=Query 'SELECT Id FROM Appointments WHERE Reason=@reason' @{reason=$reason}
    if (-not $id) { throw 'Appointment creation failed' }
    return $id
}
function Status($id) { return Query 'SELECT Status FROM Appointments WHERE Id=@id' @{id=$id} }
function Version($session, $id) { return Hidden (Get-Page $session "/Appointments/Edit/$id").Content 'RowVersion' }

try {
    Assert ((Query "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AppointmentSystem'") -eq 1) 'Appointment migration applied'
    $trainer=Register 'TrainerOne'; $trainer2=Register 'TrainerTwo'; $client=Register 'ClientOne'; $client2=Register 'ClientTwo'; $admin=Register 'AdminTrainer'
    foreach ($account in @($trainer,$trainer2)) {
        $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name='Trainer';" @{id=$account.Id}
    }
    $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name IN ('Admin','Trainer','Client');" @{id=$admin.Id}
    foreach ($account in @($trainer,$trainer2,$client,$client2,$admin)) { Login $account }
    $t=$trainer.Session; $t2=$trainer2.Session; $c=$client.Session; $c2=$client2.Session; $a=$admin.Session
    $anonymous=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    Assert ((Get-Page $anonymous '/Appointments').Content.Contains('name="Password"')) 'Anonymous appointment access redirects to login'
    ExpectStatus 403 { Get-Page $t '/Appointments/Book' } 'Trainer cannot use client booking page'
    ExpectStatus 403 { Get-Page $a '/Appointments/Book' } 'Admin cannot book even with Client and Trainer roles'
    $page=Get-Page $c '/Appointments/Book'
    Assert ($page.Content.Contains($trainer.Email) -and $page.Content.Contains($trainer2.Email) -and -not $page.Content.Contains($admin.Email)) 'Client can choose active non-admin trainers without a plan assignment'
    $fields=@{TrainerId=$trainer.Id; AppointmentDate=$tomorrow; AppointmentTime='10:00'; Reason="$prefix primary"; ClientId=$client2.Id; Status=3; Notes='Forged note'; CreatedAt='2000-01-01'}
    $bad=$fields.Clone(); $bad.AppointmentDate=$now.AddDays(-1).ToString('yyyy-MM-dd')
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $bad
    Assert ($page.Content.Contains('Choose a future date and time')) 'Past booking rejected'
    $bad=$fields.Clone(); $bad.AppointmentDate=$today; $bad.AppointmentTime='00:00'
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $bad
    Assert ($page.Content.Contains('Choose a future date and time')) 'Earlier time today rejected'
    $bad=$fields.Clone(); $bad.AppointmentTime=''
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $bad
    Assert ($page.Content.Contains('field is required')) 'Missing appointment time rejected'
    $bad=$fields.Clone(); $bad.Reason=' '
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $bad
    Assert ($page.Content.Contains('Reason field is required')) 'Blank reason rejected'
    $bad=$fields.Clone(); $bad.TrainerId=$client2.Id
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $bad
    Assert ($page.Content.Contains('Choose an active trainer')) 'Client cannot book a non-trainer account'
    $null=Query 'UPDATE AspNetUsers SET IsActive=0 WHERE Id=@id' @{id=$trainer2.Id}
    $bad=$fields.Clone(); $bad.TrainerId=$trainer2.Id
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $bad
    Assert ($page.Content.Contains('Choose an active trainer')) 'Inactive trainer rejected'
    $null=Query 'UPDATE AspNetUsers SET IsActive=1 WHERE Id=@id' @{id=$trainer2.Id}
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $fields
    $id=Query 'SELECT Id FROM Appointments WHERE Reason=@reason' @{reason=$fields.Reason}
    Assert ($id -and (Status $id) -eq 1) 'Booking starts Pending'
    Assert ((Query 'SELECT ClientId FROM Appointments WHERE Id=@id' @{id=$id}) -eq $client.Id) 'ClientId comes from sign-in'
    Assert ((Query "SELECT COUNT(*) FROM Appointments WHERE Id=@id AND Notes IS NULL AND CreatedAt > '2020-01-01'" @{id=$id}) -eq 1) 'Notes and creation timestamp cannot be overposted'
    $other=Book $c2 $trainer2.Id $tomorrow '11:00' "$prefix other"
    $page=Get-Page $c '/Appointments'
    Assert ($page.Content.Contains("/Appointments/Details/$id") -and -not $page.Content.Contains("/Appointments/Details/$other")) 'Client list contains only own appointments'
    $page=Get-Page $t '/Appointments'
    Assert ($page.Content.Contains("/Appointments/Details/$id") -and -not $page.Content.Contains("/Appointments/Details/$other")) 'Trainer list contains only assigned appointments'
    $page=Get-Page $a '/Appointments'
    Assert ($page.Content.Contains("/Appointments/Details/$id") -and $page.Content.Contains("/Appointments/Details/$other")) 'Admin list includes all appointments'
    ExpectStatus 404 { Get-Page $c2 "/Appointments/Details/$id" } 'Other client cannot view details'
    ExpectStatus 404 { Get-Page $t2 "/Appointments/Details/$id" } 'Other trainer cannot view details'
    ExpectStatus 404 { Submit $c2 '/Appointments' "/Appointments/Cancel/$id" @{} } 'Other client cannot cancel'
    ExpectStatus 404 { Get-Page $t2 "/Appointments/Edit/$id" } 'Other trainer cannot open edit'
    foreach ($action in @('Edit','Approve','TrainerCancel','Complete')) {
        ExpectStatus 404 { Submit $t2 '/Appointments' "/Appointments/$action/$id" @{} } "Other trainer cannot $action"
        ExpectStatus 403 { Submit $a '/Appointments' "/Appointments/$action/$id" @{} } "Admin cannot $action"
        ExpectStatus 403 { Submit $c '/Appointments' "/Appointments/$action/$id" @{} } "Client cannot invoke trainer action $action"
    }
    ExpectStatus 403 { Submit $a '/Appointments' "/Appointments/Cancel/$id" @{} } 'Admin cannot invoke client cancellation'
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Appointments/Approve/$id" -WebSession $t -Method Post } 'Approval requires antiforgery token'
    ExpectStatus 404 { Get-Page $t "/Appointments/Approve/$id" } 'GET cannot approve'
    $bad=$fields.Clone(); $bad.Reason="$prefix conflict"
    $page=Submit $c2 '/Appointments/Book' '/Appointments/Book' $bad
    Assert ($page.Content.Contains('already has a Pending/Approved appointment')) 'Trainer duplicate slot blocked'
    $bad.TrainerId=$trainer2.Id
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $bad
    Assert ($page.Content.Contains('already has a Pending/Approved appointment')) 'Client duplicate slot with another trainer blocked'
    $page=Submit $t '/Appointments' "/Appointments/Complete/$id" @{}
    Assert ((Status $id) -eq 1 -and $page.Content.Contains('Only Approved appointments')) 'Pending cannot transition directly to Completed'
    $page=Get-Page $t '/Dashboard/Trainer'
    Assert ($page.Content -match 'data-testid="pending-appointments">1<') 'Trainer dashboard pending count is scoped'
    Assert (-not (Get-Page $c '/Dashboard/Client').Content.Contains('data-testid="next-appointment"')) 'Pending appointment excluded from next approved appointment'
    $null=Submit $t '/Appointments' "/Appointments/Approve/$id" @{}
    Assert ((Status $id) -eq 2) 'Trainer approves Pending appointment'
    $page=Get-Page $c '/Dashboard/Client'
    Assert ($page.Content.Contains('data-testid="next-appointment"') -and $page.Content.Contains("/Appointments/Details/$id")) 'Client dashboard shows upcoming approved appointment'
    Assert (-not (Get-Page $c2 '/Dashboard/Client').Content.Contains("/Appointments/Details/$id")) 'Client dashboard excludes other client appointment'
    $page=Submit $c '/Appointments' "/Appointments/Cancel/$id" @{}
    Assert ((Status $id) -eq 2 -and $page.Content.Contains('Clients can cancel only Pending')) 'Client cannot cancel Approved appointment'
    $page=Submit $t '/Appointments' "/Appointments/Complete/$id" @{}
    Assert ((Status $id) -eq 2 -and $page.Content.Contains('once its scheduled time has arrived')) 'Future Approved appointment cannot be completed early'
    $edit=@{AppointmentDate=$later; AppointmentTime='12:00'; Notes='Rescheduled with client'; RowVersion=(Version $t $id); ClientId=$client2.Id; TrainerId=$trainer2.Id; Status=4; Reason='Forged reason'}
    $bad=$edit.Clone(); $bad.AppointmentDate=$now.AddDays(-1).ToString('yyyy-MM-dd')
    $page=Submit $t "/Appointments/Edit/$id" "/Appointments/Edit/$id" $bad
    Assert ($page.Content.Contains('Choose a future date and time')) 'Past reschedule rejected'
    $stale=$edit.Clone()
    $null=Submit $t "/Appointments/Edit/$id" "/Appointments/Edit/$id" $edit
    Assert ((Query 'SELECT Notes FROM Appointments WHERE Id=@id' @{id=$id}) -eq 'Rescheduled with client' -and (Status $id) -eq 2) 'Trainer updates schedule and shared notes while preserving approval'
    Assert ((Query 'SELECT COUNT(*) FROM Appointments WHERE Id=@id AND ClientId=@client AND TrainerId=@trainer AND Reason=@reason' @{id=$id; client=$client.Id; trainer=$trainer.Id; reason=$fields.Reason}) -eq 1) 'Edit cannot reassign participants or overwrite client reason'
    $stale.Notes='Stale write'
    $page=Submit $t "/Appointments/Edit/$id" "/Appointments/Edit/$id" $stale
    Assert ($page.Content.Contains('changed while you were editing') -and (Query 'SELECT Notes FROM Appointments WHERE Id=@id' @{id=$id}) -ne 'Stale write') 'Stale reschedule rejected'
    $cancelId=Book $c $trainer.Id $tomorrow '14:00' "$prefix client cancellation"
    $null=Submit $c '/Appointments' "/Appointments/Cancel/$cancelId" @{}
    Assert ((Status $cancelId) -eq 4) 'Client cancels own Pending appointment'
    $page=Submit $t '/Appointments' "/Appointments/Approve/$cancelId" @{}
    Assert ((Status $cancelId) -eq 4 -and $page.Content.Contains('Only Pending')) 'Cancelled appointment cannot be reopened by approval'
    $page=Get-Page $t "/Appointments/Edit/$cancelId"
    Assert ($page.Content.Contains('Only Pending or Approved appointments')) 'Cancelled appointment cannot be edited'
    $reused=Book $c $trainer.Id $tomorrow '14:00' "$prefix reused slot"
    Assert ((Status $reused) -eq 1) 'Cancelled slot can be booked again'
    $null=Submit $t '/Appointments' "/Appointments/Approve/$reused" @{}
    $null=Submit $t '/Appointments' "/Appointments/TrainerCancel/$reused" @{}
    Assert ((Status $reused) -eq 4) 'Trainer can cancel Approved appointment'
    $null=Submit $t2 '/Appointments' "/Appointments/TrainerCancel/$other" @{}
    Assert ((Status $other) -eq 4) 'Trainer can cancel Pending appointment'
    $earlier=Book $c $trainer.Id $tomorrow '15:00' "$prefix earlier"
    $null=Submit $t '/Appointments' "/Appointments/Approve/$earlier" @{}
    $page=Get-Page $c '/Dashboard/Client'
    Assert ($page.Content.Contains("/Appointments/Details/$earlier") -and -not $page.Content.Contains("/Appointments/Details/$id")) 'Dashboard chooses earliest upcoming Approved appointment'
    # Simulate passage of time only for this test record, to verify completion without waiting.
    $null=Query "UPDATE Appointments SET AppointmentDate=@today, AppointmentTime='00:00' WHERE Id=@id" @{today=$today; id=$earlier}
    $page=Get-Page $t '/Dashboard/Trainer'
    Assert ($page.Content -match 'data-testid="today-appointments">1<') 'Trainer dashboard today count uses appointment time zone and ownership'
    $null=Submit $t '/Appointments' "/Appointments/Complete/$earlier" @{}
    Assert ((Status $earlier) -eq 3) 'Approved appointment can be completed after scheduled time'
    $page=Submit $t '/Appointments' "/Appointments/TrainerCancel/$earlier" @{}
    Assert ((Status $earlier) -eq 3 -and $page.Content.Contains('Completed or Cancelled')) 'Completed appointment is terminal'
    $total=Query 'SELECT COUNT(*) FROM Appointments'
    $page=Get-Page $a '/Dashboard/Admin'
    Assert ($page.Content -match ('data-testid="total-appointments">'+$total+'<')) 'Admin dashboard shows total appointment count'
    $page=Get-Page $a "/Appointments/Details/$id"
    Assert ($page.Content.Contains('Admin access is read-only') -and -not $page.Content.Contains('Update / reschedule')) 'Admin details hide mutation controls'
    $page=Get-Page $c "/Appointments/Details/$id"
    Assert ($page.Content.Contains('Rescheduled with client')) 'Client sees rescheduled appointment notes'
    Write-Output "All $script:passed appointment checks passed."
}
finally {
    $null=Query @'
DELETE FROM Appointments WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM AspNetUsers WHERE Email LIKE @prefix;
'@ @{prefix=($prefix+'%')}
    $database.Dispose()
}
