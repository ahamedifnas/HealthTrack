param([string]$BaseUrl = 'http://localhost:5075')

# Integration test for the default development LocalDB. Temporary accounts and appointments
# are removed in finally. Past appointments are simulated only on this test's own records.
$ErrorActionPreference = 'Stop'
$prefix = 'crudpdf-' + [Guid]::NewGuid().ToString('N')
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

$artifacts=Join-Path $PSScriptRoot '../bin/PdfChecks'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$script:pdfCases=@()
function CheckPdf($account,[string]$path,[string]$name,[string]$include='', [string]$exclude='') {
    $separator=if($path.Contains('?')) { '&' } else { '?' }
    $target=Join-Path $artifacts "$name.pdf"
    $response=Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path+$separator+'download=true') -WebSession $account.Session -OutFile $target -PassThru
    Assert ($response.Headers['Content-Type'] -match 'application/pdf') "$name returns application/pdf"
    $disposition=$response.Headers['Content-Disposition']
    Assert ($disposition -match '^attachment;' -and $disposition -match 'HealthTrack_.*\.pdf') "$name supplies attachment and meaningful PDF filename"
    $bytes=[IO.File]::ReadAllBytes($target)
    Assert ([Text.Encoding]::ASCII.GetString($bytes,0,5) -eq '%PDF-') "$name is a real PDF file"
    $script:pdfCases+=@{name=$name;path=$path;file=$target;email=$account.Email;include=$include;exclude=$exclude}
}
try {
    $admin=Register 'Admin';$trainer=Register 'Trainer';$client=Register 'Client';$other=Register 'Other';$doctor=Register 'Doctor'
    foreach($pair in @(@($admin,'Admin'),@($trainer,'Trainer'),@($doctor,'HealthcareProfessional'))) {
        $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name=@role' @{id=$pair[0].Id;role=$pair[1]};Login $pair[0]
    }
    $null=Query "INSERT INTO HealthcareProfessionals(UserId,Specialty) VALUES(@id,'Rehabilitation')" @{id=$doctor.Id}
    $null=Query 'INSERT INTO TrainerClients(TrainerId,ClientId,AssignedAt) VALUES(@t,@c,GETUTCDATE())' @{t=$trainer.Id;c=$client.Id}
    $c=$client.Session;$a=$admin.Session;$t=$trainer.Session;$h=$doctor.Session
    $page=Get-Page $a "/AdminHealthcareProfessionals/Details/$($doctor.Id)"
    Assert ($page.Content.Contains('Rehabilitation') -and $page.Content.Contains('/Admin/Delete/')) 'Healthcare details shows specialty and safe deactivation link'
    Assert ((Get-Page $a "/AdminAssignments/Details/$($client.Id)").Content.Contains($trainer.Email)) 'Assignment details shows related accounts'
    $null=Get-Page $a "/AdminTrainers/Deactivate/$($trainer.Id)"
    Assert ((Query 'SELECT IsActive FROM AspNetUsers WHERE Id=@id' @{id=$trainer.Id})) 'Trainer deactivation GET is confirmation only'
    ExpectStatus 403 { Get-Page $c "/AdminHealthcareProfessionals/Details/$($doctor.Id)" } 'Client denied provider management details'
    $w=Query 'INSERT INTO WorkoutPlans(ClientId,TrainerId,PlanName,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES(@c,@t,@n,@start,@end,1,GETUTCDATE())' @{c=$client.Id;t=$trainer.Id;n='PDF Workout';start=$now.AddDays(-10).ToString('yyyy-MM-dd');end=$later}
    $d=Query 'INSERT INTO DietPlans(ClientId,TrainerId,PlanName,DailyCalories,StartDate,EndDate,Status,CreatedAt) OUTPUT INSERTED.Id VALUES(@c,@t,@n,2000,@start,@end,1,GETUTCDATE())' @{c=$client.Id;t=$trainer.Id;n='PDF Diet';start=$now.AddDays(-10).ToString('yyyy-MM-dd');end=$later}
    foreach($entry in @(@($today,'PDF_IN_RANGE'),@($now.AddDays(-1).ToString('yyyy-MM-dd'),'PDF_OUT_RANGE'))) {
        $null=Query 'INSERT INTO HealthMeasurements(ClientId,MeasurementDate,WeightKg,HeightCm,BMI,CaloriesBurned,Notes) VALUES(@c,@date,70,175,22.86,100,@n); INSERT INTO WorkoutLogs(ClientId,WorkoutPlanId,WorkoutDate,DurationMinutes,CaloriesBurned,Completed,Notes) VALUES(@c,@w,@date,20,100,1,@n); INSERT INTO MealLogs(ClientId,DietPlanId,MealDate,MealType,FoodName,Quantity,Calories,Notes) VALUES(@c,@d,@date,1,@n,''1 cup'',200,@n); INSERT INTO Appointments(ClientId,TrainerId,AppointmentDate,AppointmentTime,Reason,Status,CreatedAt) VALUES(@c,@t,@date,''09:00'',@n,3,GETUTCDATE())' @{c=$client.Id;t=$trainer.Id;w=$w;d=$d;date=$entry[0];n=$entry[1]}
    }
    $null=Query "INSERT INTO Appointments(ClientId,HealthcareProfessionalId,AppointmentDate,AppointmentTime,Reason,Status,CreatedAt) VALUES(@c,@h,@date,'11:00','PDF_HEALTHCARE',3,GETUTCDATE())" @{c=$client.Id;h=$doctor.Id;date=$today}
    $long=('Detailed workout note. '*75)+'END_OF_LONG_NOTE'
    $null=Query 'UPDATE WorkoutLogs SET Notes=@n WHERE WorkoutPlanId=@w AND WorkoutDate=@date' @{n=('PDF_IN_RANGE '+$long);w=$w;date=$today}
    # Logs stay reportable when a trainer later changes the plan's scheduled dates.
    $null=Query 'UPDATE WorkoutPlans SET StartDate=@date WHERE Id=@w; UPDATE DietPlans SET StartDate=@date WHERE Id=@d' @{date=$tomorrow;w=$w;d=$d}
    $filter="FromDate=$today&ToDate=$today"
    foreach($kind in @('System','Users','Clients','Trainers','HealthcareProfessionals','Appointments','WorkoutAnalytics','DietAnalytics','Engagement')) {
        $path="/ReportDownloads/Admin?kind=$kind"
        if($kind -eq 'Appointments') { $path+="&$filter&Status=Completed&clientId=$($client.Id)" }
        CheckPdf $admin $path "Admin-$kind" $(if($kind -eq 'Appointments'){'PDF_IN_RANGE'}else{''}) $(if($kind -eq 'Appointments'){'PDF_OUT_RANGE'}else{''})
    }
    foreach($kind in @('Progress','Workout','Diet','Appointments')) {
        $path="/Reports/$($kind)?clientId=$($client.Id)&$filter"
        $page=Get-Page $t $path
        Assert ($page.Content.Contains('Download PDF') -and $page.Content.Contains('FromDate')) "Existing trainer $kind page exposes filtered download"
        CheckPdf $trainer "/ReportDownloads/Trainer?kind=$kind&clientId=$($client.Id)&$filter" "Trainer-$kind" $(if($kind -eq 'Progress'){''}else{'PDF_IN_RANGE'}) 'PDF_OUT_RANGE'
    }
    foreach($kind in @('Progress','Measurements','Workout','Diet','Appointments')) {
        CheckPdf $client "/ReportDownloads/Client?kind=$kind&$filter" "Client-$kind" 'PDF_IN_RANGE' 'PDF_OUT_RANGE'
    }
    CheckPdf $doctor "/ReportDownloads/Healthcare?$filter&Status=Completed" 'Healthcare-Appointments' 'PDF_HEALTHCARE' 'PDF_IN_RANGE'
    CheckPdf $client "/ReportDownloads/Client?kind=Workout&FromDate=1900-01-01&ToDate=1900-01-01" 'Empty-Workout' 'No records match' 'PDF_IN_RANGE'
    ExpectStatus 403 { Get-Page $c '/ReportDownloads/Admin?kind=Users&download=true' } 'Client denied admin PDF'
    ExpectStatus 403 { Get-Page $h "/ReportDownloads/Trainer?kind=Progress&clientId=$($client.Id)&download=true" } 'Healthcare denied trainer PDF'
    ExpectStatus 404 { Get-Page $other.Session "/ReportDownloads/Client?kind=Progress&clientId=$($client.Id)&download=true" } 'Client cannot request another client PDF'
    ExpectStatus 404 { Get-Page $t "/ReportDownloads/Trainer?kind=Progress&clientId=$($other.Id)&download=true" } 'Trainer cannot export an unassigned client'
    ExpectStatus 400 { Get-Page $c "/ReportDownloads/Client?kind=Progress&FromDate=$later&ToDate=$today&download=true" } 'Reversed PDF dates rejected'
    ExpectStatus 400 { Get-Page $c '/ReportDownloads/Client?kind=999&download=true' } 'Undefined report kind rejected'
    ExpectStatus 400 { Get-Page $c '/ReportDownloads/Client?kind=Appointments&Status=999&download=true' } 'Undefined appointment status rejected'
    ExpectStatus 400 { Get-Page $a "/ReportDownloads/Admin?kind=System&$filter&download=true" } 'Unsupported filter is rejected rather than silently ignored'
    # Validate retained CRUD ownership and safe history-preserving removal with these fixture records.
    $measurement=Query 'SELECT TOP(1) Id FROM HealthMeasurements WHERE ClientId=@id ORDER BY Id' @{id=$client.Id}
    ExpectStatus 404 { Submit $other.Session '/Measurements' "/Measurements/Delete/$measurement" @{} } 'Other client cannot delete measurement'
    $null=Get-Page $c "/Measurements/Delete/$measurement"
    Assert ((Query 'SELECT COUNT(*) FROM HealthMeasurements WHERE Id=@id' @{id=$measurement}) -eq 1) 'Measurement delete GET does not delete'
    $log=Query 'SELECT TOP(1) Id FROM WorkoutLogs WHERE ClientId=@id ORDER BY Id' @{id=$client.Id}
    ExpectStatus 404 { Submit $other.Session '/Workouts' "/Workouts/DeleteLog/$log" @{} } 'Other client cannot delete workout log'
    $meal=Query 'SELECT TOP(1) Id FROM MealLogs WHERE ClientId=@id ORDER BY Id' @{id=$client.Id}
    ExpectStatus 404 { Submit $other.Session '/Diets' "/Diets/DeleteLog/$meal" @{} } 'Other client cannot delete meal log'
    # Browser tools receive only throwaway test credentials and the expected PDF case list.
    $manifest=Join-Path $artifacts 'cases.json'; $script:pdfCases | ConvertTo-Json -Depth 5 | Set-Content $manifest
    $env:HEALTHTRACK_PDF_TEST_PASSWORD=$password
    & node "$PSScriptRoot/PdfDownload.Browser.cjs" $BaseUrl $manifest
    if($LASTEXITCODE -ne 0) { throw 'PDF browser verification failed' }
    # Complete the owner-side edit/delete paths after PDF fixtures have been checked.
    # The selected measurement may be yesterday; use its stored date to avoid the unique-day constraint.
    $measurementDate=Query 'SELECT CONVERT(varchar,MeasurementDate,23) FROM HealthMeasurements WHERE Id=@id' @{id=$measurement}
    $null=Submit $c "/Measurements/Edit/$measurement" "/Measurements/Edit/$measurement" @{MeasurementDate=$measurementDate;WeightKg=72;HeightCm=180;CaloriesBurned=150;Notes='Updated measurement'}
    Assert ((Query 'SELECT BMI FROM HealthMeasurements WHERE Id=@id' @{id=$measurement}) -eq 22.22) 'Measurement edit recalculates BMI from persisted height and weight'
    Assert ((Get-Page $c "/Measurements/Details/$measurement").Content.Contains('Updated measurement')) 'Measurement details reflects edit'
    ExpectStatus 404 { Get-Page $other.Session "/Measurements/Edit/$measurement" } 'Other client cannot edit measurement'
    $logDate=Query 'SELECT CONVERT(varchar,WorkoutDate,23) FROM WorkoutLogs WHERE Id=@id' @{id=$log}
    $null=Submit $c "/Workouts/EditLog/$log" "/Workouts/EditLog/$log" @{WorkoutPlanId=$w;WorkoutDate=$logDate;DurationMinutes=35;CaloriesBurned=160;Completed='false';Notes='Updated workout'}
    Assert ((Query 'SELECT DurationMinutes FROM WorkoutLogs WHERE Id=@id' @{id=$log}) -eq 35 -and -not (Query 'SELECT Completed FROM WorkoutLogs WHERE Id=@id' @{id=$log})) 'Workout log edit preserves historical date outside revised plan and updates completion'
    Assert ((Get-Page $c "/Workouts/LogDetails/$log").Content.Contains('Updated workout')) 'Workout log details reflects edit'
    ExpectStatus 404 { Get-Page $other.Session "/Workouts/EditLog/$log" } 'Other client cannot edit workout log'
    $mealDate=Query 'SELECT CONVERT(varchar,MealDate,23) FROM MealLogs WHERE Id=@id' @{id=$meal}
    $null=Submit $c "/Diets/EditLog/$meal" "/Diets/EditLog/$meal" @{DietPlanId=$d;MealDate=$mealDate;MealType=2;FoodName='Updated meal';Quantity='2 cups';Calories=350}
    Assert ((Query 'SELECT Calories FROM MealLogs WHERE Id=@id' @{id=$meal}) -eq 350) 'Meal log edit persists changes'
    Assert ((Get-Page $c "/Diets/LogDetails/$meal").Content.Contains('Updated meal')) 'Meal log details reflects edit'
    ExpectStatus 404 { Get-Page $other.Session "/Diets/EditLog/$meal" } 'Other client cannot edit meal log'
    $null=Submit $t '/TrainerWorkouts' "/TrainerWorkouts/SetStatus/$w" @{status=2}
    $notice=Query 'SELECT TOP(1) Id FROM Notifications WHERE UserId=@id' @{id=$client.Id}
    Assert ([bool]$notice) 'System creates client notifications'
    $null=Get-Page $c "/Notifications/Details/$notice"
    $null=Submit $c '/Notifications' "/Notifications/MarkRead/$notice" @{}
    Assert ((Query 'SELECT IsRead FROM Notifications WHERE Id=@id' @{id=$notice})) 'Notification mark-read persists'
    $null=Submit $c '/Notifications' "/Notifications/MarkUnread/$notice" @{}
    Assert (-not (Query 'SELECT IsRead FROM Notifications WHERE Id=@id' @{id=$notice})) 'Notification mark-unread persists'
    ExpectStatus 404 { Submit $other.Session '/Notifications' "/Notifications/Delete/$notice" @{} } 'Other client cannot delete notification'
    foreach($entry in @(@('Measurements','Delete',$measurement),@('Workouts','DeleteLog',$log),@('Diets','DeleteLog',$meal),@('Notifications','Delete',$notice))) {
        ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/$($entry[0])/$($entry[1])/$($entry[2])" -WebSession $c -Method Post } 'Owner delete requires antiforgery'
    }
    $null=Submit $t '/TrainerWorkouts' "/TrainerWorkouts/Delete/$w" @{}
    Assert ((Query 'SELECT Status FROM WorkoutPlans WHERE Id=@id' @{id=$w}) -eq 2 -and (Query 'SELECT COUNT(*) FROM WorkoutLogs WHERE WorkoutPlanId=@id' @{id=$w}) -eq 2) 'Deleting workout plan with logs deactivates and preserves history'
    $null=Submit $t '/TrainerDiets' "/TrainerDiets/Delete/$d" @{}
    Assert ((Query 'SELECT Status FROM DietPlans WHERE Id=@id' @{id=$d}) -eq 2 -and (Query 'SELECT COUNT(*) FROM MealLogs WHERE DietPlanId=@id' @{id=$d}) -eq 2) 'Deleting diet plan with logs deactivates and preserves history'
    $null=Submit $a '/AdminHealthcareProfessionals' "/Admin/Delete/$($doctor.Id)" @{}
    Assert (-not (Query 'SELECT IsActive FROM AspNetUsers WHERE Id=@id' @{id=$doctor.Id}) -and (Query 'SELECT COUNT(*) FROM Appointments WHERE HealthcareProfessionalId=@id' @{id=$doctor.Id}) -eq 1) 'Provider deactivation preserves consultation history'
    $null=Submit $a '/Dashboard/Admin' "/AdminTrainers/Deactivate/$($trainer.Id)" @{}
    Assert (-not (Query 'SELECT IsActive FROM AspNetUsers WHERE Id=@id' @{id=$trainer.Id})) 'Confirmed trainer deactivation works'
    foreach($entry in @(@('Measurements','Delete',$measurement,'HealthMeasurements'),@('Workouts','DeleteLog',$log,'WorkoutLogs'),@('Diets','DeleteLog',$meal,'MealLogs'),@('Notifications','Delete',$notice,'Notifications'))) {
        $null=Submit $c "/$($entry[0])" "/$($entry[0])/$($entry[1])/$($entry[2])" @{}
        Assert ((Query "SELECT COUNT(*) FROM $($entry[3]) WHERE Id=@id" @{id=$entry[2]}) -eq 0) "$($entry[0]) owner delete works"
    }
    Write-Output "All $script:passed CRUD/PDF HTTP checks passed; browser checks are recorded separately."
}
finally {
    Remove-Item Env:HEALTHTRACK_PDF_TEST_PASSWORD -ErrorAction SilentlyContinue
    $null=Query @"
DELETE FROM Appointments WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
DELETE FROM HealthMeasurements WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix);
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
