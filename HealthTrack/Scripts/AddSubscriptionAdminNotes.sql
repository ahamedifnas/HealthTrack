BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[ClientSubscriptions]') AND name = 'AdminNotes'
)
BEGIN
    ALTER TABLE [ClientSubscriptions] ADD [AdminNotes] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[ClientSubscriptions]') AND name = 'LastModifiedAt'
)
BEGIN
    ALTER TABLE [ClientSubscriptions] ADD [LastModifiedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory] 
    WHERE [MigrationId] = N'20261020180623_AddSubscriptionAdminNotes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020180623_AddSubscriptionAdminNotes', N'10.0.0-preview.1.25081.1');
END;

COMMIT;
