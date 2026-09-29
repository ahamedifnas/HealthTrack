param([string]$BaseUrl = 'http://localhost:5075')

# Integration test for the default development LocalDB. Temporary accounts and appointments
# are removed in finally. Past appointments are simulated only on this test's own records.
$ErrorActionPreference = 'Stop'
$prefix = 'security-' + [Guid]::NewGuid().ToString('N')
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
    $client=Register 'client'; $trainer=Register 'trainer'; $admin=Register 'admin'
    foreach($item in @(@($trainer,'Trainer'),@($admin,'Admin'))) {
        $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name=@role' @{id=$item[0].Id;role=$item[1]}
        Login $item[0]
    }
    $c=$client.Session; $t=$trainer.Session; $a=$admin.Session
    $null=Query 'INSERT INTO TrainerClients (TrainerId,ClientId) VALUES (@trainer,@client)' @{trainer=$trainer.Id;client=$client.Id}
    $anonymous=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    Assert ((Get-Page $anonymous '/css/site.css').StatusCode -eq 200) 'Anonymous presentation assets remain accessible'
    foreach($path in @('/Dashboard','/Measurements','/Workouts','/Diets','/Progress','/Clients','/Notifications','/Reports','/AdminReports/Print')) {
        Assert ((Get-Page $anonymous $path).Content.Contains('name="Password"')) "Anonymous $path requires login"
    }
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Home/Privacy" -WebSession $anonymous -Method Post } 'Global antiforgery applies beyond existing controller annotations'
    ExpectStatus 404 { Get-Page $a '/AdminTrainers/Edit' } 'Missing trainer ID returns 404 rather than an exception'
    $page=Get-Page $c '/Measurements'
    Assert ($page.Headers['Cache-Control'] -match 'no-store') 'Private health pages disable caching'
    try { $null=Get-Page $anonymous '/Home/Error'; throw 'Expected generic error response' }
    catch {
        if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 500) { throw }
        $reader=New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $html=$reader.ReadToEnd(); $reader.Dispose()
        Assert ($html.Contains('Request ID:') -and -not ($html -match 'Development Mode|StackTrace|SqlException|ConnectionStrings|ASPNETCORE_ENVIRONMENT')) 'Error page exposes only a request ID and friendly guidance'
    }
    $fields=@{FullName=' ';Email="$prefix-invalid@example.test";Password=$password;ConfirmPassword=$password}
    $null=Submit $anonymous '/Account/Register' '/Account/Register' $fields
    Assert ((Query 'SELECT COUNT(*) FROM AspNetUsers WHERE Email=@email' @{email=$fields.Email}) -eq 0) 'Whitespace-only full name rejected'
    $fields.FullName='Valid name'; $fields.Email='invalid-email'
    $page=Submit $anonymous '/Account/Register' '/Account/Register' $fields
    Assert ($page.Content.Contains('not a valid e-mail address')) 'Invalid email rejected server-side'
    foreach($invalid in @(@{WeightKg='-1'}, @{HeightCm='-1'}, @{CaloriesBurned='-1'})) {
        $measurement=@{MeasurementDate=$today;WeightKg='80';HeightCm='180';CaloriesBurned='100'}
        foreach($key in $invalid.Keys) { $measurement[$key]=$invalid[$key] }
        $null=Submit $c '/Measurements/Create' '/Measurements/Create' $measurement
        Assert ((Query 'SELECT COUNT(*) FROM HealthMeasurements WHERE ClientId=@id' @{id=$client.Id}) -eq 0) "Negative $($invalid.Keys) rejected without saving"
    }
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Measurements/TargetWeight" -WebSession $c -Method Post -Body @{TargetWeight='70';__RequestVerificationToken='tampered'} } 'Tampered antiforgery token rejected'
    foreach($kind in @('Workout','Diet')) {
        $controller="Trainer${kind}s"
        $fields=@{ClientId=$client.Id;PlanName="$prefix $kind";DailyCalories=2000}
        $null=Submit $t "/$controller/Create" "/$controller/Create" $fields
        Assert ((Query "SELECT COUNT(*) FROM ${kind}Plans WHERE ClientId=@id" @{id=$client.Id}) -eq 0) "Omitted $kind plan dates rejected"
        $fields.StartDate=$today; $fields.EndDate=$later
        $null=Submit $t "/$controller/Create" "/$controller/Create" $fields
        $plan=Query "SELECT Id FROM ${kind}Plans WHERE ClientId=@id" @{id=$client.Id}
        Assert ($null -ne $plan) "Valid $kind plan remains usable"
        $originalStatus=Query "SELECT Status FROM ${kind}Plans WHERE Id=@id" @{id=$plan}
        foreach($invalidStatus in @(@{}, @{status='invalid'}, @{status='999'}, @{status=''})) {
            ExpectStatus 400 { Submit $t "/$controller/Details/$plan" "/$controller/SetStatus/$plan" $invalidStatus } "Missing or invalid $kind status rejected"
            Assert ((Query "SELECT Status FROM ${kind}Plans WHERE Id=@id" @{id=$plan}) -eq $originalStatus) "$kind status unchanged after invalid request"
        }
        if($kind -eq 'Workout') {
            $null=Submit $c "/Workouts/Log?planId=$plan" '/Workouts/Log' @{WorkoutPlanId=$plan;DurationMinutes=10;CaloriesBurned=20}
            Assert ((Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 0) 'Omitted workout log date rejected'
        } else {
            $null=Submit $c "/Diets/Log?planId=$plan" '/Diets/Log' @{DietPlanId=$plan;MealType=1;FoodName='Food';Quantity='1 cup';Calories=20}
            Assert ((Query 'SELECT COUNT(*) FROM MealLogs WHERE ClientId=@id' @{id=$client.Id}) -eq 0) 'Omitted meal log date rejected'
        }
    }
    $null=Query "INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name='Trainer'" @{id=$client.Id}
    ExpectStatus 404 { Get-Page $t "/Progress?clientId=$($client.Id)" } 'Assigned account promoted to Trainer is no longer exposed as Client data'
    ExpectStatus 403 { Get-Page $c '/Measurements' } 'Existing Client cookie immediately reflects added Trainer role'
    $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id AND RoleId IN (SELECT Id FROM AspNetRoles WHERE Name='Trainer')" @{id=$client.Id}
    Assert ((Get-Page $c '/Measurements').StatusCode -eq 200) 'Restored Client role works without logging in again'
    $null=Query "DELETE FROM AspNetUserRoles WHERE UserId=@id" @{id=$trainer.Id}
    ExpectStatus 403 { Get-Page $t '/Clients' } 'Revoked Trainer role takes effect on next request'
    $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id' @{id=$admin.Id}
    ExpectStatus 403 { Get-Page $a '/Dashboard/Admin' } 'Revoked Admin role takes effect on next request'
    $null=Query 'UPDATE AspNetUsers SET SecurityStamp=CONVERT(nvarchar(36),NEWID()) WHERE Id=@id' @{id=$client.Id}
    Assert ((Get-Page $c '/Measurements').Content.Contains('name="Password"')) 'Security stamp change immediately invalidates old cookie'
    Login $client
    $null=Query 'UPDATE AspNetUsers SET IsActive=0 WHERE Id=@id' @{id=$client.Id}
    Assert ((Get-Page $client.Session '/Measurements').Content.Contains('name="Password"')) 'Inactive account loses existing authenticated access immediately'
    $page=Submit $anonymous '/Account/Login' '/Account/Login' @{Email=$client.Email;Password=$password}
    Assert ($page.Content.Contains('Unable to sign in')) 'Inactive login remains blocked'
    Write-Output "All $script:passed security hardening checks passed."
}
finally {
    $null=Query @'
DELETE FROM WorkoutLogs WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM MealLogs WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM HealthMeasurements WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM WorkoutPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM DietPlans WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM TrainerClients WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM AspNetUsers WHERE Email LIKE @prefix;
'@ @{prefix=($prefix+'%')}
    $database.Dispose()
}


