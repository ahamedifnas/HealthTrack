param([string]$BaseUrl = 'http://localhost:5075')

# Integration test for the default development LocalDB. Temporary accounts and appointments
# are removed in finally. Past appointments are simulated only on this test's own records.
$ErrorActionPreference = 'Stop'
$prefix = 'notifications-' + [Guid]::NewGuid().ToString('N')
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

function Count($account, [string]$title) {
    return Query 'SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND Title=@title' @{id=$account.Id;title=$title}
}
try {
    $trainer=Register 'trainer'; $client=Register 'client'; $other=Register 'other'
    $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name='Trainer';" @{id=$trainer.Id}
    $null=Query 'INSERT INTO TrainerClients (TrainerId,ClientId) VALUES (@trainer,@client)' @{trainer=$trainer.Id;client=$client.Id}
    Login $trainer
    $t=$trainer.Session; $c=$client.Session; $o=$other.Session
    $anonymous=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    Assert ((Get-Page $anonymous '/Notifications').Content.Contains('name="Password"')) 'Anonymous inbox requires login'
    Assert ((Get-Page $c '/Notifications').Content.Contains('No notifications yet.')) 'Empty inbox renders'
    $id=Book $c $trainer.Id $tomorrow '10:00' "$prefix booking"
    Assert ((Count $trainer 'Appointment requested') -eq 1) 'Booking notifies trainer once'
    Assert ((Count $client 'Appointment requested') -eq 0) 'Booking does not notify actor'
    $null=Submit $c '/Appointments/Book' '/Appointments/Book' @{TrainerId=$trainer.Id;AppointmentDate=$tomorrow;AppointmentTime='10:00';Reason='Conflicting booking'}
    Assert ((Count $trainer 'Appointment requested') -eq 1) 'Conflicting save rolls back notification'
    $null=Submit $t '/Appointments' "/Appointments/Approve/$id" @{}
    Assert ((Count $client 'Appointment approved') -eq 1) 'Approval notifies client'
    $null=Submit $t '/Appointments' "/Appointments/Approve/$id" @{}
    Assert ((Count $client 'Appointment approved') -eq 1) 'Repeated approval creates no notification'
    $fields=@{AppointmentDate=$later;AppointmentTime='11:00';Notes='New time';RowVersion=(Version $t $id)}
    $null=Submit $t "/Appointments/Edit/$id" "/Appointments/Edit/$id" $fields
    Assert ((Count $client 'Appointment rescheduled') -eq 1) 'Reschedule notifies client'
    $null=Submit $t "/Appointments/Edit/$id" "/Appointments/Edit/$id" $fields
    Assert ((Count $client 'Appointment rescheduled') -eq 1) 'Stale reschedule creates no notification'
    $fields.RowVersion=Version $t $id; $fields.Notes='Notes only'
    $null=Submit $t "/Appointments/Edit/$id" "/Appointments/Edit/$id" $fields
    Assert ((Count $client 'Appointment rescheduled') -eq 1) 'Notes-only edit is not a reschedule'
    $null=Submit $t '/Appointments' "/Appointments/TrainerCancel/$id" @{}
    Assert ((Count $client 'Appointment cancelled') -eq 1) 'Trainer cancellation notifies client'
    $second=Book $c $trainer.Id $tomorrow '12:00' "$prefix second"
    $null=Submit $c '/Appointments' "/Appointments/Cancel/$second" @{}
    Assert ((Count $trainer 'Appointment cancelled') -eq 1) 'Client cancellation notifies trainer'
    foreach($kind in @('Workout','Diet')) {
        $controller="Trainer${kind}s"
        $fields=@{ClientId=$client.Id;PlanName="$prefix $kind";StartDate=$today;EndDate=$later;DailyCalories=2000}
        $null=Submit $t "/$controller/Create" "/$controller/Create" $fields
        $plan=Query "SELECT Id FROM ${kind}Plans WHERE ClientId=@id" @{id=$client.Id}
        Assert ((Count $client "$kind plan assigned") -eq 1) "$kind assignment notifies client"
        $fields.PlanName="$prefix $kind updated"
        $null=Submit $t "/$controller/Edit/$plan" "/$controller/Edit/$plan" $fields
        Assert ((Count $client "$kind plan updated") -eq 1) "$kind edit notifies client"
        $fields.PlanName=''
        $null=Submit $t "/$controller/Edit/$plan" "/$controller/Edit/$plan" $fields
        Assert ((Count $client "$kind plan updated") -eq 1) "Invalid $kind edit creates no notification"
        $itemName=if($kind -eq 'Workout'){'Exercise'}else{'Meal'}
        $itemFields=if($kind -eq 'Workout'){@{ExerciseName='Walking';Sets=1;DurationMinutes=20}}else{@{MealType=1;FoodName='Rice';Quantity='1 cup';Calories=200}}
        $null=Submit $t "/$controller/Details/$plan" "/$controller/Add${itemName}?planId=$plan" $itemFields
        Assert ((Count $client "$kind plan updated") -eq 2) "$kind item addition notifies client"
        $itemId=Query "SELECT Id FROM ${kind}${itemName}s WHERE ${kind}PlanId=@id" @{id=$plan}
        $null=Submit $t "/$controller/Edit${itemName}/$itemId" "/$controller/Edit${itemName}/$itemId" $itemFields
        Assert ((Count $client "$kind plan updated") -eq 3) "$kind item edit notifies client"
        $null=Submit $t "/$controller/Details/$plan" "/$controller/Delete${itemName}/$itemId" @{}
        Assert ((Count $client "$kind plan updated") -eq 4) "$kind item deletion notifies client"
        $null=Submit $t "/$controller/Details/$plan" "/$controller/SetStatus/$plan" @{status=2}
        Assert ((Count $client "$kind plan updated") -eq 5) "$kind status change notifies client"
        $null=Submit $t "/$controller/Details/$plan" "/$controller/SetStatus/$plan" @{status=2}
        Assert ((Count $client "$kind plan updated") -eq 5) "Unchanged $kind status creates no notification"
    }
    $notice=Query 'SELECT MIN(Id) FROM Notifications WHERE UserId=@id' @{id=$client.Id}
    $unread=Query 'SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND IsRead=0' @{id=$client.Id}
    $page=Get-Page $c '/Notifications'
    Assert ($page.Content.Contains("aria-label=`"$unread unread notifications`">$unread</span>")) 'Navigation shows exact unread count'
    Assert ($page.Content.Contains('Unread') -and -not $page.Content.Contains('<script')) 'Inbox displays unread status without JavaScript'
    $page=Get-Page $o ('/Notifications?userId='+$client.Id)
    Assert (-not $page.Content.Contains('Appointment approved') -and $page.Content.Contains('No notifications yet.')) 'Forged user query cannot read another inbox'
    ExpectStatus 404 { Submit $o '/Notifications' "/Notifications/MarkRead/$notice" @{UserId=$client.Id} } 'Another user cannot mark read'
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Notifications/MarkRead/$notice" -WebSession $c -Method Post } 'Mark read requires antiforgery'
    ExpectStatus 404 { Get-Page $c "/Notifications/MarkRead/$notice" } 'GET cannot mark read'
    $page=Submit $c '/Notifications' "/Notifications/MarkRead/$notice" @{UserId=$other.Id;IsRead='false'}
    Assert ((Query 'SELECT IsRead FROM Notifications WHERE Id=@id' @{id=$notice}) -eq $true) 'Owner can mark read; posted owner/status ignored'
    $expected=$unread-1
    Assert ($page.Content.Contains("aria-label=`"$expected unread notifications`">$expected</span>") -and $page.Content.Contains('>Read</span>')) 'Read status and navigation count update'
    $null=Submit $c '/Notifications' "/Notifications/MarkRead/$notice" @{}
    Assert ((Query 'SELECT COUNT(*) FROM Notifications WHERE UserId=@id AND IsRead=0' @{id=$client.Id}) -eq $expected) 'Repeated mark read is idempotent'
    Write-Output "All $script:passed notification checks passed."
}
finally {
    $null=Query @'
DELETE FROM Appointments WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM WorkoutExercises WHERE WorkoutPlanId IN (SELECT Id FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix));
DELETE FROM DietMeals WHERE DietPlanId IN (SELECT Id FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix));
DELETE FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM TrainerClients WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM AspNetUsers WHERE Email LIKE @prefix;
'@ @{prefix=($prefix+'%')}
    $database.Dispose()
}


