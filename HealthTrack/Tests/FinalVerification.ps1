param([string]$BaseUrl = 'http://localhost:5092', [string]$Suites = 'Database,Phase1,TrainerManagement,HealthProgress,Phase3,Phase4,Assignments,DashboardPlans,DashboardUi,Notifications,Reports,Security,Coursework,CrudPdf,RemainingCrud')
$ErrorActionPreference = 'Stop'
$outputDirectory = Join-Path $PSScriptRoot '../Documentation/Test-Results'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$resultsPath = Join-Path $outputDirectory 'results.json'
$results = if (Test-Path $resultsPath) { @(Get-Content -Raw $resultsPath | ConvertFrom-Json) } else { @() }
foreach ($suite in $Suites.Split(',')) {
    Write-Output "Running $suite"
    $log = Join-Path $outputDirectory "$suite.txt"
    $ErrorActionPreference = 'Continue'
    & powershell -NoProfile -File "$PSScriptRoot/$suite.Smoke.ps1" -BaseUrl $BaseUrl > $log 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $content = Get-Content $log
    $assertions = @($content | Where-Object { $_ -match '^PASS:' }).Count
    if ($code -eq 0) { $content | Where-Object { $_ -match '^(PASS:|All )' } | Set-Content $log }
    $results = @($results | Where-Object { $_.Suite -ne $suite })
    $results += [pscustomobject]@{ Suite=$suite; ExitCode=$code; PassedAssertions=$assertions; ExecutedAtUtc=[DateTime]::UtcNow.ToString('o') }
    $results | ConvertTo-Json | Set-Content (Join-Path $outputDirectory 'results.json')
    Write-Output "$suite : exit $code; $assertions assertions passed"
}
$results | ConvertTo-Json | Set-Content (Join-Path $outputDirectory 'results.json')
if ($results.ExitCode | Where-Object { $_ -ne 0 }) { exit 1 }

