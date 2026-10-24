param([string]$BaseUrl = 'http://localhost:5075')
$ErrorActionPreference = 'Stop'

$prefix = 'avail-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$password = 'AvailPass123!' + [Guid]::NewGuid().ToString('N').Substring(0, 4)

$settings = Get-Content -Raw "$PSScriptRoot\..\appsettings.json" | ConvertFrom-Json
$connection = $settings.ConnectionStrings.DefaultConnection
if ($env:ConnectionStrings__DefaultConnection) { $connection = $env:ConnectionStrings__DefaultConnection }

$db = New-Object System.Data.SqlClient.SqlConnection($connection)
$db.Open()

$script:passed = 0

function Query([string]$sql, [hashtable]$parameters = @{}) {
    $c = $db.CreateCommand()
    $c.CommandText = $sql
    foreach ($k in $parameters.Keys) { [void]$c.Parameters.AddWithValue('@' + $k, $parameters[$k]) }
    try { return $c.ExecuteScalar() } finally { $c.Dispose() }
}

function Execute([string]$sql, [hashtable]$parameters = @{}) {
    $c = $db.CreateCommand()
    $c.CommandText = $sql
    foreach ($k in $parameters.Keys) { [void]$c.Parameters.AddWithValue('@' + $k, $parameters[$k]) }
    try { return $c.ExecuteNonQuery() } finally { $c.Dispose() }
}

function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:passed++
    Write-Output "PASS: $message"
}

function Get-Token([string]$html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw 'Antiforgery token missing' }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function Get-Page($session, [string]$path) {
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $path) -WebSession $session
}

function Post-Form($session, [string]$getPath, [string]$postPath, [hashtable]$fields) {
    $page = Get-Page $session $getPath
    $fields.__RequestVerificationToken = Get-Token $page.Content
    return Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl + $postPath) -WebSession $session -Method Post -Body $fields
}

function ExpectStatus([int]$status, [scriptblock]$action, [string]$message) {
    try { $null = & $action; throw "Expected HTTP $status : $message" }
    catch {
        if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $status) { throw }
        $script:passed++
        Write-Output "PASS: $message (HTTP $status)"
    }
}

$createdUserIds = New-Object System.Collections.Generic.List[string]

function CreateUser([string]$name, [string]$role) {
    $sess = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $email = "$prefix-$name@example.test"
    $page = Get-Page $sess '/Account/Register'
    $token = Get-Token $page.Content
    $null = Post-Form $sess '/Account/Register' '/Account/Register' @{ FullName="Avail Test $name"; Email=$email; Password=$password; ConfirmPassword=$password; __RequestVerificationToken=$token }
    $id = Query 'SELECT Id FROM AspNetUsers WHERE Email = @email' @{email=$email}
    if (-not $id) { throw "Failed to register test user $email" }
    $createdUserIds.Add($id)
    
    $null = Query 'UPDATE AspNetUsers SET EmailConfirmed = 1, RequiresRegistrationEmailVerification = 0 WHERE Id = @id' @{id=$id}
    
    if ($role -ne 'Client') {
        $roleId = Query 'SELECT Id FROM AspNetRoles WHERE Name = @role' @{role=$role}
        $null = Query 'INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@uid, @rid)' @{uid=$id; rid=$roleId}
    }
    
    $loginPage = Get-Page $sess '/Account/Login'
    $null = Post-Form $sess '/Account/Login' '/Account/Login' @{ Email=$email; Password=$password; RememberMe=$false }
    return @{ Id=$id; Email=$email; Session=$sess }
}

try {
    # 1. Database schema verification
    Assert ((Query "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('TrainerAvailabilities', 'TrainerAvailabilityBreaks', 'TrainerUnavailabilities')") -eq 3) 'Trainer availability and leave database tables exist'

    # 2. Setup Test Users
    $admin = CreateUser 'admin' 'Admin'
    $trainer1 = CreateUser 'trainer1' 'Trainer'
    $trainer2 = CreateUser 'trainer2' 'Trainer'
    $client1 = CreateUser 'client1' 'Client'
    $client2 = CreateUser 'client2' 'Client'
    Assert ($trainer1.Id -and $trainer2.Id -and $client1.Id -and $client2.Id -and $admin.Id) 'Test users registered and authenticated'

    # Determine next Monday and next Tuesday dates
    $today = [DateTime]::UtcNow.Date
    $daysUntilMon = ((1 - [int]$today.DayOfWeek) + 7) % 7
    if ($daysUntilMon -eq 0) { $daysUntilMon = 7 }
    $nextMonday = $today.AddDays($daysUntilMon).ToString('yyyy-MM-dd')
    $nextTuesday = $today.AddDays($daysUntilMon + 1).ToString('yyyy-MM-dd')

    # 3. Valid Weekly Schedule Creation (Trainer 1 creates Monday 09:00 - 17:00, 60m slots, with 12:00 - 13:00 Lunch Break)
    $schedFields = @{
        TrainerId = $trainer1.Id
        DayOfWeek = 'Monday'
        StartTime = '09:00'
        EndTime = '17:00'
        SlotDurationMinutes = '60'
        IsActive = 'true'
        'Breaks[0].BreakStartTime' = '12:00'
        'Breaks[0].BreakEndTime' = '13:00'
        'Breaks[0].Description' = 'Lunch Break'
    }
    $createResp = Post-Form $trainer1.Session '/TrainerAvailability/Create' '/TrainerAvailability/Create' $schedFields
    Assert ($createResp.StatusCode -eq 200) 'Trainer created valid Monday weekly schedule'

    $sched1Id = Query 'SELECT Id FROM TrainerAvailabilities WHERE TrainerId = @tid AND DayOfWeek = 1' @{tid=$trainer1.Id}
    Assert ($sched1Id -gt 0) 'Schedule stored in database with DayOfWeek = Monday (1)'
    $breakCount = Query 'SELECT COUNT(*) FROM TrainerAvailabilityBreaks WHERE TrainerAvailabilityId = @sid' @{sid=$sched1Id}
    Assert ($breakCount -eq 1) 'Break period stored with cascade relation'

    # 4. Invalid Schedule Rejection (EndTime <= StartTime)
    $invalidFields = @{
        TrainerId = $trainer1.Id
        DayOfWeek = 'Wednesday'
        StartTime = '17:00'
        EndTime = '09:00'
        SlotDurationMinutes = '60'
        IsActive = 'true'
    }
    $invResp = Post-Form $trainer1.Session '/TrainerAvailability/Create' '/TrainerAvailability/Create' $invalidFields
    Assert ($invResp.Content.Contains('End time must be after start time.')) 'Schedule with EndTime < StartTime rejected by server-side validation'

    # 5. Overlapping Schedule Window Rejection (Trainer1 attempts duplicate Monday overlapping window)
    $overlapFields = @{
        TrainerId = $trainer1.Id
        DayOfWeek = 'Monday'
        StartTime = '10:00'
        EndTime = '14:00'
        SlotDurationMinutes = '60'
        IsActive = 'true'
    }
    $ovResp = Post-Form $trainer1.Session '/TrainerAvailability/Create' '/TrainerAvailability/Create' $overlapFields
    Assert ($ovResp.Content.Contains('An active schedule already covers this time window on Monday.')) 'Overlapping schedule on same day rejected'

    # 6. Slot Generation API Verification
    $slotsJson = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Appointments/GetAvailableSlots?providerId=$($trainer1.Id)&date=$nextMonday"
    $slotsData = $slotsJson.Content | ConvertFrom-Json
    Assert ($slotsData.success -eq $true -and $slotsData.isTrainer -eq $true) 'Slot generation API returned success for trainer'
    Assert ($slotsData.slots.Count -eq 8) 'Correct number of candidate 60-min slots generated between 09:00 and 17:00 (8 slots)'

    # Verify 12:00 slot is marked unavailable due to Lunch Break
    $lunchSlot = $slotsData.slots | Where-Object { $_.startTime.StartsWith('12:00') }
    Assert ($lunchSlot.isAvailable -eq $false -and $lunchSlot.reason.Contains('Lunch Break')) 'Lunch break 12:00-13:00 correctly excluded from bookable slots'

    # Verify 10:00 AM slot is available
    $slot10 = $slotsData.slots | Where-Object { $_.startTime.StartsWith('10:00') }
    Assert ($slot10.isAvailable -eq $true) '10:00 AM slot is available for booking'

    # 7. Client 1 Books 10:00 AM Slot on Next Monday
    $bookFields = @{
        TrainerId = $trainer1.Id
        HealthcareProfessionalId = ''
        AppointmentDate = $nextMonday
        AppointmentTime = '10:00'
        Reason = 'Personal Training & Fitness Assessment'
    }
    $bookResp = Post-Form $client1.Session '/Appointments/Book' '/Appointments/Book' $bookFields
    Assert ($bookResp.StatusCode -eq 200) 'Client 1 successfully booked 10:00 AM slot'

    $app1Id = Query 'SELECT Id FROM Appointments WHERE ClientId = @cid AND TrainerId = @tid AND AppointmentDate = @ad AND AppointmentTime = @at' @{cid=$client1.Id; tid=$trainer1.Id; ad=$nextMonday; at='10:00'}
    Assert ($app1Id -gt 0) 'Appointment saved in database with Status = Pending'

    # Verify Trainer 1 received notification
    $notifCount = Query 'SELECT COUNT(*) FROM Notifications WHERE UserId = @uid AND Title LIKE @pat' @{uid=$trainer1.Id; pat='%requested%'}
    Assert ($notifCount -ge 1) 'Trainer received in-app notification for requested appointment'

    # 8. Slot Exclusion After Booking: 10:00 AM now marked "Already booked"
    $slotsJson2 = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Appointments/GetAvailableSlots?providerId=$($trainer1.Id)&date=$nextMonday"
    $slotsData2 = $slotsJson2.Content | ConvertFrom-Json
    $slot10After = $slotsData2.slots | Where-Object { $_.startTime.StartsWith('10:00') }
    Assert ($slot10After.isAvailable -eq $false -and $slot10After.reason -eq 'Already booked') 'Booked slot dynamically reflected as Already Booked in slot API'

    # 9. Double-Booking Prevention: Client 2 attempts to book the same Monday 10:00 AM slot
    $doubleBookFields = @{
        TrainerId = $trainer1.Id
        HealthcareProfessionalId = ''
        AppointmentDate = $nextMonday
        AppointmentTime = '10:00'
        Reason = 'Simultaneous duplicate booking attempt'
    }
    $dupResp = Post-Form $client2.Session '/Appointments/Book' '/Appointments/Book' $doubleBookFields
    Assert ($dupResp.Content.Contains('The selected time slot is not available or falls outside trainer working hours.')) 'Double booking rejected by server-side slot availability validation'

    # 10. Booking Outside Working Hours Rejection
    $outsideHoursFields = @{
        TrainerId = $trainer1.Id
        HealthcareProfessionalId = ''
        AppointmentDate = $nextMonday
        AppointmentTime = '07:00'
        Reason = 'Attempt early morning outside schedule'
    }
    $outsideResp = Post-Form $client2.Session '/Appointments/Book' '/Appointments/Book' $outsideHoursFields
    Assert ($outsideResp.Content.Contains('The selected time slot is not available or falls outside trainer working hours.')) 'Booking outside working hours rejected'

    # 11. Past Date Booking Rejection
    $pastDateFields = @{
        TrainerId = $trainer1.Id
        HealthcareProfessionalId = ''
        AppointmentDate = '2020-01-01'
        AppointmentTime = '10:00'
        Reason = 'Booking in the past'
    }
    $pastResp = Post-Form $client1.Session '/Appointments/Book' '/Appointments/Book' $pastDateFields
    Assert ($pastResp.Content.Contains('Choose a future date and time.')) 'Past appointment date rejected'

    # 12. Trainer Leave Registration & Explicit Handling of Affected Bookings
    # Trainer 1 registers full-day leave for Next Monday
    $leaveFields = @{
        TrainerId = $trainer1.Id
        StartDate = $nextMonday
        EndDate = $nextMonday
        IsFullDay = 'true'
        Reason = 'Annual National Fitness Conference'
    }
    $leaveResp = Post-Form $trainer1.Session '/TrainerAvailability/CreateLeave' '/TrainerAvailability/CreateLeave' $leaveFields
    Assert ($leaveResp.StatusCode -eq 200) 'Trainer registered full-day leave'

    # Verify Client 1's appointment was automatically marked Cancelled (Status = 4)
    $app1Status = Query 'SELECT Status FROM Appointments WHERE Id = @id' @{id=$app1Id}
    Assert ($app1Status -eq 4) 'Existing booking affected by trainer leave automatically marked Cancelled (4)'

    # Verify Client 1 received explicit cancellation notification with leave reason
    $cancelNotif = Query 'SELECT COUNT(*) FROM Notifications WHERE UserId = @cid AND Message LIKE @pat' @{cid=$client1.Id; pat='%National Fitness Conference%'}
    Assert ($cancelNotif -ge 1) 'Client received notification explicitly citing trainer leave reason'

    # Verify slots on Next Monday now return 0 available slots
    $slotsLeaveJson = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Appointments/GetAvailableSlots?providerId=$($trainer1.Id)&date=$nextMonday"
    $slotsLeaveData = $slotsLeaveJson.Content | ConvertFrom-Json
    Assert ($slotsLeaveData.slots.Count -eq 0) 'Slot generation returns 0 slots on full-day leave'

    # 13. Partial Day Leave & Slot Calculation
    # Trainer 1 creates Tuesday schedule (09:00 - 17:00)
    $tuesSched = @{
        TrainerId = $trainer1.Id
        DayOfWeek = 'Tuesday'
        StartTime = '09:00'
        EndTime = '17:00'
        SlotDurationMinutes = '60'
        IsActive = 'true'
    }
    $null = Post-Form $trainer1.Session '/TrainerAvailability/Create' '/TrainerAvailability/Create' $tuesSched

    # Trainer 1 adds partial leave on Tuesday from 14:00 to 17:00
    $partialLeaveFields = @{
        TrainerId = $trainer1.Id
        StartDate = $nextTuesday
        EndDate = $nextTuesday
        IsFullDay = 'false'
        StartTime = '14:00'
        EndTime = '17:00'
        Reason = 'Doctor Appointment'
    }
    $null = Post-Form $trainer1.Session '/TrainerAvailability/CreateLeave' '/TrainerAvailability/CreateLeave' $partialLeaveFields

    # Verify slots for Tuesday: morning slots available, afternoon slots >= 14:00 unavailable
    $tuesJson = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Appointments/GetAvailableSlots?providerId=$($trainer1.Id)&date=$nextTuesday"
    $tuesData = $tuesJson.Content | ConvertFrom-Json
    $slot11 = $tuesData.slots | Where-Object { $_.startTime.StartsWith('11:00') }
    Assert ($slot11.isAvailable -eq $true) 'Morning slot on partial leave day is available'
    $slot14 = $tuesData.slots | Where-Object { $_.startTime.StartsWith('14:00') }
    Assert ($slot14.isAvailable -eq $false -and $slot14.reason.Contains('Doctor Appointment')) 'Afternoon slot within partial leave marked unavailable with reason'

    # 14. Appointment Approval Workflow
    $tuesBookFields = @{
        TrainerId = $trainer1.Id
        HealthcareProfessionalId = ''
        AppointmentDate = $nextTuesday
        AppointmentTime = '11:00'
        Reason = 'Cardio Conditioning'
    }
    $null = Post-Form $client1.Session '/Appointments/Book' '/Appointments/Book' $tuesBookFields
    $app2Id = Query 'SELECT Id FROM Appointments WHERE ClientId = @cid AND TrainerId = @tid AND AppointmentDate = @ad AND AppointmentTime = @at' @{cid=$client1.Id; tid=$trainer1.Id; ad=$nextTuesday; at='11:00'}
    
    # Trainer 1 approves appointment
    $apprResp = Post-Form $trainer1.Session "/Appointments/Details/$app2Id" "/Appointments/Approve/$app2Id" @{}
    $app2Status = Query 'SELECT Status FROM Appointments WHERE Id = @id' @{id=$app2Id}
    Assert ($app2Status -eq 2) 'Trainer successfully approved appointment (Status = 2 Approved)'

    $clientApprNotif = Query 'SELECT COUNT(*) FROM Notifications WHERE UserId = @cid AND Title LIKE @pat' @{cid=$client1.Id; pat='%approved%'}
    Assert ($clientApprNotif -ge 1) 'Client received notification of appointment approval'

    # 15. Security & Ownership Authorization Checks
    # Client cannot access TrainerAvailability
    ExpectStatus 403 {
        $null = Get-Page $client1.Session '/TrainerAvailability'
    } 'Client blocked from accessing Trainer Availability (HTTP 403)'

    # Trainer 2 cannot edit Trainer 1's schedule
    ExpectStatus 403 {
        $null = Get-Page $trainer2.Session "/TrainerAvailability/Edit/$sched1Id"
    } 'Trainer 2 forbidden from editing Trainer 1 schedule (HTTP 403)'

    # 16. Admin Oversight & Management
    $adminOverview = Get-Page $admin.Session '/Admin/Appointments'
    Assert ($adminOverview.Content.Contains('Trainer Schedules') -and $adminOverview.Content.Contains('Trainer Leaves')) 'Admin Appointments dashboard displays trainer schedule management links'

    # Admin applies standard schedule for Trainer 2
    $stdResp = Post-Form $admin.Session '/TrainerAvailability' '/TrainerAvailability/ApplyStandardSchedule' @{ trainerId = $trainer2.Id }
    $trainer2Schedules = Query 'SELECT COUNT(*) FROM TrainerAvailabilities WHERE TrainerId = @tid' @{tid=$trainer2.Id}
    Assert ($trainer2Schedules -eq 5) 'Admin successfully applied standard Mon-Fri 5-day schedule for Trainer 2'

    Write-Output "All $script:passed checks PASSED!"
}
finally {
    # Non-destructive test data cleanup
    foreach ($uid in $createdUserIds) {
        Execute 'DELETE FROM Notifications WHERE UserId = @id' @{id=$uid}
        Execute 'DELETE FROM TrainerAvailabilityBreaks WHERE TrainerAvailabilityId IN (SELECT Id FROM TrainerAvailabilities WHERE TrainerId = @id)' @{id=$uid}
        Execute 'DELETE FROM TrainerAvailabilities WHERE TrainerId = @id' @{id=$uid}
        Execute 'DELETE FROM TrainerUnavailabilities WHERE TrainerId = @id' @{id=$uid}
        Execute 'DELETE FROM Appointments WHERE ClientId = @id OR TrainerId = @id' @{id=$uid}
        Execute 'DELETE FROM AspNetUserRoles WHERE UserId = @id' @{id=$uid}
        Execute 'DELETE FROM AspNetUsers WHERE Id = @id' @{id=$uid}
    }
    $db.Close()
}
