param([string]$BaseUrl = 'http://localhost:5089')

# Uses the local development database. All uniquely named test accounts are removed afterward.
$ErrorActionPreference='Stop'
$prefix='health-progress-' + [Guid]::NewGuid().ToString('N')
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
    foreach ($name in @('client','other','trainer','admin')) {
        $email="$prefix-$name@example.test"
        $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $null=Submit $session '/Account/Register' '/Account/Register' @{FullName="$name $prefix";Email=$email;Password=$password;ConfirmPassword=$password}
        $id=Query 'SELECT Id FROM AspNetUsers WHERE Email=@email' @{email=$email}
        if ($name -in @('trainer','admin')) {
            $role=if ($name -eq 'trainer') {'Trainer'} else {'Admin'}
            $null=Query 'DELETE FROM AspNetUserRoles WHERE UserId=@id; INSERT INTO AspNetUserRoles SELECT @id,Id FROM AspNetRoles WHERE Name=@role' @{id=$id;role=$role}
        }
        $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $null=Submit $session '/Account/Login' '/Account/Login' @{Email=$email;Password=$password}
        $accounts[$name]=@{Id=$id;Session=$session}
    }
    $client=$accounts.client.Session; $other=$accounts.other.Session; $trainer=$accounts.trainer.Session; $admin=$accounts.admin.Session
    $clientId=$accounts.client.Id; $otherId=$accounts.other.Id; $trainerId=$accounts.trainer.Id
    $null=Query 'INSERT INTO TrainerClients (TrainerId,ClientId) VALUES (@trainer,@client)' @{trainer=$trainerId;client=$clientId}
    $zone=[TimeZoneInfo]::FindSystemTimeZoneById('Sri Lanka Standard Time')
    $today=[TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow,$zone).Date
    Assert ((Get-Page $client '/Progress').Content.Contains('No measurements yet')) 'Empty progress is supported'
    Assert ((Get-Page $client '/Measurements').Content.Contains('No measurements yet')) 'Empty history is supported'
    $fields=@{MeasurementDate=$today.ToString('yyyy-MM-dd');WeightKg='80';HeightCm='200';CaloriesBurned='300';Notes="$prefix-own";ClientId=$otherId;BMI='999'}
    foreach ($invalid in @(@{WeightKg='0'},@{HeightCm='0'},@{HeightCm='301'},@{WeightKg='501'},@{CaloriesBurned='-1'},@{BodyFatPercentage='101'},@{HeartRate='301'},@{MeasurementDate=$today.AddDays(1).ToString('yyyy-MM-dd')},@{Notes=('x'*2001)},@{WeightKg=''},@{MeasurementDate=''})) {
        $bad=$fields.Clone(); foreach ($key in $invalid.Keys) { $bad[$key]=$invalid[$key] }
        $null=Submit $client '/Measurements/Create' '/Measurements/Create' $bad
        Assert ((Query 'SELECT COUNT(*) FROM HealthMeasurements WHERE ClientId=@id' @{id=$clientId}) -eq 0) ('Invalid '+($invalid.Keys -join ',')+' rejected')
    }
    ExpectStatus 400 { Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/Measurements/Create" -WebSession $client -Method Post -Body $fields } 'Measurement requires antiforgery token'
    $page=Submit $client '/Measurements/Create' '/Measurements/Create' $fields
    Assert ($page.Content.Contains("$prefix-own")) 'Client adds and sees own measurement'
    Assert ((Query 'SELECT BMI FROM HealthMeasurements WHERE ClientId=@id' @{id=$clientId}) -eq 20) 'BMI calculated by server from weight and height despite forged BMI'
    Assert ((Query 'SELECT COUNT(*) FROM HealthMeasurements WHERE ClientId=@id' @{id=$otherId}) -eq 0) 'Forged ClientId cannot write another client measurement'
    $page=Submit $client '/Measurements/Create' '/Measurements/Create' $fields
    Assert ($page.Content.Contains('already exists') -and (Query 'SELECT COUNT(*) FROM HealthMeasurements WHERE ClientId=@id' @{id=$clientId}) -eq 1) 'Duplicate daily measurement rejected'
    $fields.MeasurementDate=$today.AddDays(-6).ToString('yyyy-MM-dd'); $fields.WeightKg='90'; $fields.CaloriesBurned='200'; $fields.Notes='Older'
    $null=Submit $client '/Measurements/Create' '/Measurements/Create' $fields
    $fields.MeasurementDate=$today.AddDays(-7).ToString('yyyy-MM-dd'); $fields.WeightKg='95'; $fields.CaloriesBurned='900'
    $null=Submit $client '/Measurements/Create' '/Measurements/Create' $fields
    $null=Submit $client '/Measurements/TargetWeight' '/Measurements/TargetWeight' @{TargetWeight='70';ClientId=$otherId}
    Assert ((Query 'SELECT TargetWeight FROM AspNetUsers WHERE Id=@id' @{id=$clientId}) -eq 70) 'Client target saved'
    Assert ((Query 'SELECT COUNT(*) FROM AspNetUsers WHERE Id=@id AND TargetWeight IS NULL' @{id=$otherId}) -eq 1) 'Forged ID cannot change another target'
    $null=Submit $client '/Measurements/TargetWeight' '/Measurements/TargetWeight' @{TargetWeight='0'}
    Assert ((Query 'SELECT TargetWeight FROM AspNetUsers WHERE Id=@id' @{id=$clientId}) -eq 70) 'Invalid target rejected'
    $page=Get-Page $client '/Dashboard/Client'
    Assert ($page.Content.Contains('80 kg') -and $page.Content.Contains('20.00') -and $page.Content.Contains('Not available yet') -and $page.Content.Contains('70 kg')) 'Dashboard uses latest measurement and target; measurement calories do not substitute for workout data'
    $page=Get-Page $client '/Progress'
    Assert ($page.Content.Contains('id="progress-table"') -and $page.Content.Contains('BMI change') -and $page.Content.Contains('Calories burned (kcal)')) 'Progress rendered as a Razor table'
    $table=[regex]::Match($page.Content,'<table[^>]*id="progress-table"[\s\S]*?</table>').Value
    Assert ($table.IndexOf('>95<') -lt $table.IndexOf('>90<') -and $table.IndexOf('>90<') -lt $table.IndexOf('>80<')) 'Progress rows ordered by measurement date'
    Assert ($table.Contains('>-5<') -and $table.Contains('>-10<') -and $table.Contains('>-2.50<')) 'C# calculates changes between recorded measurements'
    Assert (-not $page.Content.Contains('<script') -and -not $page.Content.Contains('<canvas')) 'Progress requires no JavaScript or canvas'
    $fields.MeasurementDate=$today.ToString('yyyy-MM-dd');$fields.Notes="$prefix-private-other"
    $null=Submit $other '/Measurements/Create' '/Measurements/Create' $fields
    ExpectStatus 404 { Get-Page $client "/Progress?clientId=$otherId" } 'Client cannot read another progress via query ID'
    $page=Get-Page $client "/Measurements/Index/$otherId`?clientId=$otherId"
    Assert (-not $page.Content.Contains("$prefix-private-other")) 'Forged history route and query IDs do not expose another client'
    $page=Get-Page $trainer "/Progress?clientId=$clientId"
    Assert ($page.Content.Contains("$prefix-own") -and -not $page.Content.Contains("$prefix-private-other")) 'Trainer sees assigned client progress only'
    ExpectStatus 404 { Get-Page $trainer "/Progress?clientId=$otherId" } 'Trainer cannot read unassigned client progress'
    $null=Query 'DELETE FROM TrainerClients WHERE TrainerId=@trainer AND ClientId=@client' @{trainer=$trainerId;client=$clientId}
    ExpectStatus 404 { Get-Page $trainer "/Progress?clientId=$clientId" } 'Assignment revocation removes progress access immediately'
    foreach ($session in @($trainer,$admin)) {
        foreach ($path in @('/Measurements','/Measurements/Create','/Measurements/TargetWeight','/Dashboard/Client')) {
            ExpectStatus 403 { Get-Page $session $path } "Non-client cannot access $path"
        }
        ExpectStatus 403 { Submit $session '/Account/Login' '/Measurements/Create' $fields } 'Non-client cannot submit measurements'
        ExpectStatus 403 { Submit $session '/Account/Login' '/Measurements/TargetWeight' @{TargetWeight='60'} } 'Non-client cannot change target'
    }
    ExpectStatus 403 { Get-Page $admin "/Progress?clientId=$clientId" } 'Admin cannot read individual progress'
    $page=Get-Page $admin '/Progress/Summary'
    Assert ($page.Content.Contains('Total measurements') -and -not $page.Content.Contains("$prefix-own") -and -not $page.Content.Contains("$prefix-private-other")) 'Admin receives aggregate statistics only'
    ExpectStatus 403 { Get-Page $client '/Progress/Summary' } 'Client cannot access Admin summary'
    ExpectStatus 403 { Get-Page $trainer '/Progress/Summary' } 'Trainer cannot access Admin summary'
    $anonymous=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    Assert ((Get-Page $anonymous '/Progress').Content.Contains('name="Password"')) 'Anonymous progress requires login'
    Write-Output "All $script:passed health progress checks passed."
}
finally {
    $null=Query 'DELETE FROM HealthMeasurements WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM TrainerClients WHERE ClientId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix) OR TrainerId IN (SELECT Id FROM AspNetUsers WHERE Email LIKE @prefix); DELETE FROM AspNetUsers WHERE Email LIKE @prefix' @{prefix=($prefix+'%')}
    $database.Dispose()
}

