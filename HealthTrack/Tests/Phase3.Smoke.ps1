param([string]$BaseUrl = 'http://localhost:5075')

# Integration checks against the default LocalDB development database and running app.
# All accounts and records created here are uniquely named and removed in finally.
$ErrorActionPreference = 'Stop'
$prefix = 'phase3-' + [Guid]::NewGuid().ToString('N')
$password = 'Ht3!' + [Guid]::NewGuid().ToString('N')
$database = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$database.Open()

function Query([string]$sql, [hashtable]$parameters = @{}) {
    $command = $database.CreateCommand()
    $command.CommandText = $sql
    foreach ($key in $parameters.Keys) { [void]$command.Parameters.AddWithValue('@' + $key, $parameters[$key]) }
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    Write-Output "PASS: $message"
}
function Get-Page($session, [string]$path) {
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -WebSession $session
}
function Token([string]$html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw 'Antiforgery token missing' }
    return $match.Groups[1].Value
}
function Submit($session, [string]$getPath, [string]$postPath, [hashtable]$fields) {
    $page = Get-Page $session $getPath
    $fields.__RequestVerificationToken = Token $page.Content
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $postPath) -WebSession $session -Method Post -Body $fields
}
function ExpectStatus([int]$status, [scriptblock]$action, [string]$message) {
    try { $null = & $action; throw "Expected HTTP $status : $message" }
    catch {
        if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $status) { throw }
        Write-Output "PASS: $message (HTTP $status)"
    }
}
function Register([string]$name) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $email = "$prefix-$name@example.test"
    $null = Submit $session '/Account/Register' '/Account/Register' @{ FullName=$name; Email=$email; Password=$password; ConfirmPassword=$password }
    $id = Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}
    if (-not $id) { throw 'Test registration failed' }
    return @{Id=$id; Email=$email; Session=$session}
}
function Login($account) {
    $account.Session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $null = Submit $account.Session '/Account/Login' '/Account/Login' @{Email=$account.Email; Password=$password}
}

try {
    Assert ((Query "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%WorkoutAndDietManagement'") -eq 1) 'Phase 3 migration applied'
    $trainer = Register 'TrainerOne'
    $otherTrainer = Register 'TrainerTwo'
    $client = Register 'ClientOne'
    $otherClient = Register 'ClientTwo'
    $admin = Register 'AdminTrainer'
    & "$PSScriptRoot\..\Scripts\Set-DevelopmentAssignment.ps1" -TrainerEmail $trainer.Email -ClientEmail $client.Email -PromoteTrainer
    & "$PSScriptRoot\..\Scripts\Set-DevelopmentAssignment.ps1" -TrainerEmail $otherTrainer.Email -ClientEmail $otherClient.Email -PromoteTrainer
    $null = Query "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id, Id FROM AspNetRoles WHERE Name IN ('Admin','Trainer');" @{id=$admin.Id}
    foreach ($account in @($trainer,$otherTrainer,$client,$otherClient,$admin)) { Login $account }
    $t=$trainer.Session; $t2=$otherTrainer.Session; $c=$client.Session; $c2=$otherClient.Session; $a=$admin.Session
    $page = Get-Page $t '/Clients'
    Assert ($page.Content.Contains('ClientOne') -and -not $page.Content.Contains('ClientTwo')) 'Trainer sees only assigned clients'
    ExpectStatus 403 { Get-Page $c '/TrainerWorkouts' } 'Client cannot access trainer workouts'
    ExpectStatus 403 { Get-Page $t '/Workouts' } 'Trainer cannot access client workouts'
    ExpectStatus 403 { Get-Page $a '/TrainerWorkouts' } 'Admin with Trainer role cannot inherit trainer actions'
    ExpectStatus 403 { Get-Page $a '/TrainerDiets' } 'Admin cannot access trainer diets'
    ExpectStatus 403 { Get-Page $a '/Clients' } 'Admin cannot access trainer clients'
    $today = (Get-Date).ToString('yyyy-MM-dd'); $end = (Get-Date).AddDays(7).ToString('yyyy-MM-dd')
    $planFields = @{ClientId=$client.Id; PlanName="$prefix workout"; Description='Test plan'; StartDate=$today; EndDate=$end; TrainerId=$otherTrainer.Id}
    $invalid = $planFields.Clone(); $invalid.EndDate=(Get-Date).AddDays(-1).ToString('yyyy-MM-dd')
    $null = Submit $t '/TrainerWorkouts/Create' '/TrainerWorkouts/Create' $invalid
    Assert ((Query 'SELECT COUNT(*) FROM WorkoutPlans WHERE ClientId=@id' @{id=$client.Id}) -eq 0) 'Reversed plan dates rejected'
    $invalid = $planFields.Clone(); $invalid.ClientId=$otherClient.Id
    $null = Submit $t '/TrainerWorkouts/Create' '/TrainerWorkouts/Create' $invalid
    Assert ((Query 'SELECT COUNT(*) FROM WorkoutPlans WHERE TrainerId=@id' @{id=$trainer.Id}) -eq 0) 'Unassigned client rejected on create'
    $null = Submit $t '/TrainerWorkouts/Create' '/TrainerWorkouts/Create' $planFields
    $workout = Query 'SELECT Id FROM WorkoutPlans WHERE ClientId=@id' @{id=$client.Id}
    Assert ((Query 'SELECT TrainerId FROM WorkoutPlans WHERE Id=@id' @{id=$workout}) -eq $trainer.Id) 'Workout created with server-owned TrainerId'
    $otherPlan = $planFields.Clone(); $otherPlan.ClientId=$otherClient.Id; $otherPlan.PlanName='Other private workout'
    $null = Submit $t2 '/TrainerWorkouts/Create' '/TrainerWorkouts/Create' $otherPlan
    $workout2 = Query 'SELECT Id FROM WorkoutPlans WHERE ClientId=@id' @{id=$otherClient.Id}
    $exerciseFields=@{ExerciseName='Walking'; Sets=1; Repetitions=0; DurationMinutes=20; CaloriesTarget=100; Instructions='Easy pace'}
    $null = Submit $t "/TrainerWorkouts/AddExercise?planId=$workout" "/TrainerWorkouts/AddExercise?planId=$workout" $exerciseFields
    $exercise = Query 'SELECT Id FROM WorkoutExercises WHERE WorkoutPlanId=@id' @{id=$workout}
    $null = Submit $t2 "/TrainerWorkouts/AddExercise?planId=$workout2" "/TrainerWorkouts/AddExercise?planId=$workout2" $exerciseFields
    $exercise2 = Query 'SELECT Id FROM WorkoutExercises WHERE WorkoutPlanId=@id' @{id=$workout2}
    $exerciseFields.ExerciseName='Brisk walking'
    $null = Submit $t "/TrainerWorkouts/EditExercise/$exercise" "/TrainerWorkouts/EditExercise/$exercise" $exerciseFields
    Assert ((Query 'SELECT ExerciseName FROM WorkoutExercises WHERE Id=@id' @{id=$exercise}) -eq 'Brisk walking') 'Exercise add and edit work'
    $planFields.PlanName="$prefix edited workout"
    $null = Submit $t "/TrainerWorkouts/Edit/$workout" "/TrainerWorkouts/Edit/$workout" $planFields
    Assert ((Query 'SELECT PlanName FROM WorkoutPlans WHERE Id=@id' @{id=$workout}) -eq $planFields.PlanName) 'Workout plan edit works'
    ExpectStatus 404 { Get-Page $t2 "/TrainerWorkouts/Edit/$workout" } 'Other trainer cannot edit plan'
    ExpectStatus 404 { Submit $t2 '/TrainerWorkouts' "/TrainerWorkouts/Edit/$workout" $planFields } 'Other trainer POST edit blocked'
    ExpectStatus 404 { Submit $t2 '/TrainerWorkouts' "/TrainerWorkouts/DeleteExercise/$exercise" @{} } 'Other trainer cannot delete exercise'
    ExpectStatus 404 { Submit $t2 '/TrainerWorkouts' "/TrainerWorkouts/EditExercise/$exercise" $exerciseFields } 'Other trainer cannot edit exercise'
    ExpectStatus 404 { Get-Page $c2 "/Workouts/Details/$workout" } 'Client cannot read another workout'
    $page=Get-Page $c '/Workouts'
    Assert (-not $page.Content.Contains('Other private workout')) 'Client workout list is private'
    $dietFields=@{ClientId=$client.Id; PlanName="$prefix diet"; Description='Test diet'; StartDate=$today; EndDate=$end; DailyCalories=2000}
    $null = Submit $t '/TrainerDiets/Create' '/TrainerDiets/Create' $dietFields
    $diet=Query 'SELECT Id FROM DietPlans WHERE ClientId=@id' @{id=$client.Id}
    $dietFields.PlanName="$prefix edited diet"; $dietFields.DailyCalories=2100
    $null = Submit $t "/TrainerDiets/Edit/$diet" "/TrainerDiets/Edit/$diet" $dietFields
    Assert ((Query 'SELECT DailyCalories FROM DietPlans WHERE Id=@id' @{id=$diet}) -eq 2100) 'Diet plan create and edit work'
    $otherDiet=$dietFields.Clone(); $otherDiet.ClientId=$otherClient.Id; $otherDiet.PlanName='Other private diet'
    $null = Submit $t2 '/TrainerDiets/Create' '/TrainerDiets/Create' $otherDiet
    $diet2=Query 'SELECT Id FROM DietPlans WHERE ClientId=@id' @{id=$otherClient.Id}
    $mealFields=@{MealType=1; FoodName='Rice'; Quantity='1 cup'; Calories=200; Protein=4; Carbohydrates=40; Fat=1; Instructions='Cooked'}
    foreach ($mealType in 1..4) {
        $mealFields.MealType=$mealType
        $null = Submit $t "/TrainerDiets/AddMeal?planId=$diet" "/TrainerDiets/AddMeal?planId=$diet" $mealFields
    }
    $meal=Query 'SELECT MIN(Id) FROM DietMeals WHERE DietPlanId=@id' @{id=$diet}
    $mealFields.MealType=1; $mealFields.FoodName='Brown rice'
    $null = Submit $t "/TrainerDiets/EditMeal/$meal" "/TrainerDiets/EditMeal/$meal" $mealFields
    $page=Get-Page $c "/Diets/Details/$diet"
    Assert ($page.Content.Contains('Breakfast') -and $page.Content.Contains('Lunch') -and $page.Content.Contains('Dinner') -and $page.Content.Contains('Snack') -and $page.Content.Contains('Brown rice')) 'Meal items edited and grouped by all four meal types'
    ExpectStatus 404 { Get-Page $c2 "/Diets/Details/$diet" } 'Client cannot read another diet'
    ExpectStatus 404 { Submit $t2 '/TrainerDiets' "/TrainerDiets/Edit/$diet" $dietFields } 'Other trainer cannot edit diet'
    ExpectStatus 404 { Submit $t2 '/TrainerDiets' "/TrainerDiets/DeleteMeal/$meal" @{} } 'Other trainer cannot delete meal item'
    ExpectStatus 404 { Submit $t2 '/TrainerDiets' "/TrainerDiets/EditMeal/$meal" $mealFields } 'Other trainer cannot edit meal item'
    $log=@{WorkoutPlanId=$workout; WorkoutExerciseId=$exercise; WorkoutDate=$today; DurationMinutes=25; CaloriesBurned=120; Notes='Completed walk'; ClientId=$otherClient.Id; Completed='false'}
    $bad=$log.Clone(); $bad.WorkoutPlanId=$workout2
    ExpectStatus 404 { Submit $c '/Workouts/Log' '/Workouts/Log' $bad } 'Client cannot log another workout plan'
    $bad=$log.Clone(); $bad.WorkoutExerciseId=$exercise2
    $null = Submit $c '/Workouts/Log' '/Workouts/Log' $bad
    Assert ((Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 0) 'Cross-plan exercise rejected'
    $bad=$log.Clone(); $bad.DurationMinutes=-1
    $null = Submit $c '/Workouts/Log' '/Workouts/Log' $bad
    Assert ((Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 0) 'Negative duration rejected'
    $bad=$log.Clone(); $bad.WorkoutDate=$end
    $null = Submit $c '/Workouts/Log' '/Workouts/Log' $bad
    Assert ((Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 0) 'Future workout date rejected'
    $null = Submit $c '/Workouts/Log' '/Workouts/Log' $log
    Assert ((Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id AND Completed=1' @{id=$client.Id}) -eq 1) 'Completed workout logged; ownership and completion cannot be overposted'
    Assert ((Get-Page $c '/Workouts/History').Content.Contains('Completed walk')) 'Client workout history displays log'
    Assert (-not (Get-Page $c2 '/Workouts/History').Content.Contains('Completed walk')) 'Workout history isolated between clients'
    Assert ((Get-Page $t ("/TrainerWorkouts/Logs?clientId="+$client.Id)).Content.Contains('Completed walk')) 'Assigned trainer can review workout logs'
    ExpectStatus 404 { Get-Page $t2 ("/TrainerWorkouts/Logs?clientId="+$client.Id) } 'Other trainer cannot view client logs'
    $mealLog=@{DietPlanId=$diet; MealDate=$today; MealType=1; FoodName='Brown rice'; Quantity='1 cup'; Calories=200; Notes='Breakfast complete'; ClientId=$otherClient.Id}
    $bad=$mealLog.Clone(); $bad.DietPlanId=$diet2
    ExpectStatus 404 { Submit $c '/Diets/Log' '/Diets/Log' $bad } 'Client cannot log another diet plan'
    $bad=$mealLog.Clone(); $bad.MealType=999
    $null=Submit $c '/Diets/Log' '/Diets/Log' $bad
    Assert ((Query 'SELECT COUNT(*) FROM MealLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 0) 'Invalid meal type rejected'
    $null=Submit $c '/Diets/Log' '/Diets/Log' $mealLog
    Assert ((Query 'SELECT COUNT(*) FROM MealLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 1) 'Meal log ownership comes from sign-in'
    Assert ((Get-Page $c '/Diets/History').Content.Contains('Breakfast complete')) 'Meal history displays log'
    Assert (-not (Get-Page $c2 '/Diets/History').Content.Contains('Breakfast complete')) 'Meal history isolated between clients'
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/TrainerWorkouts/DeleteExercise/$exercise" -WebSession $t -Method Post } 'Mutation requires antiforgery token'
    $null=Submit $t '/TrainerWorkouts' "/TrainerWorkouts/SetStatus/$workout" @{status=2}
    $null=Submit $c '/Workouts' '/Workouts/Log' $log
    Assert ((Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 1) 'Inactive workout cannot receive logs'
    $null=Submit $t '/TrainerWorkouts' "/TrainerWorkouts/SetStatus/$workout" @{status=1}
    Assert ((Query 'SELECT Status FROM WorkoutPlans WHERE Id=@id' @{id=$workout}) -eq 1) 'Workout reactivated'
    $null=Submit $t '/TrainerDiets' "/TrainerDiets/SetStatus/$diet" @{status=2}
    $null=Submit $c '/Diets' '/Diets/Log' $mealLog
    Assert ((Query 'SELECT COUNT(*) FROM MealLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 1) 'Inactive diet cannot receive logs'
    $null=Submit $t '/TrainerDiets' "/TrainerDiets/SetStatus/$diet" @{status=1}
    Assert ((Query 'SELECT Status FROM DietPlans WHERE Id=@id' @{id=$diet}) -eq 1) 'Diet reactivated'
    $null=Submit $t '/TrainerWorkouts' "/TrainerWorkouts/DeleteExercise/$exercise" @{}
    Assert ((Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id AND WorkoutExerciseId IS NULL' @{id=$client.Id}) -eq 1) 'Exercise deletion preserves workout log'
    $null=Submit $t '/TrainerDiets' "/TrainerDiets/DeleteMeal/$meal" @{}
    Assert ((Query 'SELECT COUNT(*) FROM MealLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 1) 'Meal item deletion preserves meal history'
    $null=Query 'DELETE FROM TrainerClients WHERE ClientId=@id' @{id=$client.Id}
    ExpectStatus 404 { Get-Page $t "/TrainerWorkouts/Details/$workout" } 'Removing assignment revokes trainer workout access'
    ExpectStatus 404 { Get-Page $t "/TrainerDiets/Details/$diet" } 'Removing assignment revokes trainer diet access'
}
finally {
    $null = Query @'
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
