param([string]$BaseUrl = 'http://localhost:5090')

# Uses the local development database. All uniquely named test accounts are removed afterward.
$ErrorActionPreference='Stop'
$prefix='dashboard-plans-' + [Guid]::NewGuid().ToString('N')
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
    $accounts=@{}
    foreach ($name in @('client','empty','trainer')) {
        $email="$prefix-$name@example.test"
        $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $null=Submit $session '/Account/Register' '/Account/Register' @{FullName=$name;Email=$email;Password=$password;ConfirmPassword=$password}
        $id=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}
        $accounts[$name]=@{Id=$id;Session=$session}
    }
    $client=$accounts.client.Session; $empty=$accounts.empty.Session
    $clientId=$accounts.client.Id; $emptyId=$accounts.empty.Id; $trainerId=$accounts.trainer.Id
    $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name='Trainer'" @{id=$trainerId}
    $page=Get-Page $empty '/Dashboard/Client'
    Assert ($page.Content.Contains('No active workout plan assigned yet.')) 'Client without plans sees workout empty state'
    Assert ($page.Content.Contains('No active diet plan assigned yet.')) 'Client without plans sees diet empty state'
    Assert (-not $page.Content.Contains('Coming soon')) 'Coming soon badges removed'
    $workout=Query "INSERT INTO WorkoutPlans (ClientId,TrainerId,PlanName,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,'Dashboard workout',CAST(GETDATE() AS date),DATEADD(day,10,CAST(GETDATE() AS date)),1,SYSUTCDATETIME())" @{client=$clientId;trainer=$trainerId}
    $diet=Query "INSERT INTO DietPlans (ClientId,TrainerId,PlanName,DailyCalories,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,'Dashboard diet',2100,CAST(GETDATE() AS date),DATEADD(day,10,CAST(GETDATE() AS date)),1,SYSUTCDATETIME())" @{client=$clientId;trainer=$trainerId}
    $page=Get-Page $client '/Dashboard/Client'
    Assert ($page.Content.Contains('Dashboard workout') -and $page.Content.Contains('Your workout plan has no exercises added yet.')) 'Active workout without exercises shows plan name and exact empty state'
    Assert ($page.Content.Contains('Dashboard diet') -and $page.Content.Contains('Your diet plan has no meals added yet.')) 'Active diet without meals shows plan name and exact empty state'
    Assert ($page.Content.Contains('2100 kcal')) 'Diet daily calorie target displayed'
    foreach ($number in 1..4) {
        $null=Query 'INSERT INTO WorkoutExercises (WorkoutPlanId,ExerciseName,Sets,Repetitions,DurationMinutes,CaloriesTarget) VALUES (@plan,@name,3,12,15,0)' @{plan=$workout;name="DashboardExercise$number"}
    }
    foreach ($number in 1..5) {
        $type=[Math]::Min($number,4)
        $null=Query "INSERT INTO DietMeals (DietPlanId,MealType,FoodName,Quantity,Calories,Protein,Carbohydrates,Fat) VALUES (@plan,@type,@name,'1 serving',300,0,0,0)" @{plan=$diet;type=$type;name="DashboardMeal$number"}
    }
    $page=Get-Page $client '/Dashboard/Client'
    Assert ($page.Content.Contains('DashboardExercise1') -and $page.Content.Contains('DashboardExercise3') -and -not $page.Content.Contains('DashboardExercise4')) 'Workout preview limited to three exercises'
    Assert ($page.Content.Contains('3 sets') -and $page.Content.Contains('12 reps') -and $page.Content.Contains('15 min')) 'Exercise sets, repetitions and duration displayed'
    Assert ($page.Content.Contains('DashboardMeal1') -and $page.Content.Contains('DashboardMeal4') -and -not $page.Content.Contains('DashboardMeal5')) 'Diet preview limited to four meal items'
    foreach ($type in @('Breakfast','Lunch','Dinner','Snack')) { Assert ($page.Content.Contains($type)) "$type meal label displayed" }
    Assert ($page.Content.Contains('View Full Workout') -and $page.Content.Contains('href="/Workouts"')) 'Full workout links to existing My Workouts page'
    Assert ($page.Content.Contains('View Full Diet') -and $page.Content.Contains('href="/Diets"')) 'Full diet links to existing My Diet page'
    Assert ((Get-Page $client '/Workouts').Content.Contains('Dashboard workout')) 'Existing My Workouts page remains usable'
    Assert ((Get-Page $client '/Diets').Content.Contains('Dashboard diet')) 'Existing My Diet page remains usable'
    $page=Get-Page $empty "/Dashboard/Client/$clientId`?clientId=$clientId"
    Assert (-not $page.Content.Contains('Dashboard workout') -and -not $page.Content.Contains('Dashboard diet') -and -not $page.Content.Contains('DashboardExercise') -and -not $page.Content.Contains('DashboardMeal')) 'Another client cannot access plans by forging route and query IDs'
    $newWorkout=Query "INSERT INTO WorkoutPlans (ClientId,TrainerId,PlanName,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,'Newest workout',CAST(GETDATE() AS date),DATEADD(day,10,CAST(GETDATE() AS date)),1,DATEADD(second,1,SYSUTCDATETIME()))" @{client=$clientId;trainer=$trainerId}
    $newDiet=Query "INSERT INTO DietPlans (ClientId,TrainerId,PlanName,DailyCalories,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,'Newest diet',0,CAST(GETDATE() AS date),DATEADD(day,10,CAST(GETDATE() AS date)),1,DATEADD(second,1,SYSUTCDATETIME()))" @{client=$clientId;trainer=$trainerId}
    $page=Get-Page $client '/Dashboard/Client'
    Assert ($page.Content.Contains('Newest workout') -and $page.Content.Contains('Newest diet') -and -not $page.Content.Contains('Dashboard workout') -and -not $page.Content.Contains('Dashboard diet')) 'Newest active plan selected when multiple plans exist'
    Assert (-not $page.Content.Contains('Daily calorie target:')) 'Unavailable calorie target omitted'
    $null=Query 'UPDATE WorkoutPlans SET Status=2 WHERE ClientId=@client; UPDATE DietPlans SET Status=2 WHERE ClientId=@client' @{client=$clientId}
    $page=Get-Page $client '/Dashboard/Client'
    Assert ($page.Content.Contains('No active workout plan assigned yet.') -and $page.Content.Contains('No active diet plan assigned yet.')) 'Inactive plans are excluded'
    Assert ($page.Content.Contains('Current Weight') -and $page.Content.Contains('Target Weight') -and $page.Content.Contains('Next approved appointment')) 'Existing dashboard summary and appointment cards remain'
    Write-Output "All $script:passed dashboard plan checks passed."
}
finally {
    $null=Query 'DELETE FROM WorkoutExercises WHERE WorkoutPlanId IN (SELECT Id FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix)); DELETE FROM DietMeals WHERE DietPlanId IN (SELECT Id FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix)); DELETE FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM AspNetUsers WHERE Email LIKE @prefix' @{prefix=($prefix+'%')}
    $database.Dispose()
}
