param([string]$BaseUrl = 'http://localhost:5075')

# Integration test for the default development LocalDB. Temporary accounts and appointments
# are removed in finally. Past appointments are simulated only on this test's own records.
$ErrorActionPreference = 'Stop'
$prefix = 'reports-' + [Guid]::NewGuid().ToString('N')
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

function Metric([string]$html, [string]$label, $value) {
    return $html -match ('<h2 class="h6">'+[regex]::Escape($label)+'</h2><p class="h4 mb-0">'+[regex]::Escape([string]$value)+'</p>')
}
try {
    $trainer=Register 'trainer'; $trainer2=Register 'trainer2'; $client=Register 'client'; $other=Register 'other'; $empty=Register 'empty'; $admin=Register 'admin'
    foreach($account in @($trainer,$trainer2)) {
        $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name='Trainer'" @{id=$account.Id}
        Login $account
    }
    $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name IN ('Admin','Trainer')" @{id=$admin.Id}
    Login $admin
    $null=Query 'INSERT INTO TrainerClients (TrainerId,ClientId) VALUES (@trainer,@client),(@trainer,@empty),(@trainer2,@other)' @{trainer=$trainer.Id;client=$client.Id;empty=$empty.Id;trainer2=$trainer2.Id;other=$other.Id}
    $t=$trainer.Session; $t2=$trainer2.Session; $c=$client.Session; $a=$admin.Session
    $anonymous=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    Assert ((Get-Page $anonymous '/Reports').Content.Contains('name="Password"')) 'Anonymous reports require login'
    ExpectStatus 403 { Get-Page $c '/Reports' } 'Clients cannot open report selector'
    ExpectStatus 403 { Get-Page $t '/Reports/Admin' } 'Trainer cannot access Admin totals'
    ExpectStatus 403 { Get-Page $c '/Reports/Admin' } 'Client cannot access Admin totals'
    $page=Get-Page $t '/Reports'
    Assert ($page.Content.Contains($client.Email) -and -not $page.Content.Contains($other.Email)) 'Selector includes only assigned clients'
    Assert ($page.Content.Contains('>Reports</a>')) 'Trainer navigation includes Reports'
    foreach($action in @('Progress','Workout','Diet','Appointments')) {
        ExpectStatus 403 { Get-Page $c "/Reports/$($action)?clientId=$($client.Id)" } "Client denied $action report"
        ExpectStatus 403 { Get-Page $a "/Reports/$($action)?clientId=$($client.Id)" } "Admin with Trainer role denied individual $action report"
        ExpectStatus 404 { Get-Page $t2 "/Reports/$($action)?clientId=$($client.Id)" } "Other trainer denied $action report"
        ExpectStatus 404 { Get-Page $t "/Reports/$($action)?clientId=$($other.Id)" } "Forged unassigned client denied $action report"
        ExpectStatus 404 { Get-Page $t "/Reports/$action" } "Missing client denied $action report"
        $page=Get-Page $t "/Reports/$($action)?clientId=$($empty.Id)"
        Assert ($page.Content.Contains('No records available.') -and -not $page.Content.Contains('<script')) "Empty $action report renders without JavaScript"
    }
    $null=Query 'UPDATE AspNetUsers SET TargetWeight=70 WHERE Id=@id' @{id=$client.Id}
    $null=Query "INSERT INTO HealthMeasurements (ClientId,MeasurementDate,WeightKg,HeightCm,BMI,CaloriesBurned) VALUES (@id,'2026-01-01',80,200,20,100),(@id,'2026-02-01',76,200,19,200)" @{id=$client.Id}
    $workout=Query "INSERT INTO WorkoutPlans (ClientId,TrainerId,PlanName,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,'Report workout','2026-01-01','2026-12-31',1,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer.Id}
    $foreignWorkout=Query "INSERT INTO WorkoutPlans (ClientId,TrainerId,PlanName,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,'SECRET WORKOUT','2026-01-01','2026-12-31',1,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer2.Id}
    $exercise=Query "INSERT INTO WorkoutExercises (WorkoutPlanId,ExerciseName,Sets,Repetitions,DurationMinutes,CaloriesTarget) OUTPUT INSERTED.Id VALUES (@plan,'Report walking',2,5,30,120)" @{plan=$workout}
    $null=Query "INSERT INTO WorkoutLogs (ClientId,WorkoutPlanId,WorkoutExerciseId,WorkoutDate,DurationMinutes,CaloriesBurned,Completed) VALUES (@client,@plan,@exercise,'2026-01-10',30,120,1),(@client,@plan,NULL,'2026-01-11',10,30,0),(@client,@foreign,NULL,'2026-01-12',10,900,1)" @{client=$client.Id;plan=$workout;exercise=$exercise;foreign=$foreignWorkout}
    $diet=Query "INSERT INTO DietPlans (ClientId,TrainerId,PlanName,DailyCalories,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,'Report diet',2100,'2026-01-01','2026-12-31',2,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer.Id}
    $foreignDiet=Query "INSERT INTO DietPlans (ClientId,TrainerId,PlanName,DailyCalories,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,'SECRET DIET',2100,'2026-01-01','2026-12-31',1,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer2.Id}
    $null=Query "INSERT INTO DietMeals (DietPlanId,MealType,FoodName,Quantity,Calories,Protein,Carbohydrates,Fat) VALUES (@plan,1,'Report oats','1 cup',300,10,40,5)" @{plan=$diet}
    $null=Query "INSERT INTO MealLogs (ClientId,DietPlanId,MealDate,MealType,FoodName,Quantity,Calories) VALUES (@client,@plan,'2026-01-10',1,'Report breakfast','1 cup',310),(@client,@foreign,'2026-01-10',1,'SECRET FOOD','1 cup',999)" @{client=$client.Id;plan=$diet;foreign=$foreignDiet}
    foreach($status in 1..4) {
        $null=Query "INSERT INTO Appointments (ClientId,TrainerId,AppointmentDate,AppointmentTime,Reason,Status,CreatedAt) VALUES (@client,@trainer,'2026-01-10',@time,'Report appointment',@status,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer.Id;status=$status;time="0${status}:00"}
    }
    $null=Query "INSERT INTO Appointments (ClientId,TrainerId,AppointmentDate,AppointmentTime,Reason,Status,CreatedAt) VALUES (@client,@trainer,'2026-01-11','10:00','SECRET APPOINTMENT',1,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer2.Id}
    $page=Get-Page $t "/Reports/Progress?clientId=$($client.Id)"
    Assert ($page.Headers['Cache-Control'] -match 'no-store') 'Sensitive reports disallow caching'
    foreach($entry in @(@('Current weight (kg)',76),@('Target weight (kg)',70),@('Latest BMI',19),@('Measurements',2),@('Weight change, latest minus first (kg)',-4),@('Workout logs',2),@('Completed workout logs',1),@('Incomplete workout logs',1),@('Workout calories burned (kcal)',150),@('Measurement calories burned (kcal)',300))) {
        Assert (Metric $page.Content $entry[0] $entry[1]) "Progress calculates $($entry[0]) correctly"
    }
    Assert ($page.Content.Contains('2026-01-01') -and $page.Content.Contains('2026-02-01')) 'Weight history includes recorded dates'
    $page=Get-Page $t "/Reports/Workout?clientId=$($client.Id)"
    Assert ($page.Content.Contains('Report workout') -and $page.Content.Contains('Report walking') -and $page.Content.Contains('2026-01-10')) 'Workout report includes plans exercises and completion dates'
    Assert ((Metric $page.Content 'Calories burned (kcal)' 150) -and -not $page.Content.Contains('SECRET WORKOUT')) 'Workout data and calorie totals exclude other trainer plans'
    $page=Get-Page $t "/Reports/Diet?clientId=$($client.Id)"
    Assert ($page.Content.Contains('Report diet') -and $page.Content.Contains('Report oats') -and $page.Content.Contains('Report breakfast') -and $page.Content.Contains('Inactive')) 'Diet includes inactive plans meals and meal logs'
    Assert ((Metric $page.Content 'Logged calories (kcal)' 310) -and -not $page.Content.Contains('SECRET')) 'Diet report excludes other trainer data'
    $page=Get-Page $t "/Reports/Appointments?clientId=$($client.Id)"
    foreach($status in @('Pending','Approved','Completed','Cancelled')) { Assert (Metric $page.Content $status 1) "$status count scoped to trainer and client" }
    Assert (-not $page.Content.Contains('SECRET APPOINTMENT')) 'Appointment report excludes other trainers'
    $page=Get-Page $a '/Reports'
    Assert ($page.Content.Contains('Admin Report')) 'Admin navigation opens aggregate report'
    Assert ($page.Content.Contains('href="/AdminReports/Print"') -and $page.Content.Contains('Printable Report')) 'Admin report links to dedicated printable route'
    Assert ($page.Content.Contains('href="/Dashboard/Admin"') -and $page.Content.Contains('Back to Dashboard')) 'Admin report provides dashboard action'
    Assert ($page.Content.Contains('Open the printable report and use Ctrl+P to print or save as PDF.')) 'Admin report explains printable workflow'
    Assert (-not ($page.Content -match '<a[^>]*class="btn[^>]*>Reports</a>')) 'Redundant in-page Reports button removed'
    foreach($entry in @(@('Total Users','SELECT COUNT(*) FROM AspNetUsers'),@('Active Users','SELECT COUNT(*) FROM AspNetUsers WHERE IsActive=1'),@('Total Appointments','SELECT COUNT(*) FROM Appointments'),@('Total Workout Plans','SELECT COUNT(*) FROM WorkoutPlans'),@('Total Diet Plans','SELECT COUNT(*) FROM DietPlans'))) {
        Assert (Metric $page.Content $entry[0] (Query $entry[1])) "Admin $($entry[0]) matches SQL Server"
    }
    foreach($role in @('Client','Trainer')) {
        $count=Query 'SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name=@role' @{role=$role}
        Assert (Metric $page.Content "Total ${role}s" $count) "Admin $role count matches role membership"
    }
    $statuses=@('Pending','Approved','Completed','Cancelled')
    foreach($status in 1..4) {
        $count=Query 'SELECT COUNT(*) FROM Appointments WHERE Status=@status' @{status=$status}
        Assert ($page.Content -match "<td>$($statuses[$status-1])</td>\s*<td>$count</td>") "Admin $($statuses[$status-1]) total matches SQL Server"
    }
    Assert (-not $page.Content.Contains('SECRET FOOD') -and -not $page.Content.Contains('SECRET APPOINTMENT') -and -not $page.Content.Contains('Report breakfast')) 'Admin report exposes aggregates only'
    Assert ($page.Content.Contains('reports.css') -and $page.Content.Contains('Ctrl+P')) 'Report loads print stylesheet and browser print instructions'
    $css=Get-Page $a '/css/reports.css'
    Assert ($css.Content.Contains('@media print') -and $css.Content.Contains('table-header-group')) 'CSS supports printing with repeated table headers'
    Assert ((Get-Page $anonymous '/AdminReports/Print').Content.Contains('name="Password"')) 'Anonymous printable route requires login'
    ExpectStatus 403 { Get-Page $t '/AdminReports/Print' } 'Trainer cannot access printable Admin report'
    ExpectStatus 403 { Get-Page $c '/AdminReports/Print' } 'Client cannot access printable Admin report'
    $print=Get-Page $a '/AdminReports/Print'
    Assert ($print.Headers['Cache-Control'] -match 'no-store') 'Printable report disallows caching'
    Assert (-not ($print.Content -match '<(nav|footer|button|script)\b')) 'Printable report excludes navigation footer buttons and JavaScript'
    foreach($entry in @(@('Total Users','SELECT COUNT(*) FROM AspNetUsers'),@('Total Clients',"SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Client'"),@('Total Trainers',"SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Trainer'"),@('Active Users','SELECT COUNT(*) FROM AspNetUsers WHERE IsActive=1'),@('Total Appointments','SELECT COUNT(*) FROM Appointments'),@('Total Workout Plans','SELECT COUNT(*) FROM WorkoutPlans'),@('Total Diet Plans','SELECT COUNT(*) FROM DietPlans'))) {
        $count=Query $entry[1]
        Assert ($print.Content -match ('<th scope="row">'+[regex]::Escape($entry[0])+'</th>\s*<td>'+ $count+'</td>')) "Printable $($entry[0]) matches SQL Server"
    }
    foreach($status in 1..4) {
        $count=Query 'SELECT COUNT(*) FROM Appointments WHERE Status=@status' @{status=$status}
        Assert ($print.Content -match "<th scope=`"row`">$($statuses[$status-1]) Appointments</th>\s*<td>$count</td>") "Printable $($statuses[$status-1]) count matches SQL Server"
    }
    $printCss=Get-Page $a '/css/admin-report-print.css'
    Assert ($printCss.Content.Contains('A4 portrait') -and $printCss.Content.Contains('@media print')) 'Dedicated printable CSS uses A4 formatting'
    $null=Query 'UPDATE AspNetUsers SET IsActive=0 WHERE Id=@id' @{id=$client.Id}
    ExpectStatus 404 { Get-Page $t "/Reports/Progress?clientId=$($client.Id)" } 'Inactive client reports denied'
    $null=Query 'UPDATE AspNetUsers SET IsActive=1 WHERE Id=@id; DELETE FROM TrainerClients WHERE ClientId=@id' @{id=$client.Id}
    foreach($action in @('Progress','Workout','Diet','Appointments')) {
        ExpectStatus 404 { Get-Page $t "/Reports/$($action)?clientId=$($client.Id)" } "Revocation immediately blocks $action report"
    }
    Write-Output "All $script:passed report checks passed."
}
finally {
    $null=Query @'
DELETE FROM Appointments WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM HealthMeasurements WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM WorkoutLogs WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM MealLogs WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM WorkoutExercises WHERE WorkoutPlanId IN (SELECT Id FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix));
DELETE FROM DietMeals WHERE DietPlanId IN (SELECT Id FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix));
DELETE FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM TrainerClients WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM AspNetUsers WHERE Email LIKE @prefix;
'@ @{prefix=($prefix+'%')}
    $database.Dispose()
}


