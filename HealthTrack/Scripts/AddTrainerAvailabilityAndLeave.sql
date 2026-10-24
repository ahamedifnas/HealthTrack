BEGIN TRANSACTION;
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

CREATE TABLE [TrainerAvailabilityBreaks] (
    [Id] int NOT NULL IDENTITY,
    [TrainerAvailabilityId] int NOT NULL,
    [BreakStartTime] time(0) NOT NULL,
    [BreakEndTime] time(0) NOT NULL,
    [Description] nvarchar(100) NULL,
    CONSTRAINT [PK_TrainerAvailabilityBreaks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TrainerAvailabilityBreaks_TrainerAvailabilities_TrainerAvailabilityId] FOREIGN KEY ([TrainerAvailabilityId]) REFERENCES [TrainerAvailabilities] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_TrainerAvailabilities_TrainerId_DayOfWeek_IsActive] ON [TrainerAvailabilities] ([TrainerId], [DayOfWeek], [IsActive]);

CREATE INDEX [IX_TrainerAvailabilityBreaks_TrainerAvailabilityId] ON [TrainerAvailabilityBreaks] ([TrainerAvailabilityId]);

CREATE INDEX [IX_TrainerUnavailabilities_TrainerId_StartDate_EndDate_Status] ON [TrainerUnavailabilities] ([TrainerId], [StartDate], [EndDate], [Status]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020155447_AddTrainerAvailabilityAndLeave', N'10.0.9');

COMMIT;
GO

