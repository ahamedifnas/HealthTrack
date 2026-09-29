param([string]$BaseUrl='http://localhost:5092', [string]$PlaywrightPath="$env:TEMP/healthtrack-browser-check/node_modules/playwright")
# Browser tooling only; application navigation uses native HTML and CSS, with scripts disabled here.
$ErrorActionPreference='Stop'
$env:HEALTHTRACK_TEST_PREFIX='browser-'+[Guid]::NewGuid().ToString('N')
try {
    & node "$PSScriptRoot/Navigation.Browser.cjs" $BaseUrl $PlaywrightPath
    if($LASTEXITCODE -ne 0) { throw 'Browser verification failed' }
} finally {
    $database=New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
    try {
        $database.Open()
        $command=$database.CreateCommand()
        $command.CommandText='DELETE FROM AspNetUsers WHERE Email IN (@client,@trainer)'
        [void]$command.Parameters.AddWithValue('@client',"$env:HEALTHTRACK_TEST_PREFIX-client@example.test")
        [void]$command.Parameters.AddWithValue('@trainer',"$env:HEALTHTRACK_TEST_PREFIX-trainer@example.test")
        [void]$command.ExecuteNonQuery()
        $command.Dispose()
    } finally { $database.Dispose(); Remove-Item Env:HEALTHTRACK_TEST_PREFIX }
}
