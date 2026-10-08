param([string]$BaseUrl='http://localhost:5092', [Parameter(Mandatory=$true)][string]$PlaywrightPath)
$ErrorActionPreference='Stop'
$env:HEALTHTRACK_OTP_TEST_PREFIX='otp-browser-'+[Guid]::NewGuid().ToString('N')
$settings=Get-Content -Raw "$PSScriptRoot/../appsettings.json" | ConvertFrom-Json
$connection=$settings.ConnectionStrings.DefaultConnection
if($env:ConnectionStrings__DefaultConnection) { $connection=$env:ConnectionStrings__DefaultConnection }
$db=New-Object System.Data.SqlClient.SqlConnection($connection); $db.Open()
try {
    & node "$PSScriptRoot/RegistrationOtp.Browser.cjs" $BaseUrl $PlaywrightPath
    if($LASTEXITCODE -ne 0) { throw 'Registration OTP browser checks failed' }
    $c=$db.CreateCommand(); $c.CommandText='SELECT o.AttemptCount FROM EmailVerificationOtps o JOIN AspNetUsers u ON u.Id=o.UserId WHERE u.Email=@email'
    [void]$c.Parameters.AddWithValue('@email',$env:HEALTHTRACK_OTP_TEST_PREFIX+'-concurrency@example.test')
    if([int]$c.ExecuteScalar() -ne 5) { throw 'Concurrent attempts exceeded the database limit' }; $c.Dispose()
    Write-Output 'PASS: Database attempt count remains exactly five after concurrent requests'
} finally {
    $c=$db.CreateCommand(); $c.CommandText='DELETE FROM AspNetUsers WHERE Email LIKE @prefix'
    [void]$c.Parameters.AddWithValue('@prefix',$env:HEALTHTRACK_OTP_TEST_PREFIX+'%'); $null=$c.ExecuteNonQuery(); $c.Dispose(); $db.Dispose()
    Remove-Item Env:HEALTHTRACK_OTP_TEST_PREFIX
}
