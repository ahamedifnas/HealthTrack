$settings = Get-Content -Raw "$PSScriptRoot\..\appsettings.json" | ConvertFrom-Json
$connStr = $settings.ConnectionStrings.DefaultConnection
$sql = Get-Content -Raw "$PSScriptRoot\AddSubscriptionAdminNotes.sql"

$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
$cmd.ExecuteNonQuery()
$conn.Close()

Write-Output "Phase 5 DB Migration Applied Successfully!"
