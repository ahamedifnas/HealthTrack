IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927112856_InitialIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927112856_InitialIdentity', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE TABLE [DietPlans] (
        [Id] int NOT NULL IDENTITY,
        [ClientId] nvarchar(450) NOT NULL,
        [TrainerId] nvarchar(450) NOT NULL,
        [PlanName] nvarchar(100) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [DailyCalories] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DietPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DietPlans_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DietPlans_AspNetUsers_TrainerId] FOREIGN KEY ([TrainerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE TABLE [TrainerClients] (
        [TrainerId] nvarchar(450) NOT NULL,
        [ClientId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_TrainerClients] PRIMARY KEY ([TrainerId], [ClientId]),
        CONSTRAINT [FK_TrainerClients_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TrainerClients_AspNetUsers_TrainerId] FOREIGN KEY ([TrainerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE TABLE [WorkoutPlans] (
        [Id] int NOT NULL IDENTITY,
        [ClientId] nvarchar(450) NOT NULL,
        [TrainerId] nvarchar(450) NOT NULL,
        [PlanName] nvarchar(100) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WorkoutPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkoutPlans_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkoutPlans_AspNetUsers_TrainerId] FOREIGN KEY ([TrainerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE TABLE [DietMeals] (
        [Id] int NOT NULL IDENTITY,
        [DietPlanId] int NOT NULL,
        [MealType] int NOT NULL,
        [FoodName] nvarchar(150) NOT NULL,
        [Quantity] nvarchar(100) NOT NULL,
        [Calories] int NOT NULL,
        [Protein] decimal(10,2) NOT NULL,
        [Carbohydrates] decimal(10,2) NOT NULL,
        [Fat] decimal(10,2) NOT NULL,
        [Instructions] nvarchar(2000) NULL,
        CONSTRAINT [PK_DietMeals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DietMeals_DietPlans_DietPlanId] FOREIGN KEY ([DietPlanId]) REFERENCES [DietPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE TABLE [MealLogs] (
        [Id] int NOT NULL IDENTITY,
        [ClientId] nvarchar(450) NOT NULL,
        [DietPlanId] int NOT NULL,
        [MealDate] date NOT NULL,
        [MealType] int NOT NULL,
        [FoodName] nvarchar(150) NOT NULL,
        [Quantity] nvarchar(100) NOT NULL,
        [Calories] int NOT NULL,
        [Notes] nvarchar(2000) NULL,
        CONSTRAINT [PK_MealLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MealLogs_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MealLogs_DietPlans_DietPlanId] FOREIGN KEY ([DietPlanId]) REFERENCES [DietPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE TABLE [WorkoutExercises] (
        [Id] int NOT NULL IDENTITY,
        [WorkoutPlanId] int NOT NULL,
        [ExerciseName] nvarchar(100) NOT NULL,
        [Sets] int NOT NULL,
        [Repetitions] int NOT NULL,
        [DurationMinutes] int NOT NULL,
        [CaloriesTarget] int NOT NULL,
        [Instructions] nvarchar(2000) NULL,
        CONSTRAINT [PK_WorkoutExercises] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkoutExercises_WorkoutPlans_WorkoutPlanId] FOREIGN KEY ([WorkoutPlanId]) REFERENCES [WorkoutPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE TABLE [WorkoutLogs] (
        [Id] int NOT NULL IDENTITY,
        [ClientId] nvarchar(450) NOT NULL,
        [WorkoutPlanId] int NOT NULL,
        [WorkoutExerciseId] int NULL,
        [WorkoutDate] date NOT NULL,
        [DurationMinutes] int NOT NULL,
        [CaloriesBurned] int NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [Completed] bit NOT NULL,
        CONSTRAINT [PK_WorkoutLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkoutLogs_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkoutLogs_WorkoutExercises_WorkoutExerciseId] FOREIGN KEY ([WorkoutExerciseId]) REFERENCES [WorkoutExercises] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_WorkoutLogs_WorkoutPlans_WorkoutPlanId] FOREIGN KEY ([WorkoutPlanId]) REFERENCES [WorkoutPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_DietMeals_DietPlanId] ON [DietMeals] ([DietPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_DietPlans_ClientId] ON [DietPlans] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_DietPlans_TrainerId] ON [DietPlans] ([TrainerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_MealLogs_ClientId_MealDate] ON [MealLogs] ([ClientId], [MealDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_MealLogs_DietPlanId] ON [MealLogs] ([DietPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TrainerClients_ClientId] ON [TrainerClients] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_WorkoutExercises_WorkoutPlanId] ON [WorkoutExercises] ([WorkoutPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_WorkoutLogs_ClientId_WorkoutDate] ON [WorkoutLogs] ([ClientId], [WorkoutDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_WorkoutLogs_WorkoutExerciseId] ON [WorkoutLogs] ([WorkoutExerciseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_WorkoutLogs_WorkoutPlanId] ON [WorkoutLogs] ([WorkoutPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_WorkoutPlans_ClientId] ON [WorkoutPlans] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    CREATE INDEX [IX_WorkoutPlans_TrainerId] ON [WorkoutPlans] ([TrainerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160418_WorkoutAndDietManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927160418_WorkoutAndDietManagement', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927162051_AppointmentSystem'
)
BEGIN
    CREATE TABLE [Appointments] (
        [Id] int NOT NULL IDENTITY,
        [ClientId] nvarchar(450) NOT NULL,
        [TrainerId] nvarchar(450) NOT NULL,
        [AppointmentDate] date NOT NULL,
        [AppointmentTime] time(0) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Appointments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Appointments_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Appointments_AspNetUsers_TrainerId] FOREIGN KEY ([TrainerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927162051_AppointmentSystem'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Appointments_ClientId_AppointmentDate_AppointmentTime] ON [Appointments] ([ClientId], [AppointmentDate], [AppointmentTime]) WHERE [Status] IN (1, 2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927162051_AppointmentSystem'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Appointments_TrainerId_AppointmentDate_AppointmentTime] ON [Appointments] ([TrainerId], [AppointmentDate], [AppointmentTime]) WHERE [Status] IN (1, 2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927162051_AppointmentSystem'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927162051_AppointmentSystem', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928073603_HealthMeasurements'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TargetWeight] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928073603_HealthMeasurements'
)
BEGIN
    CREATE TABLE [HealthMeasurements] (
        [Id] int NOT NULL IDENTITY,
        [ClientId] nvarchar(450) NOT NULL,
        [MeasurementDate] date NOT NULL,
        [WeightKg] decimal(5,2) NOT NULL,
        [HeightCm] decimal(5,2) NOT NULL,
        [BMI] decimal(7,2) NOT NULL,
        [BodyFatPercentage] decimal(5,2) NULL,
        [HeartRate] int NULL,
        [CaloriesBurned] int NOT NULL,
        [Notes] nvarchar(2000) NULL,
        CONSTRAINT [PK_HealthMeasurements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HealthMeasurements_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928073603_HealthMeasurements'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HealthMeasurements_ClientId_MeasurementDate] ON [HealthMeasurements] ([ClientId], [MeasurementDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928073603_HealthMeasurements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928073603_HealthMeasurements', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928081242_TrainerClientAssignedAt'
)
BEGIN
    ALTER TABLE [TrainerClients] ADD [AssignedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928081242_TrainerClientAssignedAt'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928081242_TrainerClientAssignedAt', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928145118_Notifications'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [Type] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsRead] bit NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928145118_Notifications'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAt] ON [Notifications] ([UserId], [IsRead], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928145118_Notifications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928145118_Notifications', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    DROP INDEX [IX_Appointments_TrainerId_AppointmentDate_AppointmentTime] ON [Appointments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Appointments]') AND [c].[name] = N'TrainerId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Appointments] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Appointments] ALTER COLUMN [TrainerId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    ALTER TABLE [Appointments] ADD [HealthcareProfessionalId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    CREATE TABLE [HealthcareProfessionals] (
        [UserId] nvarchar(450) NOT NULL,
        [Specialty] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_HealthcareProfessionals] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_HealthcareProfessionals_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Appointments_HealthcareProfessionalId_AppointmentDate_AppointmentTime] ON [Appointments] ([HealthcareProfessionalId], [AppointmentDate], [AppointmentTime]) WHERE [Status] IN (1, 2) AND [HealthcareProfessionalId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Appointments_TrainerId_AppointmentDate_AppointmentTime] ON [Appointments] ([TrainerId], [AppointmentDate], [AppointmentTime]) WHERE [Status] IN (1, 2) AND [TrainerId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    EXEC(N'ALTER TABLE [Appointments] ADD CONSTRAINT [CK_Appointment_ExactlyOneProvider] CHECK (([TrainerId] IS NOT NULL AND [HealthcareProfessionalId] IS NULL) OR ([TrainerId] IS NULL AND [HealthcareProfessionalId] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    ALTER TABLE [Appointments] ADD CONSTRAINT [FK_Appointments_HealthcareProfessionals_HealthcareProfessionalId] FOREIGN KEY ([HealthcareProfessionalId]) REFERENCES [HealthcareProfessionals] ([UserId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133007_HealthcareProfessionalAppointments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007133007_HealthcareProfessionalAppointments', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133224_ReminderDeliveryLedger'
)
BEGIN
    CREATE TABLE [ReminderDeliveries] (
        [UserId] nvarchar(450) NOT NULL,
        [ReminderKey] nvarchar(120) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ReminderDeliveries] PRIMARY KEY ([UserId], [ReminderKey]),
        CONSTRAINT [FK_ReminderDeliveries_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007133224_ReminderDeliveryLedger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007133224_ReminderDeliveryLedger', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120739_ArchiveWorkoutExercises'
)
BEGIN
    ALTER TABLE [WorkoutLogs] DROP CONSTRAINT [FK_WorkoutLogs_WorkoutExercises_WorkoutExerciseId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120739_ArchiveWorkoutExercises'
)
BEGIN
    ALTER TABLE [WorkoutExercises] ADD [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120739_ArchiveWorkoutExercises'
)
BEGIN
    ALTER TABLE [WorkoutLogs] ADD CONSTRAINT [FK_WorkoutLogs_WorkoutExercises_WorkoutExerciseId] FOREIGN KEY ([WorkoutExerciseId]) REFERENCES [WorkoutExercises] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008120739_ArchiveWorkoutExercises'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008120739_ArchiveWorkoutExercises', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008184508_ClientRegistrationEmailOtp'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [RequiresRegistrationEmailVerification] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008184508_ClientRegistrationEmailOtp'
)
BEGIN
    CREATE TABLE [EmailVerificationOtps] (
        [UserId] nvarchar(450) NOT NULL,
        [OtpHash] nvarchar(512) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [LastSentAt] datetimeoffset NULL,
        [IsUsed] bit NOT NULL,
        [AttemptCount] int NOT NULL,
        CONSTRAINT [PK_EmailVerificationOtps] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_EmailVerificationOtps_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008184508_ClientRegistrationEmailOtp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008184508_ClientRegistrationEmailOtp', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE TABLE [MembershipPackages] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [PriceLKR] decimal(18,2) NOT NULL,
        [DurationDays] int NOT NULL,
        [BenefitsJson] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MembershipPackages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE TABLE [PaymentTransactions] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] nvarchar(50) NOT NULL,
        [ClientId] nvarchar(450) NOT NULL,
        [MembershipPackageId] int NOT NULL,
        [PackageNameSnapshot] nvarchar(100) NOT NULL,
        [AmountLKR] decimal(18,2) NOT NULL,
        [DurationDaysSnapshot] int NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        [Status] int NOT NULL,
        [PayHerePaymentId] nvarchar(100) NULL,
        [PaymentMethod] nvarchar(50) NULL,
        [SignatureVerified] bit NOT NULL,
        [FailureReason] nvarchar(500) NULL,
        [RawPayload] nvarchar(4000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [PaidAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentTransactions_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentTransactions_MembershipPackages_MembershipPackageId] FOREIGN KEY ([MembershipPackageId]) REFERENCES [MembershipPackages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE TABLE [ClientSubscriptions] (
        [Id] int NOT NULL IDENTITY,
        [ClientId] nvarchar(450) NOT NULL,
        [MembershipPackageId] int NOT NULL,
        [PaymentTransactionId] int NOT NULL,
        [PackageNameSnapshot] nvarchar(100) NOT NULL,
        [AmountPaidLKR] decimal(18,2) NOT NULL,
        [DurationDaysSnapshot] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] int NOT NULL,
        [ActivatedAt] datetime2 NOT NULL,
        [RenewedAt] datetime2 NULL,
        CONSTRAINT [PK_ClientSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClientSubscriptions_AspNetUsers_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClientSubscriptions_MembershipPackages_MembershipPackageId] FOREIGN KEY ([MembershipPackageId]) REFERENCES [MembershipPackages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClientSubscriptions_PaymentTransactions_PaymentTransactionId] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransactions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE INDEX [IX_ClientSubscriptions_ClientId_Status_EndDate] ON [ClientSubscriptions] ([ClientId], [Status], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE INDEX [IX_ClientSubscriptions_MembershipPackageId] ON [ClientSubscriptions] ([MembershipPackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClientSubscriptions_PaymentTransactionId] ON [ClientSubscriptions] ([PaymentTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_ClientId] ON [PaymentTransactions] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_MembershipPackageId] ON [PaymentTransactions] ([MembershipPackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentTransactions_OrderId] ON [PaymentTransactions] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020075246_AddMembershipPackagesAndPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020075246_AddMembershipPackagesAndPayments', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020155447_AddTrainerAvailabilityAndLeave'
)
BEGIN
    CREATE TABLE [TrainerAvailabilities] (
        [Id] int NOT NULL IDENTITY,
        [TrainerId] nvarchar(450) NOT NULL,
        [DayOfWeek] int NOT NULL,
        [StartTime] time(0) NOT NULL,
        [EndTime] time(0) NOT NULL,
        [SlotDurationMinutes] int NOT NULL,
        [IsActive] bit NOT NULL,
        [EffectiveStartDate] date NULL,
        [EffectiveEndDate] date NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TrainerAvailabilities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrainerAvailabilities_AspNetUsers_TrainerId] FOREIGN KEY ([TrainerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020155447_AddTrainerAvailabilityAndLeave'
)
BEGIN
    CREATE TABLE [TrainerUnavailabilities] (
        [Id] int NOT NULL IDENTITY,
        [TrainerId] nvarchar(450) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [IsFullDay] bit NOT NULL,
        [StartTime] time(0) NULL,
        [EndTime] time(0) NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TrainerUnavailabilities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrainerUnavailabilities_AspNetUsers_TrainerId] FOREIGN KEY ([TrainerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020155447_AddTrainerAvailabilityAndLeave'
)
BEGIN
    CREATE TABLE [TrainerAvailabilityBreaks] (
        [Id] int NOT NULL IDENTITY,
        [TrainerAvailabilityId] int NOT NULL,
        [BreakStartTime] time(0) NOT NULL,
        [BreakEndTime] time(0) NOT NULL,
        [Description] nvarchar(100) NULL,
        CONSTRAINT [PK_TrainerAvailabilityBreaks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrainerAvailabilityBreaks_TrainerAvailabilities_TrainerAvailabilityId] FOREIGN KEY ([TrainerAvailabilityId]) REFERENCES [TrainerAvailabilities] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020155447_AddTrainerAvailabilityAndLeave'
)
BEGIN
    CREATE INDEX [IX_TrainerAvailabilities_TrainerId_DayOfWeek_IsActive] ON [TrainerAvailabilities] ([TrainerId], [DayOfWeek], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020155447_AddTrainerAvailabilityAndLeave'
)
BEGIN
    CREATE INDEX [IX_TrainerAvailabilityBreaks_TrainerAvailabilityId] ON [TrainerAvailabilityBreaks] ([TrainerAvailabilityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020155447_AddTrainerAvailabilityAndLeave'
)
BEGIN
    CREATE INDEX [IX_TrainerUnavailabilities_TrainerId_StartDate_EndDate_Status] ON [TrainerUnavailabilities] ([TrainerId], [StartDate], [EndDate], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020155447_AddTrainerAvailabilityAndLeave'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020155447_AddTrainerAvailabilityAndLeave', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020180623_AddSubscriptionAdminNotes'
)
BEGIN
    ALTER TABLE [ClientSubscriptions] ADD [AdminNotes] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020180623_AddSubscriptionAdminNotes'
)
BEGIN
    ALTER TABLE [ClientSubscriptions] ADD [LastModifiedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020180623_AddSubscriptionAdminNotes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020180623_AddSubscriptionAdminNotes', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021073641_AddDemoPaymentMarker'
)
BEGIN
    ALTER TABLE [PaymentTransactions] ADD [IsDemo] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021073641_AddDemoPaymentMarker'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261021073641_AddDemoPaymentMarker', N'10.0.9');
END;

COMMIT;
GO

