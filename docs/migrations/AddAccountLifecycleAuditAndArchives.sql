BEGIN TRANSACTION;
IF EXISTS (SELECT NormalizedEmail FROM AspNetUsers WHERE NormalizedEmail IS NOT NULL GROUP BY NormalizedEmail HAVING COUNT(*)>1) THROW 51020, 'Duplicate normalized emails require explicit correction before this upgrade; no records were changed.', 1;

DROP INDEX [EmailIndex] ON [AspNetUsers];

CREATE TABLE [AccountAccessStates] (
    [UserId] nvarchar(450) NOT NULL,
    [Enabled] bit NOT NULL,
    [PermissionVersion] bigint NOT NULL,
    [ChangedAt] datetimeoffset NOT NULL,
    [ChangedBy] nvarchar(450) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_AccountAccessStates] PRIMARY KEY ([UserId]),
    CONSTRAINT [FK_AccountAccessStates_AspNetUsers_ChangedBy] FOREIGN KEY ([ChangedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AccountAccessStates_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [AccountChanges] (
    [AccountChangeId] uniqueidentifier NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [ActorId] nvarchar(450) NOT NULL,
    [Action] nvarchar(80) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AccountChanges] PRIMARY KEY ([AccountChangeId]),
    CONSTRAINT [FK_AccountChanges_AspNetUsers_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AccountChanges_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [AccountCredentials] (
    [CredentialId] uniqueidentifier NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Kind] nvarchar(20) NOT NULL,
    [TokenHash] nvarchar(64) NOT NULL,
    [IssuedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [UsedAt] datetimeoffset NULL,
    [RevokedAt] datetimeoffset NULL,
    [IssuedBy] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AccountCredentials] PRIMARY KEY ([CredentialId]),
    CONSTRAINT [CK_AccountCredentials_Expiry] CHECK ([ExpiresAt] > [IssuedAt] AND [Kind] IN ('Activation','Recovery')),
    CONSTRAINT [FK_AccountCredentials_AspNetUsers_IssuedBy] FOREIGN KEY ([IssuedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AccountCredentials_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [AuditEvents] (
    [AuditEventId] uniqueidentifier NOT NULL,
    [ActorId] nvarchar(450) NULL,
    [EntityType] nvarchar(100) NOT NULL,
    [EntityId] nvarchar(1000) NOT NULL,
    [Action] nvarchar(80) NOT NULL,
    [Details] nvarchar(2000) NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([AuditEventId])
);

CREATE TABLE [ScopeArchiveEvents] (
    [ScopeArchiveEventId] uniqueidentifier NOT NULL,
    [ScopeId] uniqueidentifier NOT NULL,
    [ScopeType] nvarchar(20) NOT NULL,
    [ActorId] nvarchar(450) NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [ExpectedVersion] nvarchar(100) NOT NULL,
    [Archived] bit NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [OutcomeSnapshot] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ScopeArchiveEvents] PRIMARY KEY ([ScopeArchiveEventId]),
    CONSTRAINT [CK_ScopeArchiveEvents_Kind] CHECK ([ScopeType] IN ('Project','Cycle') AND LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 1000),
    CONSTRAINT [FK_ScopeArchiveEvents_AspNetUsers_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;

CREATE INDEX [IX_AccountAccessStates_ChangedBy] ON [AccountAccessStates] ([ChangedBy]);

CREATE INDEX [IX_AccountChanges_ActorId] ON [AccountChanges] ([ActorId]);

CREATE INDEX [IX_AccountChanges_OccurredAt] ON [AccountChanges] ([OccurredAt]);

CREATE INDEX [IX_AccountChanges_UserId] ON [AccountChanges] ([UserId]);

CREATE INDEX [IX_AccountCredentials_IssuedBy] ON [AccountCredentials] ([IssuedBy]);

CREATE UNIQUE INDEX [IX_AccountCredentials_TokenHash] ON [AccountCredentials] ([TokenHash]);

CREATE INDEX [IX_AccountCredentials_UserId_IssuedAt] ON [AccountCredentials] ([UserId], [IssuedAt]);

CREATE INDEX [IX_AuditEvents_ActorId] ON [AuditEvents] ([ActorId]);

CREATE INDEX [IX_AuditEvents_OccurredAt_AuditEventId] ON [AuditEvents] ([OccurredAt], [AuditEventId]);

CREATE UNIQUE INDEX [IX_ScopeArchiveEvents_ActorId_RequestId] ON [ScopeArchiveEvents] ([ActorId], [RequestId]);

CREATE INDEX [IX_ScopeArchiveEvents_ScopeId_OccurredAt] ON [ScopeArchiveEvents] ([ScopeId], [OccurredAt]);

CREATE TRIGGER [TR_AuditEvents_AppendOnly] ON [AuditEvents] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51021, 'Audit records are append-only.', 1; END;

CREATE TRIGGER [TR_AccountChanges_AppendOnly] ON [AccountChanges] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51021, 'Audit records are append-only.', 1; END;

CREATE TRIGGER [TR_ScopeArchiveEvents_AppendOnly] ON [ScopeArchiveEvents] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51021, 'Audit records are append-only.', 1; END;

CREATE TRIGGER [TR_ScopeArchiveEvents_Scope] ON [ScopeArchiveEvents] AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted i WHERE (i.ScopeType='Project' AND NOT EXISTS(SELECT 1 FROM Projects p WHERE p.ProjectId=i.ScopeId)) OR (i.ScopeType='Cycle' AND NOT EXISTS(SELECT 1 FROM LearningCycles c WHERE c.CycleId=i.ScopeId))) THROW 51022, 'Archive scope does not exist.', 1; END;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261107083104_AddAccountLifecycleAuditAndArchives', N'10.0.14');

COMMIT;
GO

