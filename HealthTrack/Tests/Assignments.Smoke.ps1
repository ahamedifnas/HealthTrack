param([string]$BaseUrl = 'http://localhost:5091')

# Uses the local development database. All uniquely named test accounts are removed afterward.
$ErrorActionPreference='Stop'
$prefix='assignments-' + [Guid]::NewGuid().ToString('N')
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
    foreach($name in @('client','otherClient','trainer','otherTrainer','admin','inactive')) {
        $email="$prefix-$name@example.test"
        $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $null=Submit $session '/Account/Register' '/Account/Register' @{FullName="$name $prefix";Email=$email;Password=$password;ConfirmPassword=$password}
        $id=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}
        if($name -in @('trainer','otherTrainer','admin')) {
            $role=if($name -eq 'admin') {'Admin'} else {'Trainer'}
            $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name=@role' @{id=$id;role=$role}
        }
        $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $null=Submit $session '/Account/Login' '/Account/Login' @{Email=$email;Password=$password}
        $accounts[$name]=@{Id=$id;Session=$session}
    }
    $clientId=$accounts.client.Id; $trainerId=$accounts.trainer.Id; $otherId=$accounts.otherClient.Id; $otherTrainerId=$accounts.otherTrainer.Id
    $admin=$accounts.admin.Session; $trainer=$accounts.trainer.Session; $otherTrainer=$accounts.otherTrainer.Session; $client=$accounts.client.Session
    $null=Query 'UPDATE AspNetUsers SET IsActive=0 WHERE Id=@id' @{id=$accounts.inactive.Id}
    $page=Get-Page $admin '/Dashboard/Admin'
    Assert ($page.Content.Contains('Trainer-Client Assignment') -and $page.Content.Contains('name="Assignment.ClientId"')) 'Admin Dashboard contains assignment controls'
    Assert (-not $page.Content.Contains("inactive $prefix")) 'Inactive clients are excluded from dropdown'
    $fields=@{'Assignment.ClientId'=$clientId;'Assignment.TrainerId'=$trainerId}
    ExpectStatus 403 { Submit $trainer '/Account/Login' '/AdminAssignments/Assign' $fields } 'Trainer cannot assign clients'
    ExpectStatus 403 { Submit $client '/Account/Login' '/AdminAssignments/Assign' $fields } 'Client cannot assign trainers'
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/AdminAssignments/Assign" -WebSession $admin -Method Post -Body $fields } 'Assignment requires antiforgery token'
    foreach($invalid in @(@{'Assignment.ClientId'=$accounts.inactive.Id},@{'Assignment.ClientId'=$trainerId},@{'Assignment.TrainerId'=$clientId},@{'Assignment.TrainerId'=$accounts.admin.Id},@{'Assignment.ClientId'='missing'})) {
        $bad=$fields.Clone();foreach($key in $invalid.Keys){$bad[$key]=$invalid[$key]}
        $page=Submit $admin '/Dashboard/Admin' '/AdminAssignments/Assign' $bad
        Assert ($page.Content.Contains('Select an active') -and $page.Content.Contains('Admin Dashboard')) 'Invalid role, inactive, or missing account rejected on dashboard'
    }
    $page=Submit $admin '/Dashboard/Admin' '/AdminAssignments/Assign' $fields
    Assert ($page.Content.Contains('Client assigned to trainer successfully.')) 'Admin assignment succeeds and returns to dashboard'
    Assert ((Query 'SELECT COUNT(*) FROM TrainerClients WHERE ClientId=@client AND TrainerId=@trainer AND AssignedAt IS NOT NULL' @{client=$clientId;trainer=$trainerId}) -eq 1) 'Assignment and timestamp persist in SQL Server'
    $null=Submit $admin '/Dashboard/Admin' '/AdminAssignments/Assign' $fields
    Assert ((Query 'SELECT COUNT(*) FROM TrainerClients WHERE ClientId=@id' @{id=$clientId}) -eq 1) 'Repeated assignment does not duplicate row'
    $bad=$fields.Clone();$bad['Assignment.TrainerId']=$otherTrainerId
    $page=Submit $admin '/Dashboard/Admin' '/AdminAssignments/Assign' $bad
    Assert ($page.Content.Contains('already assigned to another trainer')) 'Client cannot be assigned to two trainers'
    $null=Submit $admin '/Dashboard/Admin' '/AdminAssignments/Assign' @{'Assignment.ClientId'=$otherId;'Assignment.TrainerId'=$otherTrainerId}
    Assert ((Get-Page $trainer '/Clients').Content.Contains("client $prefix")) 'Assigned client appears in Trainer Clients list'
    $page=Get-Page $trainer '/Dashboard/Trainer'
    Assert ($page.Content.Contains('data-testid="total-clients">1<') -and $page.Content.Contains("client $prefix")) 'Trainer dashboard shows one client and Recent Clients'
    Assert (-not $page.Content.Contains("otherClient $prefix")) 'Recent Clients excludes other trainer clients'
    $plan=@{ClientId=$clientId;PlanName="plan $prefix";StartDate=(Get-Date).ToString('yyyy-MM-dd');EndDate=(Get-Date).AddDays(30).ToString('yyyy-MM-dd');DailyCalories='2000'}
    foreach($controller in @('TrainerWorkouts','TrainerDiets')) {
        $page=Get-Page $trainer "/$controller/Create?clientId=$clientId"
        Assert ($page.Content.Contains($clientId) -and -not $page.Content.Contains('You have no active assigned clients')) "$controller form offers assigned client"
        $page=Submit $trainer "/$controller/Create" "/$controller/Create" $plan
        Assert ($page.Content.Contains("plan $prefix")) "Assigned trainer creates $controller plan"
        ExpectStatus 404 { Get-Page $trainer "/$controller/Create?clientId=$otherId" } "$controller rejects unassigned client URL"
        $forged=$plan.Clone();$forged.ClientId=$otherId
        $page=Submit $trainer "/$controller/Create" "/$controller/Create" $forged
        Assert ($page.Content.Contains('Choose one of your assigned clients.')) "$controller rejects forged client POST"
    }
    $workout=Query 'SELECT Id FROM WorkoutPlans WHERE ClientId=@id' @{id=$clientId}
    $diet=Query 'SELECT Id FROM DietPlans WHERE ClientId=@id' @{id=$clientId}
    ExpectStatus 404 { Get-Page $otherTrainer "/TrainerWorkouts/Details/$workout" } 'Other trainer cannot open workout by ID'
    ExpectStatus 404 { Get-Page $otherTrainer "/TrainerDiets/Details/$diet" } 'Other trainer cannot open diet by ID'
    foreach ($entry in @(@{Controller='TrainerWorkouts';Id=$workout},@{Controller='TrainerDiets';Id=$diet})) {
        $controller=$entry.Controller; $planId=$entry.Id
        ExpectStatus 404 { Get-Page $otherTrainer "/$controller/Edit/$planId" } "$controller rejects another trainer's edit URL"
        ExpectStatus 404 { Submit $otherTrainer "/$controller/Create" "/$controller/Edit/$planId" $plan } "$controller rejects another trainer's edit POST"
        ExpectStatus 404 { Submit $otherTrainer "/$controller/Create" "/$controller/SetStatus/$planId" @{status='2'} } "$controller rejects another trainer's status POST"
    }
    ExpectStatus 404 { Get-Page $otherTrainer "/Progress?clientId=$clientId" } 'Other trainer cannot open client progress by ID'
    ExpectStatus 404 { Get-Page $otherTrainer "/TrainerWorkouts/Logs?clientId=$clientId" } 'Other trainer cannot open client workout logs by ID'
    $page=Get-Page $trainer '/Dashboard/Trainer'
    Assert ($page.Content.Contains('data-testid="active-workout-plans">1<') -and $page.Content.Contains('data-testid="active-diet-plans">1<')) 'Active plan counts reflect created plans'
    $null=Query 'UPDATE AspNetUsers SET IsActive=0 WHERE Id=@id' @{id=$clientId}
    $page=Get-Page $trainer '/Dashboard/Trainer'
    Assert ($page.Content.Contains('data-testid="total-clients">0<') -and $page.Content.Contains('data-testid="active-workout-plans">0<') -and $page.Content.Contains('data-testid="active-diet-plans">0<') -and -not $page.Content.Contains("client $prefix")) 'Inactive assigned client excluded from counts and Recent Clients'
    ExpectStatus 404 { Get-Page $trainer "/TrainerWorkouts/Details/$workout" } 'Inactive assigned client plans cannot be opened'
    $null=Query 'UPDATE AspNetUsers SET IsActive=1 WHERE Id=@id' @{id=$clientId}
    $null=Query 'UPDATE WorkoutPlans SET Status=2 WHERE Id=@id' @{id=$workout}
    Assert ((Get-Page $trainer '/Dashboard/Trainer').Content.Contains('data-testid="active-workout-plans">0<')) 'Inactive plans excluded from counts'
    $null=Query 'DELETE FROM TrainerClients WHERE ClientId=@id' @{id=$clientId}
    ExpectStatus 404 { Get-Page $trainer "/TrainerDiets/Details/$diet" } 'Removing assignment revokes plan access'
    Assert ((Get-Page $trainer '/Dashboard/Trainer').Content.Contains('data-testid="total-clients">0<')) 'Removing assignment updates dashboard count'
    Write-Output "All $script:passed assignment checks passed."
}
finally {
    $null=Query 'DELETE FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM TrainerClients WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix) OR TrainerId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM AspNetUsers WHERE Email LIKE @prefix' @{prefix=($prefix+'%')}
    $database.Dispose()
}
