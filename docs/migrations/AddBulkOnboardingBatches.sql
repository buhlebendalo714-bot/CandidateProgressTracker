BEGIN TRANSACTION;
CREATE TABLE [OnboardingBatches] (
    [BatchId] uniqueidentifier NOT NULL,
    [CohortId] uniqueidentifier NOT NULL,
    [ActorId] nvarchar(450) NOT NULL,
    [StartDate] date NOT NULL,
    [Fingerprint] nvarchar(64) NOT NULL,
    [PayloadHash] nvarchar(64) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OnboardingBatches] PRIMARY KEY ([BatchId]),
    CONSTRAINT [FK_OnboardingBatches_AspNetUsers_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OnboardingBatches_Cohorts_CohortId] FOREIGN KEY ([CohortId]) REFERENCES [Cohorts] ([CohortId]) ON DELETE NO ACTION
);

CREATE TABLE [OnboardingBatchRows] (
    [BatchId] uniqueidentifier NOT NULL,
    [Row] int NOT NULL,
    [UserName] nvarchar(256) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [Action] nvarchar(20) NOT NULL,
    [ExpectedUserId] nvarchar(450) NULL,
    [Status] nvarchar(20) NOT NULL,
    [Message] nvarchar(1000) NULL,
    [UserId] nvarchar(450) NULL,
    [MembershipId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_OnboardingBatchRows] PRIMARY KEY ([BatchId], [Row]),
    CONSTRAINT [CK_OnboardingBatchRows_Action] CHECK ([Action] IN ('Create','EnrolExisting','Skip')),
    CONSTRAINT [CK_OnboardingBatchRows_Row] CHECK ([Row] > 1),
    CONSTRAINT [CK_OnboardingBatchRows_Status] CHECK ([Status] IN ('Pending','Failed','Created','Enrolled','Skipped')),
    CONSTRAINT [FK_OnboardingBatchRows_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OnboardingBatchRows_CohortMemberships_MembershipId] FOREIGN KEY ([MembershipId]) REFERENCES [CohortMemberships] ([MembershipId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OnboardingBatchRows_OnboardingBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [OnboardingBatches] ([BatchId]) ON DELETE NO ACTION
);

CREATE INDEX [IX_OnboardingBatches_ActorId] ON [OnboardingBatches] ([ActorId]);

CREATE INDEX [IX_OnboardingBatches_CohortId] ON [OnboardingBatches] ([CohortId]);

CREATE INDEX [IX_OnboardingBatchRows_MembershipId] ON [OnboardingBatchRows] ([MembershipId]);

CREATE INDEX [IX_OnboardingBatchRows_UserId] ON [OnboardingBatchRows] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261116205953_AddBulkOnboardingBatches', N'10.0.14');

COMMIT;
GO

