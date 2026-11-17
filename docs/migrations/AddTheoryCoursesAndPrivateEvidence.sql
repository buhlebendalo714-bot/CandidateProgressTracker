BEGIN TRANSACTION;
ALTER TABLE [AssignmentCreditedRosters] DROP CONSTRAINT [FK_AssignmentCreditedRosters_Assignments_AssignmentId_ProjectId];

ALTER TABLE [Assignments] DROP CONSTRAINT [AK_Assignments_AssignmentId_ProjectId];

DROP INDEX [IX_Assignments_MilestoneId_CandidateId] ON [Assignments];

DROP INDEX [IX_AssignmentCreditedRosters_AssignmentId_ProjectId] ON [AssignmentCreditedRosters];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Assignments]') AND [c].[name] = N'ProjectId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Assignments] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Assignments] ALTER COLUMN [ProjectId] uniqueidentifier NULL;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Assignments]') AND [c].[name] = N'MilestoneId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Assignments] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Assignments] ALTER COLUMN [MilestoneId] uniqueidentifier NULL;

ALTER TABLE [Assignments] ADD [OfferingId] uniqueidentifier NULL;

CREATE TABLE [Courses] (
    [CourseId] uniqueidentifier NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Platform] nvarchar(120) NOT NULL,
    [Url] nvarchar(2048) NOT NULL,
    [CreatedBy] nvarchar(450) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Courses] PRIMARY KEY ([CourseId]),
    CONSTRAINT [FK_Courses_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [UploadAttempts] (
    [UploadAttemptId] uniqueidentifier NOT NULL,
    [AssignmentId] uniqueidentifier NOT NULL,
    [RevisionId] uniqueidentifier NOT NULL,
    [ActorId] nvarchar(450) NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [Explanation] nvarchar(max) NOT NULL,
    [LinksJson] nvarchar(max) NOT NULL,
    [PayloadHash] nvarchar(64) NOT NULL,
    [RosterConfirmation] nvarchar(64) NOT NULL,
    [ExpectedVersion] varbinary(8) NOT NULL,
    [ReceivedAt] datetimeoffset NOT NULL,
    [Status] nvarchar(30) NOT NULL,
    [Message] nvarchar(1000) NOT NULL,
    [Failures] int NOT NULL,
    [NextTryAt] datetimeoffset NOT NULL,
    [LeaseUntil] datetimeoffset NULL,
    [LeaseId] uniqueidentifier NULL,
    [SubmissionId] uniqueidentifier NULL,
    [FinishedAt] datetimeoffset NULL,
    [Hold] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_UploadAttempts] PRIMARY KEY ([UploadAttemptId]),
    CONSTRAINT [CK_UploadAttempts_State] CHECK (([Status]='Committed' AND [SubmissionId] IS NOT NULL) OR ([Status] IN ('Processing','Rejected','Expired') AND [SubmissionId] IS NULL)),
    CONSTRAINT [FK_UploadAttempts_AspNetUsers_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UploadAttempts_AssignmentRevisions_RevisionId_AssignmentId] FOREIGN KEY ([RevisionId], [AssignmentId]) REFERENCES [AssignmentRevisions] ([RevisionId], [AssignmentId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UploadAttempts_Submissions_SubmissionId_AssignmentId] FOREIGN KEY ([SubmissionId], [AssignmentId]) REFERENCES [Submissions] ([SubmissionId], [AssignmentId]) ON DELETE NO ACTION
);

CREATE TABLE [CourseOfferings] (
    [OfferingId] uniqueidentifier NOT NULL,
    [CourseId] uniqueidentifier NOT NULL,
    [CycleId] uniqueidentifier NOT NULL,
    [TitleSnapshot] nvarchar(200) NOT NULL,
    [PlatformSnapshot] nvarchar(120) NOT NULL,
    [UrlSnapshot] nvarchar(2048) NOT NULL,
    [CreatedBy] nvarchar(450) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CourseOfferings] PRIMARY KEY ([OfferingId]),
    CONSTRAINT [AK_CourseOfferings_OfferingId_CycleId] UNIQUE ([OfferingId], [CycleId]),
    CONSTRAINT [FK_CourseOfferings_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CourseOfferings_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([CourseId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CourseOfferings_LearningCycles_CycleId] FOREIGN KEY ([CycleId]) REFERENCES [LearningCycles] ([CycleId]) ON DELETE NO ACTION
);

CREATE TABLE [EvidenceFiles] (
    [EvidenceFileId] uniqueidentifier NOT NULL,
    [UploadAttemptId] uniqueidentifier NOT NULL,
    [SequenceNo] int NOT NULL,
    [OriginalName] nvarchar(255) NOT NULL,
    [ObjectKey] nvarchar(64) NOT NULL,
    [MediaType] nvarchar(40) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [Sha256] nvarchar(64) NOT NULL,
    [ScannedAt] datetimeoffset NULL,
    [ScanResult] nvarchar(30) NOT NULL,
    [Deleted] bit NOT NULL,
    CONSTRAINT [PK_EvidenceFiles] PRIMARY KEY ([EvidenceFileId]),
    CONSTRAINT [CK_EvidenceFiles_Size] CHECK ([SizeBytes]>0 AND [SizeBytes]<=10485760 AND [SequenceNo] BETWEEN 1 AND 5),
    CONSTRAINT [FK_EvidenceFiles_UploadAttempts_UploadAttemptId] FOREIGN KEY ([UploadAttemptId]) REFERENCES [UploadAttempts] ([UploadAttemptId]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_Assignments_MilestoneId_CandidateId] ON [Assignments] ([MilestoneId], [CandidateId]) WHERE [CandidateId] IS NOT NULL AND [MilestoneId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_Assignments_OfferingId_CandidateId] ON [Assignments] ([OfferingId], [CandidateId]) WHERE [OfferingId] IS NOT NULL;

CREATE INDEX [IX_Assignments_OfferingId_CycleId] ON [Assignments] ([OfferingId], [CycleId]);

ALTER TABLE [Assignments] ADD CONSTRAINT [CK_Assignments_Target] CHECK (([OfferingId] IS NULL AND [ProjectId] IS NOT NULL AND [MilestoneId] IS NOT NULL) OR ([OfferingId] IS NOT NULL AND [ProjectId] IS NULL AND [MilestoneId] IS NULL AND [CandidateId] IS NOT NULL AND [TeamId] IS NULL));

CREATE UNIQUE INDEX [IX_CourseOfferings_CourseId_CycleId] ON [CourseOfferings] ([CourseId], [CycleId]);

CREATE INDEX [IX_CourseOfferings_CreatedBy] ON [CourseOfferings] ([CreatedBy]);

CREATE INDEX [IX_CourseOfferings_CycleId] ON [CourseOfferings] ([CycleId]);

CREATE INDEX [IX_Courses_CreatedBy] ON [Courses] ([CreatedBy]);

CREATE UNIQUE INDEX [IX_EvidenceFiles_ObjectKey] ON [EvidenceFiles] ([ObjectKey]);

CREATE UNIQUE INDEX [IX_EvidenceFiles_UploadAttemptId_SequenceNo] ON [EvidenceFiles] ([UploadAttemptId], [SequenceNo]);

CREATE UNIQUE INDEX [IX_UploadAttempts_ActorId_RequestId] ON [UploadAttempts] ([ActorId], [RequestId]);

CREATE INDEX [IX_UploadAttempts_RevisionId_AssignmentId] ON [UploadAttempts] ([RevisionId], [AssignmentId]);

CREATE INDEX [IX_UploadAttempts_Status_NextTryAt_LeaseUntil] ON [UploadAttempts] ([Status], [NextTryAt], [LeaseUntil]);

CREATE UNIQUE INDEX [IX_UploadAttempts_SubmissionId] ON [UploadAttempts] ([SubmissionId]) WHERE [SubmissionId] IS NOT NULL;

CREATE INDEX [IX_UploadAttempts_SubmissionId_AssignmentId] ON [UploadAttempts] ([SubmissionId], [AssignmentId]);

ALTER TABLE [AssignmentCreditedRosters] ADD CONSTRAINT [FK_AssignmentCreditedRosters_Assignments_AssignmentId] FOREIGN KEY ([AssignmentId]) REFERENCES [Assignments] ([AssignmentId]) ON DELETE NO ACTION;

ALTER TABLE [Assignments] ADD CONSTRAINT [FK_Assignments_CourseOfferings_OfferingId_CycleId] FOREIGN KEY ([OfferingId], [CycleId]) REFERENCES [CourseOfferings] ([OfferingId], [CycleId]) ON DELETE NO ACTION;

EXEC(N'CREATE TRIGGER TR_EvidenceFiles_Immutable ON EvidenceFiles AFTER UPDATE, DELETE AS
BEGIN SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d WHERE d.ScanResult=''Clean'')
        THROW 51030, ''Committed evidence metadata is immutable.'', 1;
    IF EXISTS (SELECT EvidenceFileId,UploadAttemptId,SequenceNo,OriginalName,ObjectKey,MediaType,SizeBytes,Sha256 FROM deleted
        EXCEPT SELECT EvidenceFileId,UploadAttemptId,SequenceNo,OriginalName,ObjectKey,MediaType,SizeBytes,Sha256 FROM inserted)
        THROW 51031, ''Evidence payload identity is immutable.'', 1;
END');
EXEC(N'CREATE TRIGGER TR_UploadAttempts_Immutable ON UploadAttempts AFTER UPDATE, DELETE AS
BEGIN SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted WHERE Status=''Committed'')
        THROW 51032, ''Committed processing receipts are immutable.'', 1;
    IF EXISTS (SELECT UploadAttemptId,AssignmentId,RevisionId,ActorId,RequestId,Explanation,LinksJson,PayloadHash,RosterConfirmation,ExpectedVersion FROM deleted
        EXCEPT SELECT UploadAttemptId,AssignmentId,RevisionId,ActorId,RequestId,Explanation,LinksJson,PayloadHash,RosterConfirmation,ExpectedVersion FROM inserted)
        THROW 51033, ''Processing payload identity is immutable.'', 1;
END');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261107065418_AddTheoryCoursesAndPrivateEvidence', N'10.0.14');

COMMIT;
GO

