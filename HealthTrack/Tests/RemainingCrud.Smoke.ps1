param([string]$BaseUrl = 'http://localhost:5075')

# Integration test for the default development LocalDB. Temporary accounts and appointments
# are removed in finally. Past appointments are simulated only on this test's own records.
$ErrorActionPreference = 'Stop'
$prefix = 'remainingcrud-' + [Guid]::NewGuid().ToString('N')
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
    $admin=Register 'Admin'; $trainer=Register 'Trainer'; $trainer2=Register 'Trainer2'; $other=Register 'Other'
    foreach($entry in @(@($admin,'Admin'),@($trainer,'Trainer'),@($trainer2,'Trainer'))) {
        $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name=@role' @{id=$entry[0].Id;role=$entry[1]}
        Login $entry[0]
    }
    $a=$admin.Session; $t=$trainer.Session
    $email="$prefix-managed@example.test"
    $null=Submit $a '/Admin/CreateClient' '/Admin/CreateClient' @{FullName="$prefix managed";Email=$email;Password=$password;ConfirmPassword=$password}
    $id=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}
    Assert ([bool]$id) 'Admin creates client through MVC'
    $client=@{Id=$id;Email=$email}; Login $client; $c=$client.Session
    Assert ((Get-Page $a "/Admin/Details/$id").Content.Contains($email)) 'Admin reads client details'
    $null=Submit $a "/Admin/Edit/$id" "/Admin/Edit/$id" @{FullName="$prefix edited";Email=$email;IsActive='true';TargetWeight=65}
    Assert ((Query 'SELECT TargetWeight FROM AspNetUsers WHERE Id=@id' @{id=$id}) -eq 65) 'Admin edits client profile'
    ExpectStatus 403 { Submit $c '/Measurements' "/Admin/Edit/$id" @{FullName='forged';Email=$email;IsActive='true'} } 'Client denied admin account mutation'
    $null=Submit $a '/Dashboard/Admin' '/AdminAssignments/Assign' @{'Assignment.ClientId'=$id;'Assignment.TrainerId'=$trainer.Id}
    Assert ((Query 'SELECT TrainerId FROM TrainerClients WHERE ClientId=@id' @{id=$id}) -eq $trainer.Id) 'Admin creates assignment'
    $plan=@{ClientId=$id;PlanName="$prefix workout";StartDate=$today;EndDate=$later}
    $null=Submit $t '/TrainerWorkouts/Create' '/TrainerWorkouts/Create' $plan
    $w=Query 'SELECT Id FROM WorkoutPlans WHERE ClientId=@id' @{id=$id}
    $plan.PlanName="$prefix edited workout"; $plan.Status=1
    $null=Submit $t "/TrainerWorkouts/Edit/$w" "/TrainerWorkouts/Edit/$w" $plan
    Assert ((Query 'SELECT PlanName FROM WorkoutPlans WHERE Id=@id' @{id=$w}) -eq $plan.PlanName) 'Workout plan edit persists'
    $plan.PlanName="$prefix diet"; $plan.DailyCalories=2100
    $null=Submit $t '/TrainerDiets/Create' '/TrainerDiets/Create' $plan
    $d=Query 'SELECT Id FROM DietPlans WHERE ClientId=@id' @{id=$id}
    $plan.DailyCalories=2200
    $null=Submit $t "/TrainerDiets/Edit/$d" "/TrainerDiets/Edit/$d" $plan
    Assert ((Query 'SELECT DailyCalories FROM DietPlans WHERE Id=@id' @{id=$d}) -eq 2200) 'Diet plan edit persists'
    # Expired and future Active plans do not count as currently active.
    $null=Query 'UPDATE WorkoutPlans SET StartDate=@past,EndDate=@past WHERE Id=@w; UPDATE DietPlans SET StartDate=@future WHERE Id=@d' @{past=$now.AddDays(-1).ToString('yyyy-MM-dd');future=$tomorrow;w=$w;d=$d}
    $html=(Get-Page $t '/Dashboard/Trainer').Content
    Assert ($html -match 'Active workout plans</span><strong>0<' -and $html -match 'Active diet plans</span><strong>0<') 'Trainer dashboard excludes expired and future plans'
    $html=(Get-Page $t '/Reports/Analytics').Content
    foreach($label in @('Active Workout Plans','Active Diet Plans','Workout Logs This Month','Meal Logs This Month','Completed Appointments','Cancelled Appointments')) {
        Assert ($html -match ([regex]::Escape($label)+'</h2><p class="h4 mb-0">0<')) "Trainer analytics zero state: $label"
    }
    Assert ($html.Contains('0% (no appointments)')) 'Completion rate handles empty denominator'
    $null=Query 'UPDATE WorkoutPlans SET StartDate=@date,EndDate=@end WHERE Id=@w; UPDATE DietPlans SET StartDate=@date WHERE Id=@d' @{date=$today;end=$later;w=$w;d=$d}
    $null=Submit $c "/Workouts/Log?planId=$w" '/Workouts/Log' @{WorkoutPlanId=$w;WorkoutDate=$today;DurationMinutes=20;CaloriesBurned=100}
    $null=Submit $c "/Diets/Log?planId=$d" '/Diets/Log' @{DietPlanId=$d;MealDate=$today;MealType=1;FoodName='Fixture';Quantity='1 cup';Calories=200}
    $null=Query "INSERT INTO Appointments(ClientId,TrainerId,AppointmentDate,AppointmentTime,Reason,Status,CreatedAt) VALUES(@c,@t,@date,'08:00',@reason,3,GETUTCDATE()),(@c,@t,@date,'09:00',@reason,4,GETUTCDATE())" @{c=$id;t=$trainer.Id;date=$today;reason=$prefix}
    $html=(Get-Page $t '/Reports/Analytics').Content
    foreach($label in @('Active Clients','Active Workout Plans','Active Diet Plans','Workout Logs This Month','Meal Logs This Month','Completed Appointments','Cancelled Appointments')) {
        Assert ($html -match ([regex]::Escape($label)+'</h2><p class="h4 mb-0">1<')) "Trainer analytics matches fixture: $label"
    }
    Assert ($html -match 'Appointment Completion Rate</h2><p class="h4 mb-0">50%<') 'Completion rate is completed divided by all appointments'
    Assert ($html -match 'Most Active Clients' -and $html.Contains("$prefix edited")) 'Most active ranking uses recorded activity'
    $html=(Get-Page $a '/Reports/Admin').Content
    $month=$now.ToString('yyyy-MM-01')
    $parameters=@{today=$today;month=$month}
    foreach($entry in @(
        @('Active Clients',"SELECT COUNT(*) FROM AspNetUsers u WHERE IsActive=1 AND EXISTS(SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=u.Id AND r.Name='Client')"),
        @('Active Workout Plans','SELECT COUNT(*) FROM WorkoutPlans WHERE Status=1 AND StartDate<=@today AND EndDate>=@today'),
        @('Active Diet Plans','SELECT COUNT(*) FROM DietPlans WHERE Status=1 AND StartDate<=@today AND EndDate>=@today'),
        @('Completed Appointments','SELECT COUNT(*) FROM Appointments WHERE Status=3'),
        @('Cancelled Appointments','SELECT COUNT(*) FROM Appointments WHERE Status=4'),
        @('Workout Logs This Month',"SELECT COUNT(*) FROM WorkoutLogs l WHERE WorkoutDate>=@month AND WorkoutDate<=@today AND EXISTS(SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=l.ClientId AND r.Name='Client')"),
        @('Meal Logs This Month',"SELECT COUNT(*) FROM MealLogs l WHERE MealDate>=@month AND MealDate<=@today AND EXISTS(SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId=l.ClientId AND r.Name='Client')")
    )) {
        $expected=Query $entry[1] $parameters
        Assert ($html -match ([regex]::Escape($entry[0])+'</h2><p class="h4 mb-0">'+$expected+'<')) "Admin analytics equals SQL: $($entry[0])"
    }
    $null=Submit $a "/AdminAssignments/Reassign/$id" "/AdminAssignments/Reassign/$id" @{ClientId=$id;TrainerId=$trainer2.Id;expectedTrainerId=$trainer.Id}
    Assert ((Query 'SELECT TrainerId FROM TrainerClients WHERE ClientId=@id' @{id=$id}) -eq $trainer2.Id) 'Admin reassigns client'
    Assert ((Query 'SELECT TrainerId FROM WorkoutPlans WHERE Id=@id' @{id=$w}) -eq $trainer.Id) 'Reassignment preserves historical plan owner'
    ExpectStatus 404 { Get-Page $t "/TrainerWorkouts/Details/$w" } 'Reassignment revokes former trainer access'
    $null=Get-Page $a "/AdminAssignments/Unassign/$id"
    Assert ((Query 'SELECT COUNT(*) FROM TrainerClients WHERE ClientId=@id' @{id=$id}) -eq 1) 'Unassignment GET is confirmation only'
    $null=Submit $a "/AdminAssignments/Unassign/$id" "/AdminAssignments/Unassign/$id" @{}
    Assert ((Query 'SELECT COUNT(*) FROM TrainerClients WHERE ClientId=@id' @{id=$id}) -eq 0 -and (Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id' @{id=$id}) -eq 1) 'Unassignment preserves history'
    $null=Submit $a "/Admin/Delete/$id" "/Admin/Delete/$id" @{}
    Assert (-not (Query 'SELECT IsActive FROM AspNetUsers WHERE Id=@id' @{id=$id}) -and (Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id' @{id=$id}) -eq 1) 'Client removal deactivates account with history'
    Write-Output "All $script:passed remaining CRUD and analytics checks passed."
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
DELETE FROM AspNetUsers WHERE Email LIKE @prefix;
"@ @{prefix=($prefix+'%')}
    $database.Dispose()
}
