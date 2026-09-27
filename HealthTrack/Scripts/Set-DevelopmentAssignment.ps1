param(
    [Parameter(Mandatory = $true)][string]$TrainerEmail,
    [Parameter(Mandatory = $true)][string]$ClientEmail,
    [switch]$PromoteTrainer
)

# Local coursework setup only. Register both accounts in the app first and log out.
# No passwords are accepted or changed. Production assignment management is out of scope.
$ErrorActionPreference = 'Stop'
$database = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=HealthTrackDB;Integrated Security=True;TrustServerCertificate=True')
$database.Open()
$transaction = $database.BeginTransaction()
try {
    $command = $database.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandText = @'
DECLARE @trainer nvarchar(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = UPPER(@trainerEmail) AND IsActive = 1);
DECLARE @client nvarchar(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = UPPER(@clientEmail) AND IsActive = 1);
DECLARE @trainerRole nvarchar(450) = (SELECT Id FROM AspNetRoles WHERE Name = 'Trainer');
DECLARE @clientRole nvarchar(450) = (SELECT Id FROM AspNetRoles WHERE Name = 'Client');
IF @trainer IS NULL OR @client IS NULL OR @trainerRole IS NULL OR @clientRole IS NULL
    THROW 50000, 'Register both active accounts and start the app to seed roles first.', 1;
IF @trainer = @client
    THROW 50000, 'Trainer and Client must be different accounts.', 1;
IF EXISTS (SELECT 1 FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId
           WHERE ur.UserId IN (@trainer, @client) AND r.Name = 'Admin')
    THROW 50000, 'Admin accounts cannot be used for this development assignment.', 1;
IF NOT EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @client AND RoleId = @clientRole)
   OR EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @client AND RoleId = @trainerRole)
    THROW 50000, 'The client must have only the Client role.', 1;
IF @promote = 1
BEGIN
    IF EXISTS (SELECT 1 FROM TrainerClients WHERE ClientId = @trainer)
        THROW 50000, 'An assigned client cannot be promoted by this setup script.', 1;
    DELETE FROM AspNetUserRoles WHERE UserId = @trainer AND RoleId = @clientRole;
    IF NOT EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @trainer AND RoleId = @trainerRole)
        INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@trainer, @trainerRole);
    UPDATE AspNetUsers SET SecurityStamp = CONVERT(nvarchar(36), NEWID()),
        ConcurrencyStamp = CONVERT(nvarchar(36), NEWID()) WHERE Id = @trainer;
END;
IF NOT EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @trainer AND RoleId = @trainerRole)
    THROW 50000, 'The trainer needs the Trainer role. For a test account, rerun with -PromoteTrainer.', 1;
IF EXISTS (SELECT 1 FROM TrainerClients WHERE ClientId = @client AND TrainerId <> @trainer)
    THROW 50000, 'This client is already assigned to another trainer. No reassignment was made.', 1;
IF NOT EXISTS (SELECT 1 FROM TrainerClients WHERE TrainerId = @trainer AND ClientId = @client)
    INSERT INTO TrainerClients (TrainerId, ClientId) VALUES (@trainer, @client);
'@
    [void]$command.Parameters.AddWithValue('@trainerEmail', $TrainerEmail.Trim())
    [void]$command.Parameters.AddWithValue('@clientEmail', $ClientEmail.Trim())
    [void]$command.Parameters.AddWithValue('@promote', [bool]$PromoteTrainer)
    [void]$command.ExecuteNonQuery()
    $transaction.Commit()
    Write-Output 'Development assignment saved. Log in again to refresh role claims.'
}
catch {
    $transaction.Rollback()
    throw
}
finally {
    $transaction.Dispose()
    $database.Dispose()
}
