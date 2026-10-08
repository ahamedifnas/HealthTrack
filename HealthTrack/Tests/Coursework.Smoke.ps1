param([string]$BaseUrl = 'http://localhost:5075')

# Integration test for the default development LocalDB. Temporary accounts and appointments
# are removed in finally. Past appointments are simulated only on this test's own records.
$ErrorActionPreference = 'Stop'
$prefix = 'coursework-' + [Guid]::NewGuid().ToString('N')
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
    $admin=Register 'Admin'; $trainer=Register 'Trainer'; $client=Register 'Client'; $other=Register 'Other'; $trainer2=Register 'Trainer2'
    foreach($entry in @(@($admin,'Admin'),@($trainer,'Trainer'),@($trainer2,'Trainer'))) {
        $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name=@role' @{id=$entry[0].Id;role=$entry[1]}
        Login $entry[0]
    }
    $a=$admin.Session; $c=$client.Session; $t=$trainer.Session; $o=$other.Session
    foreach($name in @('Doctor','Doctor2')) {
        $email="$prefix-$name@example.test"
        $page=Submit $a '/AdminHealthcareProfessionals/Create' '/AdminHealthcareProfessionals/Create' @{FullName="$prefix $name"; Email=$email; Specialty='Sports medicine'; Password=$password; ConfirmPassword=$password}
        $id=Query 'SELECT UserId FROM HealthcareProfessionals h JOIN AspNetUsers u ON u.Id=h.UserId WHERE u.Email=@email' @{email=$email}
        Assert ([bool]$id) 'Admin creates healthcare Identity account and specialty profile'
        $account=@{Id=$id; Email=$email}; Login $account
        if($name -eq 'Doctor') { $doctor=$account } else { $doctor2=$account }
    }
    $h=$doctor.Session; $h2=$doctor2.Session
    $null=Query "INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name='Client'" @{id=$doctor.Id}
    ExpectStatus 403 { Get-Page $h '/Measurements' } 'Mixed healthcare/client role cannot use client measurements'
    ExpectStatus 403 { Get-Page $h '/Progress' } 'Mixed healthcare/client role cannot use client progress'
    $null=Query "DELETE ur FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=@id AND r.Name='Client'" @{id=$doctor.Id}
    $null=Query "INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name='Admin'" @{id=$doctor2.Id}
    $page=Submit $h2 "/AdminHealthcareProfessionals/Edit/$($doctor2.Id)" "/AdminHealthcareProfessionals/Edit/$($doctor2.Id)" @{FullName="$prefix Doctor2";Email=$doctor2.Email;Specialty='Sports medicine';IsActive='false'}
    Assert ($page.Content.Contains('Your own account') -and (Query 'SELECT IsActive FROM AspNetUsers WHERE Id=@id' @{id=$doctor2.Id})) 'Healthcare admin edit preserves self-deactivation protection for mixed roles'
    $null=Query "DELETE ur FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=@id AND r.Name='Admin'" @{id=$doctor2.Id}
    Assert ((Get-Page $h '/Dashboard').Content.Contains('Appointments')) 'Healthcare login routes to own appointments'
    foreach($path in @('/AdminHealthcareProfessionals','/Measurements','/TrainerWorkouts','/Reports','/Progress')) {
        ExpectStatus 403 { Get-Page $h $path } "Healthcare role denied $path"
    }
    ExpectStatus 403 { Get-Page $c '/AdminHealthcareProfessionals' } 'Client cannot manage providers'
    $page=Get-Page $c '/Appointments/Book'
    Assert ($page.Content.Contains($doctor.Id) -and $page.Content.Contains($trainer.Id)) 'Booking offers both provider categories'
    $fields=@{HealthcareProfessionalId=$doctor.Id; AppointmentDate=$tomorrow; AppointmentTime='10:00'; Reason="$prefix consultation"}
    $both=$fields.Clone(); $both.TrainerId=$trainer.Id
    $page=Submit $c '/Appointments/Book' '/Appointments/Book' $both
    Assert ($page.Content.Contains('Choose exactly one provider') -and (Query 'SELECT COUNT(*) FROM Appointments WHERE Reason=@r' @{r=$fields.Reason}) -eq 0) 'Both providers rejected without writing'
    $none=$fields.Clone(); $none.Remove('HealthcareProfessionalId')
    Assert ((Submit $c '/Appointments/Book' '/Appointments/Book' $none).Content.Contains('Choose exactly one provider')) 'Missing provider rejected'
    $invalid=$fields.Clone(); $invalid.HealthcareProfessionalId=$client.Id
    Assert ((Submit $c '/Appointments/Book' '/Appointments/Book' $invalid).Content.Contains('Choose an active healthcare professional')) 'Unauthorized provider ID rejected'
    $past=$fields.Clone(); $past.AppointmentDate=$now.AddDays(-1).ToString('yyyy-MM-dd')
    Assert ((Submit $c '/Appointments/Book' '/Appointments/Book' $past).Content.Contains('Choose a future')) 'Past consultation rejected'
    $null=Submit $c '/Appointments/Book' '/Appointments/Book' $fields
    $id=Query 'SELECT Id FROM Appointments WHERE Reason=@r' @{r=$fields.Reason}
    Assert ([bool]$id -and (Query 'SELECT COUNT(*) FROM Appointments WHERE Id=@id AND TrainerId IS NULL AND HealthcareProfessionalId=@h' @{id=$id;h=$doctor.Id}) -eq 1) 'Healthcare booking persists exactly one provider'
    Assert ((Get-Page $a '/Admin/Appointments').Content.Contains("$prefix Doctor")) 'Admin appointment overview renders healthcare name'
    Assert ((Get-Page $h '/Notifications').Content.Contains('Appointment requested')) 'Healthcare receives booking event notification'
    foreach($session in @($h2,$t,$o)) {
        ExpectStatus 404 { Get-Page $session "/Appointments/Details/$id" } 'Other account cannot read healthcare consultation'
    }
    ExpectStatus 404 { Submit $h2 '/Appointments' "/Appointments/Approve/$id" @{} } 'Other provider cannot approve consultation'
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Appointments/Approve/$id" -WebSession $h -Method Post } 'Provider POST requires antiforgery'
    $conflict=$fields.Clone(); $conflict.Reason="$prefix provider conflict"
    $page=Submit $o '/Appointments/Book' '/Appointments/Book' $conflict
    Assert ($page.Content.Contains('already has a Pending/Approved appointment')) 'Healthcare exact-slot conflict rejected'
    $conflict=$fields.Clone(); $conflict.HealthcareProfessionalId=$doctor2.Id; $conflict.Reason="$prefix client conflict"
    Assert ((Submit $c '/Appointments/Book' '/Appointments/Book' $conflict).Content.Contains('already has a Pending/Approved appointment')) 'Client conflict across providers rejected'
    foreach($account in @($client,$doctor)) {
        Assert ((Query "SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND Title='Appointment Reminder' AND Message LIKE '%tomorrow%'" @{id=$account.Id}) -eq 1) 'Tomorrow reminder delivered to appointment participant'
    }
    $null=Get-Page $c '/Dashboard'; $null=Get-Page $h '/Appointments'; $null=Get-Page $c '/Notifications'
    Assert ((Query "SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND Title='Appointment Reminder'" @{id=$client.Id}) -eq 1) 'Repeated requests do not duplicate appointment reminders'
    $notice=Query "SELECT Id FROM Notifications WHERE UserId=@id AND Title='Appointment Reminder'" @{id=$client.Id}
    $null=Submit $c '/Notifications' "/Notifications/Delete/$notice" @{}
    $null=Get-Page $c '/Notifications'
    Assert ((Query "SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND Title='Appointment Reminder'" @{id=$client.Id}) -eq 0) 'Deleted reminder is not regenerated'
    $request=$fields.Clone(); $request.AppointmentTime='10:30'; $request.RowVersion=Hidden (Get-Page $c "/Appointments/EditRequest/$id").Content 'RowVersion'
    $null=Submit $c "/Appointments/EditRequest/$id" "/Appointments/EditRequest/$id" $request
    Assert ((Query "SELECT COUNT(*) FROM Appointments WHERE Id=@id AND AppointmentTime='10:30' AND HealthcareProfessionalId=@h" @{id=$id;h=$doctor.Id}) -eq 1) 'Client edits a future healthcare request while preserving provider'
    # Restore the initial slot so subsequent conflict and reminder checks refer to 10:00.
    $request.AppointmentTime='10:00'; $request.RowVersion=Hidden (Get-Page $c "/Appointments/EditRequest/$id").Content 'RowVersion'
    $null=Submit $c "/Appointments/EditRequest/$id" "/Appointments/EditRequest/$id" $request
    $null=Submit $h '/Appointments' "/Appointments/Approve/$id" @{}
    Assert ((Status $id) -eq 2) 'Healthcare provider approves own appointment'
    $edit=@{AppointmentDate=$later; AppointmentTime='11:00'; Notes='Consultation rescheduled'; RowVersion=(Version $h $id)}
    $null=Submit $h "/Appointments/Edit/$id" "/Appointments/Edit/$id" $edit
    Assert ((Query 'SELECT CONVERT(varchar,AppointmentDate,23) FROM Appointments WHERE Id=@id' @{id=$id}) -eq $later) 'Healthcare provider reschedules with concurrency token'
    $null=Query "UPDATE Appointments SET AppointmentDate=@today,AppointmentTime='23:59' WHERE Id=@id" @{today=$today;id=$id}
    $null=Get-Page $h '/Appointments'
    Assert ((Query "SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND Title='Appointment Reminder' AND Message LIKE '%today%'" @{id=$client.Id}) -eq 1) 'Appointment-day reminder generated'
    $null=Query "UPDATE Appointments SET AppointmentTime='00:00' WHERE Id=@id" @{id=$id}
    $null=Submit $h '/Appointments' "/Appointments/Complete/$id" @{}
    Assert ((Status $id) -eq 3) 'Healthcare provider completes appointment after scheduled time'
    $fields.AppointmentTime='12:00'; $fields.Reason="$prefix cancel"
    $null=Submit $c '/Appointments/Book' '/Appointments/Book' $fields
    $cancel=Query 'SELECT Id FROM Appointments WHERE Reason=@r' @{r=$fields.Reason}
    $null=Submit $h '/Appointments' "/Appointments/TrainerCancel/$cancel" @{}
    Assert ((Status $cancel) -eq 4) 'Healthcare provider cancels own appointment'
    $before=Query 'SELECT COUNT(*) FROM ReminderDeliveries WHERE UserId=@id' @{id=$client.Id}
    $null=Query "UPDATE Appointments SET AppointmentDate=@today,AppointmentTime='23:58' WHERE Id IN (@id,@cancel)" @{today=$today;id=$id;cancel=$cancel}
    $null=Get-Page $c '/Notifications'
    Assert ((Query 'SELECT COUNT(*) FROM ReminderDeliveries WHERE UserId=@id' @{id=$client.Id}) -eq $before) 'Cancelled and completed appointments generate no new reminders'
    # Exercise database invariant independently of MVC validation.
    foreach($sql in @('UPDATE Appointments SET TrainerId=@t WHERE Id=@id','UPDATE Appointments SET HealthcareProfessionalId=NULL WHERE Id=@id')) {
        $rejected=$false
        try { $null=Query $sql @{t=$trainer.Id;id=$id} } catch { if($_.Exception.InnerException.Number -eq 547 -or $_.Exception.Message -match 'CHECK constraint') { $rejected=$true } else { throw } }
        Assert $rejected 'Database rejects invalid provider combination'
    }
    $null=Submit $a "/AdminHealthcareProfessionals/Edit/$($doctor.Id)" "/AdminHealthcareProfessionals/Edit/$($doctor.Id)" @{FullName="$prefix Doctor";Email=$doctor.Email;Specialty='Rehabilitation';IsActive='false'}
    Assert (-not (Get-Page $c '/Appointments/Book').Content.Contains($doctor.Id)) 'Inactive healthcare provider omitted from picker'
    Assert ((Get-Page $h '/Appointments').Content.Contains('name="Password"')) 'Deactivation invalidates existing healthcare session'
    $fields.Reason="$prefix inactive"
    Assert ((Submit $c '/Appointments/Book' '/Appointments/Book' $fields).Content.Contains('Choose an active healthcare professional')) 'Forged inactive provider selection rejected'
    $null=Query 'INSERT INTO TrainerClients (TrainerId,ClientId,AssignedAt) VALUES (@t,@c,GETUTCDATE()),(@t2,@o,GETUTCDATE())' @{t=$trainer.Id;c=$client.Id;t2=$trainer2.Id;o=$other.Id}
    $plan=@{ClientId=$client.Id;PlanName="$prefix popular workout";StartDate=$today;EndDate=$later;Description='Coursework fixture'}
    $null=Submit $t '/TrainerWorkouts/Create' '/TrainerWorkouts/Create' $plan
    $w=Query 'SELECT Id FROM WorkoutPlans WHERE ClientId=@id' @{id=$client.Id}
    $plan.PlanName="$prefix popular diet"; $plan.DailyCalories=2000
    $null=Submit $t '/TrainerDiets/Create' '/TrainerDiets/Create' $plan
    $d=Query 'SELECT Id FROM DietPlans WHERE ClientId=@id' @{id=$client.Id}
    Add-Type -AssemblyName System.Net.Http
    $handler=New-Object System.Net.Http.HttpClientHandler
    $handler.CookieContainer=$c.Cookies
    $http=New-Object System.Net.Http.HttpClient($handler)
    try {
        $tasks=@(1..4 | ForEach-Object { $http.GetAsync("$BaseUrl/Dashboard/Client") })
        foreach($task in $tasks) { $response=$task.GetAwaiter().GetResult(); Assert $response.IsSuccessStatusCode 'Concurrent reminder request succeeds'; $response.Dispose() }
    } finally { $http.Dispose(); $handler.Dispose() }
    $null=Get-Page $c '/Dashboard'; $null=Get-Page $c '/Notifications'; $null=Get-Page $c '/Dashboard'
    foreach($title in @('Workout Reminder','Meal Reminder')) {
        Assert ((Query 'SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND Title=@title' @{id=$client.Id;title=$title}) -eq 1) "$title generated once per day"
    }
    $null=Submit $c "/Workouts/Log?planId=$w" '/Workouts/Log' @{WorkoutPlanId=$w;WorkoutDate=$today;DurationMinutes=20;CaloriesBurned=100;Completed='true'}
    $null=Submit $c '/Diets/Log' '/Diets/Log' @{DietPlanId=$d;MealDate=$today;MealType=1;FoodName='Fixture meal';Quantity='1 cup';Calories=200}
    $plan.ClientId=$other.Id; $plan.PlanName="$prefix hidden diet"
    $null=Submit $trainer2.Session '/TrainerDiets/Create' '/TrainerDiets/Create' $plan
    $null=Query 'UPDATE DietPlans SET Status=2 WHERE ClientId=@id' @{id=$other.Id}
    $null=Get-Page $o '/Notifications'
    Assert ((Query "SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND Title='Meal Reminder'" @{id=$other.Id}) -eq 0) 'Inactive plan does not generate a meal reminder'
    $null=Query 'UPDATE DietPlans SET Status=1,StartDate=@start WHERE ClientId=@id' @{id=$other.Id;start=$tomorrow}
    $null=Get-Page $o '/Notifications'
    Assert ((Query "SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND Title='Meal Reminder'" @{id=$other.Id}) -eq 0) 'Future active plan does not generate a meal reminder'
    $page=Get-Page $t '/Reports/Analytics'
    Assert ($page.Content.Contains("$prefix popular workout") -and $page.Content.Contains("$prefix popular diet") -and -not $page.Content.Contains("$prefix hidden diet")) 'Trainer popularity analytics enforce assignment scope'
    Assert ($page.Content -match 'Workout Logs This Month</h2><p class="h4 mb-0">1<' -and $page.Content -match 'Meal Logs This Month</h2><p class="h4 mb-0">1<') 'Monthly engagement counts match fixture logs'
    Assert ($page.Content.Contains('Most Active Clients') -and $page.Content.Contains("$prefix Client")) 'Most active clients includes fixture activity'
    $page=Get-Page $a '/Reports/Admin'
    Assert ($page.Content.Contains("$prefix hidden diet") -and $page.Content.Contains('Appointment Completion Rate')) 'Admin analytics include system-wide plans and engagement'
    Assert ((Get-Page $a '/AdminReports/Print').Content.Contains('Popular workout routines')) 'Printable admin report includes analytics'
    ExpectStatus 403 { Get-Page $c '/Reports/Analytics' } 'Client cannot access trainer analytics'
    $null=Query 'DELETE FROM TrainerClients WHERE ClientId=@id' @{id=$client.Id}
    Assert (-not (Get-Page $t '/Reports/Analytics').Content.Contains("$prefix popular workout")) 'Assignment revocation immediately removes trainer analytics data'
    Write-Output "All $script:passed coursework checks passed."
}
finally {
    $null=Query @"
DELETE FROM Appointments WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM WorkoutLogs WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM MealLogs WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM WorkoutExercises WHERE WorkoutPlanId IN (SELECT Id FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix));
DELETE FROM DietMeals WHERE DietPlanId IN (SELECT Id FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix));
DELETE FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM TrainerClients WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM HealthcareProfessionals WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM AspNetUsers WHERE Email LIKE @prefix;
"@ @{prefix=($prefix+'%')}
    $database.Dispose()
}
