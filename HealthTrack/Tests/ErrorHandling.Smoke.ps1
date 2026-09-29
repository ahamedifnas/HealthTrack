param([string]$BaseUrl = 'http://localhost:5093')

# Run against a separate Development instance started with:
# --Appointments:TimeZoneId SecurityReview-Invalid-TimeZone
# This forces a real service-resolution failure without changing database records or source.
$ErrorActionPreference = 'Stop'
foreach ($path in @('/', '/Account/Login', '/Home/Error')) {
    try {
        $null=Invoke-WebRequest -UseBasicParsing -Uri ($BaseUrl+$path)
        throw 'Expected HTTP 500 from the intentionally invalid test configuration.'
    }
    catch {
        if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 500) { throw }
        $reader=New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $html=$reader.ReadToEnd(); $reader.Dispose()
        if (-not $html.Contains('Request ID:') -or $html -match 'SecurityReview-Invalid|TimeZoneNotFoundException|StackTrace|DeveloperExceptionPage|ConnectionStrings|ASPNETCORE_ENVIRONMENT') {
            throw 'Unhandled exception response was not a safe generic error page.'
        }
        Write-Output "PASS: $path returns a friendly error without internal exception or configuration details"
    }
}
