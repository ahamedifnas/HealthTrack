param([string]$BaseUrl = 'http://localhost:5092')
# SQL Server integration checks. All temporary accounts and records are removed in finally.
$ErrorActionPreference = 'Stop'
$prefix = 'dashboard-ui-' + [Guid]::NewGuid().ToString('N')
$password = 'Ht6!' + [Guid]::NewGuid().ToString('N')
$database = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$database.Open()
$zoneId = (Get-Content -Raw "$PSScriptRoot/../appsettings.json" | ConvertFrom-Json).Appointments.TimeZoneId
if ($zoneId -eq 'Asia/Colombo') { $zoneId = 'Sri Lanka Standard Time' }
$today = [TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow, [TimeZoneInfo]::FindSystemTimeZoneById($zoneId)).Date
$script:passed = 0
function Query([string]$sql, [hashtable]$parameters = @{}) {
    $command = $database.CreateCommand(); $command.CommandText = $sql
    foreach ($key in $parameters.Keys) { [void]$command.Parameters.AddWithValue('@' + $key, $parameters[$key]) }
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:passed++; Write-Output "PASS: $message"
}
function Page($session, [string]$path) { return (Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -WebSession $session).Content }
function Submit($session, [string]$getPath, [string]$postPath, [hashtable]$fields) {
    $html = Page $session $getPath
    $match = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw 'Missing antiforgery token' }
    $fields.__RequestVerificationToken = [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
    return (Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $postPath) -WebSession $session -Method Post -Body $fields).Content
}
function Metric([string]$html, [string]$title) {
    $html = [System.Net.WebUtility]::HtmlDecode($html)
    $match = [regex]::Match($html, '<h2 class="kpi-title">' + [regex]::Escape($title) + '</h2>\s*<p class="dashboard-value"[^>]*>([^<]+)</p>')
    if (-not $match.Success) { throw "Missing metric $title" }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}
function ExpectForbidden($session, [string]$path) {
    try { $null = Page $session $path; throw "Expected forbidden: $path" }
    catch { if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 403) { throw }; Assert $true "Role access remains restricted for $path" }
}
try {
    $accounts = @{}
    foreach ($name in @('Client','Other','Trainer','OtherTrainer','Admin')) {
        $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $email = "$prefix-$name@example.test"
        $null = Submit $session '/Account/Register' '/Account/Register' @{FullName="$name $prefix";Email=$email;Password=$password;ConfirmPassword=$password}
        $id = Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}
        if (-not $id) { throw 'Registration failed' }
        $accounts[$name] = @{Id=$id;Session=$session}
        if ($name -in @('Trainer','OtherTrainer','Admin')) {
            $role = if ($name -eq 'Admin') { 'Admin' } else { 'Trainer' }
            $null = Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name=@role' @{id=$id;role=$role}
        }
    }
    $client = $accounts.Client; $trainer = $accounts.Trainer; $admin = $accounts.Admin; $other = $accounts.Other
    $null = Submit $admin.Session '/Dashboard/Admin' '/AdminAssignments/Assign' @{'Assignment.ClientId'=$client.Id;'Assignment.TrainerId'=$trainer.Id}
    $null = Query 'UPDATE AspNetUsers SET TargetWeight=80 WHERE Id=@id' @{id=$client.Id}
    foreach ($entry in @(@(-3,90),@(0,85))) {
        $null = Query 'INSERT INTO HealthMeasurements (ClientId,MeasurementDate,WeightKg,HeightCm,BMI,CaloriesBurned) VALUES (@client,@date,@weight,180,26,0)' @{client=$client.Id;date=$today.AddDays($entry[0]);weight=$entry[1]}
    }
    $workout = Query "INSERT INTO WorkoutPlans (ClientId,TrainerId,PlanName,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,@name,@start,@end,1,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer.Id;name="$prefix workout";start=$today;end=$today.AddDays(10)}
    $null = Query "INSERT INTO DietPlans (ClientId,TrainerId,PlanName,DailyCalories,StartDate,EndDate,Status,CreatedAt) VALUES (@client,@trainer,@name,2100,@start,@end,1,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer.Id;name="$prefix diet";start=$today;end=$today.AddDays(10)}
    foreach ($entry in @(@(0,120),@(-6,230),@(-7,900),@(1,800))) {
        $null = Query 'INSERT INTO WorkoutLogs (ClientId,WorkoutPlanId,WorkoutDate,DurationMinutes,CaloriesBurned,Completed) VALUES (@client,@plan,@date,20,@calories,1)' @{client=$client.Id;plan=$workout;date=$today.AddDays($entry[0]);calories=$entry[1]}
    }
    foreach ($entry in @(@(1,2),@(2,1),@(-1,2),@(3,4))) {
        $null = Query "INSERT INTO Appointments (ClientId,TrainerId,AppointmentDate,AppointmentTime,Reason,Status,CreatedAt) VALUES (@client,@trainer,@date,'10:00',@reason,@status,SYSUTCDATETIME())" @{client=$client.Id;trainer=$trainer.Id;date=$today.AddDays($entry[0]);status=$entry[1];reason="$prefix consultation"}
    }
    $foreign = Query "INSERT INTO Appointments (ClientId,TrainerId,AppointmentDate,AppointmentTime,Reason,Status,CreatedAt) OUTPUT INSERTED.Id VALUES (@client,@trainer,@date,'11:00',@reason,2,SYSUTCDATETIME())" @{client=$other.Id;trainer=$accounts.OtherTrainer.Id;date=$today.AddDays(1);reason="$prefix other consultation"}
    foreach ($account in @($client,$other)) {
        $null = Query 'INSERT INTO Notifications (UserId,Title,Message,Type,CreatedAt,IsRead) VALUES (@user,@title,@message,1,SYSUTCDATETIME(),0)' @{user=$account.Id;title="$prefix-$($account.Id)";message='Appointment update'}
    }
    $html = Page $client.Session '/Dashboard/Client'
    Assert ($html.Contains('Welcome, Client!')) 'Client greeting uses the real first name'
    Assert ((Metric $html 'Current Weight') -eq '85 kg' -and (Metric $html 'Target Weight') -eq '80 kg') 'Weight KPI values come from the logged-in client records'
    Assert ((Metric $html 'Calories Burned') -eq '350 kcal') 'Calories total includes only today and the previous six days'
    Assert ($html.Contains('aria-valuenow="50"') -and $html.Contains('width: 50%')) 'C# weight progress renders an accessible 50 percent progress bar'
    Assert ($html.Contains('1 unread') -and $html.Contains("$prefix-$($client.Id)") -and -not $html.Contains("$prefix-$($other.Id)")) 'Notifications show the client unread count without leaking another client records'
    Assert ($html.Contains("$prefix workout") -and $html.Contains("$prefix diet") -and $html.Contains('data-testid="next-appointment"')) 'Daily overview renders real plans and approved appointment'
    foreach ($weight in @(100,80)) {
        $null = Query 'UPDATE HealthMeasurements SET WeightKg=@weight WHERE ClientId=@client AND MeasurementDate=@date' @{weight=$weight;client=$client.Id;date=$today}
        $percent = if ($weight -eq 80) { 100 } else { 0 }
        Assert ((Page $client.Session '/Dashboard/Client').Contains("aria-valuenow=`"$percent`"")) "Weight progress correctly renders $percent percent"
    }
    $null = Query 'UPDATE HealthMeasurements SET WeightKg=CASE WHEN MeasurementDate=@date THEN 65 ELSE 60 END WHERE ClientId=@client; UPDATE AspNetUsers SET TargetWeight=70 WHERE Id=@client' @{date=$today;client=$client.Id}
    Assert ((Page $client.Session '/Dashboard/Client').Contains('aria-valuenow="50"')) 'Weight progress also supports weight gain goals'
    $null = Query 'UPDATE AspNetUsers SET TargetWeight=NULL WHERE Id=@client' @{client=$client.Id}
    Assert (-not (Page $client.Session '/Dashboard/Client').Contains('role="progressbar"')) 'Missing target never displays fabricated progress'
    $empty = Page $other.Session '/Dashboard/Client'
    Assert ((Metric $empty 'Calories Burned') -eq 'Not available yet') 'Client without calorie logs retains the required empty state'
    $html = Page $trainer.Session '/Dashboard/Trainer'
    Assert ($html.Contains('Welcome back, Trainer.')) 'Trainer greeting uses the real first name'
    Assert ((Metric $html 'Total Assigned Clients') -eq '1' -and (Metric $html 'Active Workout Plans') -eq '1' -and (Metric $html 'Active Diet Plans') -eq '1') 'Trainer KPI counts reflect assigned clients and active plans'
    Assert ((Metric $html 'Pending Appointments') -eq '1' -and (Metric $html "Today's Appointments") -eq '0') 'Trainer appointment counts come from real scoped records'
    $upcoming = [regex]::Match($html, '(?s)id="upcoming-heading".*?</section>').Value
    Assert ([regex]::Matches($upcoming, 'class="badge status-').Count -eq 2 -and -not $upcoming.Contains("/Appointments/Details/$foreign")) 'Upcoming appointments exclude past, cancelled and other trainer appointments'
    Assert ($html.Contains($client.Id) -and $html.Contains('Quick View') -and $html.Contains('Plan Summary')) 'Recent clients retain progress and plan creation actions'
    $html = Page $admin.Session '/Dashboard/Admin'
    Assert ($html.Contains('HealthTrack Administration') -and $html.Contains('Welcome back, Admin.')) 'Admin header uses the real first name'
    $counts = @(
        @('Total Users','SELECT COUNT(*) FROM AspNetUsers'),
        @('Total Clients',"SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Client'"),
        @('Total Trainers',"SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Trainer'"),
        @('Active Users','SELECT COUNT(*) FROM AspNetUsers WHERE IsActive=1'),
        @('Total Appointments','SELECT COUNT(*) FROM Appointments'),
        @('Active Workout Plans','SELECT COUNT(*) FROM WorkoutPlans WHERE Status=1'),
        @('Active Diet Plans','SELECT COUNT(*) FROM DietPlans WHERE Status=1')
    )
    foreach ($entry in $counts) { Assert ((Metric $html $entry[0]) -eq (Query $entry[1]).ToString()) "Admin $($entry[0]) matches SQL Server" }
    Assert ($html.Contains('Recent System Activity') -and $html.Contains("$prefix-Admin@example.test") -and $html.Contains('Recent Assignments') -and $html.Contains("/Appointments/Details/$foreign")) 'Admin activity displays recently created users, assignments and appointments'
    Assert ($html.Contains('action="/AdminAssignments/Assign"') -and $html.Contains('/AdminTrainers/SetActive/') -and $html.Contains('/AdminReports/Print')) 'Admin management forms and printable report access remain available'
    ExpectForbidden $client.Session '/Dashboard/Trainer'
    ExpectForbidden $client.Session '/Dashboard/Admin'
    ExpectForbidden $trainer.Session '/Dashboard/Client'
    ExpectForbidden $trainer.Session '/Dashboard/Admin'
    ExpectForbidden $admin.Session '/Dashboard/Client'
    ExpectForbidden $admin.Session '/Dashboard/Trainer'
    Write-Output "All $script:passed dashboard UI integration checks passed."
}
finally {
    $filter = @{prefix=($prefix+'%')}
    $null = Query 'DELETE FROM Appointments WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix) OR TrainerId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM Notifications WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM WorkoutLogs WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM HealthMeasurements WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM TrainerClients WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix) OR TrainerId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM AspNetUsers WHERE Email LIKE @prefix' $filter
    $database.Dispose()
}
