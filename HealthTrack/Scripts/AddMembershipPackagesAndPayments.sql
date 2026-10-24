BEGIN TRANSACTION;

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

CREATE INDEX [IX_ClientSubscriptions_ClientId_Status_EndDate] ON [ClientSubscriptions] ([ClientId], [Status], [EndDate]);

CREATE INDEX [IX_ClientSubscriptions_MembershipPackageId] ON [ClientSubscriptions] ([MembershipPackageId]);

CREATE UNIQUE INDEX [IX_ClientSubscriptions_PaymentTransactionId] ON [ClientSubscriptions] ([PaymentTransactionId]);

CREATE INDEX [IX_PaymentTransactions_ClientId] ON [PaymentTransactions] ([ClientId]);

CREATE INDEX [IX_PaymentTransactions_MembershipPackageId] ON [PaymentTransactions] ([MembershipPackageId]);

CREATE UNIQUE INDEX [IX_PaymentTransactions_OrderId] ON [PaymentTransactions] ([OrderId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020075246_AddMembershipPackagesAndPayments', N'10.0.9');

COMMIT;
GO
