# Read-only verification of the existing SQL Server schema. No migrations or data are changed.
$ErrorActionPreference='Stop'
$database=New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$database.Open()
function Values([string]$sql) {
    $command=$database.CreateCommand(); $command.CommandText=$sql
    $reader=$command.ExecuteReader()
    try { while($reader.Read()) { $reader.GetValue(0) } } finally { $reader.Dispose(); $command.Dispose() }
}
function Assert($condition,[string]$message) { if(-not $condition) { throw $message }; Write-Output "PASS: $message" }
try {
    $expectedMigrations=@(Get-ChildItem "$PSScriptRoot/../Migrations" -Filter '*.cs' | Where-Object { $_.Name -match '^\d+_[^.]+\.cs$' } | ForEach-Object { $_.BaseName })
    $applied=@(Values 'SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId')
    Assert (-not (Compare-Object $expectedMigrations $applied)) "All $($expectedMigrations.Count) migrations applied with no unknown migrations"
    $expectedTables=@('__EFMigrationsHistory','AspNetRoleClaims','AspNetRoles','AspNetUserClaims','AspNetUserLogins','AspNetUserRoles','AspNetUsers','AspNetUserTokens','Appointments','DietMeals','DietPlans','HealthMeasurements','HealthcareProfessionals','ReminderDeliveries','MealLogs','Notifications','TrainerClients','WorkoutExercises','WorkoutLogs','WorkoutPlans') | ForEach-Object { "dbo.$_" }
    $tables=@(Values "SELECT SCHEMA_NAME(schema_id)+'.'+name FROM sys.tables WHERE is_ms_shipped=0")
    Assert (-not (Compare-Object $expectedTables $tables)) 'Exactly the expected 20 tables exist; no duplicate feature tables'
    $foreignKeys=@(Values @'
SELECT OBJECT_NAME(f.parent_object_id)+'.'+COL_NAME(c.parent_object_id,c.parent_column_id)+'->'+OBJECT_NAME(f.referenced_object_id)
FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
'@)
    foreach($edge in @('Notifications.UserId->AspNetUsers','HealthMeasurements.ClientId->AspNetUsers','Appointments.ClientId->AspNetUsers','Appointments.TrainerId->AspNetUsers','TrainerClients.ClientId->AspNetUsers','TrainerClients.TrainerId->AspNetUsers','WorkoutPlans.ClientId->AspNetUsers','WorkoutPlans.TrainerId->AspNetUsers','DietPlans.ClientId->AspNetUsers','DietPlans.TrainerId->AspNetUsers','WorkoutExercises.WorkoutPlanId->WorkoutPlans','DietMeals.DietPlanId->DietPlans','WorkoutLogs.ClientId->AspNetUsers','WorkoutLogs.WorkoutPlanId->WorkoutPlans','WorkoutLogs.WorkoutExerciseId->WorkoutExercises','MealLogs.ClientId->AspNetUsers','MealLogs.DietPlanId->DietPlans')) {
        Assert ($foreignKeys -contains $edge) "Relationship $edge exists"
    }
    Assert ((Values 'SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1') -eq 0) 'All foreign keys enabled and trusted'
    Assert ((Values "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('TrainerClients') AND name='IX_TrainerClients_ClientId' AND is_unique=1 AND is_disabled=0") -eq 1) 'Unique client assignment index enforced'
    Assert ((Values "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('WorkoutLogs') AND referenced_object_id=OBJECT_ID('WorkoutExercises') AND delete_referential_action_desc='NO_ACTION'") -eq 1) 'Database prevents deletion of exercises referenced by history'
} finally { $database.Dispose() }
