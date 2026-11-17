BEGIN TRANSACTION;
ALTER TABLE [Submissions] ADD CONSTRAINT [AK_Submissions_SubmissionId_AssignmentId] UNIQUE ([SubmissionId], [AssignmentId]);

CREATE TABLE [SubmissionReviews] (
    [ReviewId] uniqueidentifier NOT NULL,
    [AssignmentId] uniqueidentifier NOT NULL,
    [SubmissionId] uniqueidentifier NOT NULL,
    [ReviewNo] int NOT NULL,
    [Decision] tinyint NOT NULL,
    [Feedback] nvarchar(max) NOT NULL,
    [ReviewerId] nvarchar(450) NOT NULL,
    [DecidedAt] datetimeoffset NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SubmissionReviews] PRIMARY KEY ([ReviewId]),
    CONSTRAINT [AK_SubmissionReviews_ReviewId_AssignmentId] UNIQUE ([ReviewId], [AssignmentId]),
    CONSTRAINT [CK_SubmissionReviews_Decision] CHECK ([Decision] IN (1, 2)),
    CONSTRAINT [CK_SubmissionReviews_Feedback] CHECK (DATALENGTH([Feedback]) <= 20000 AND ([Decision] <> 2 OR LEN(LTRIM(RTRIM([Feedback]))) > 0)),
    CONSTRAINT [CK_SubmissionReviews_Number] CHECK ([ReviewNo] > 0),
    CONSTRAINT [CK_SubmissionReviews_Request] CHECK ([RequestId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_SubmissionReviews_Utc] CHECK (DATEPART(TZOFFSET, [DecidedAt]) = 0),
    CONSTRAINT [FK_SubmissionReviews_AspNetUsers_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SubmissionReviews_Submissions_SubmissionId_AssignmentId] FOREIGN KEY ([SubmissionId], [AssignmentId]) REFERENCES [Submissions] ([SubmissionId], [AssignmentId]) ON DELETE NO ACTION
);

CREATE TABLE [AssignmentReopenings] (
    [ReopeningId] uniqueidentifier NOT NULL,
    [AssignmentId] uniqueidentifier NOT NULL,
    [ApprovalReviewId] uniqueidentifier NOT NULL,
    [AdministratorId] nvarchar(450) NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [ReopenedAt] datetimeoffset NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssignmentReopenings] PRIMARY KEY ([ReopeningId]),
    CONSTRAINT [CK_AssignmentReopenings_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
    CONSTRAINT [CK_AssignmentReopenings_Request] CHECK ([RequestId] <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT [CK_AssignmentReopenings_Utc] CHECK (DATEPART(TZOFFSET, [ReopenedAt]) = 0),
    CONSTRAINT [FK_AssignmentReopenings_AspNetUsers_AdministratorId] FOREIGN KEY ([AdministratorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssignmentReopenings_SubmissionReviews_ApprovalReviewId_AssignmentId] FOREIGN KEY ([ApprovalReviewId], [AssignmentId]) REFERENCES [SubmissionReviews] ([ReviewId], [AssignmentId]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_AssignmentReopenings_AdministratorId_RequestId] ON [AssignmentReopenings] ([AdministratorId], [RequestId]);

CREATE UNIQUE INDEX [IX_AssignmentReopenings_ApprovalReviewId] ON [AssignmentReopenings] ([ApprovalReviewId]);

CREATE INDEX [IX_AssignmentReopenings_ApprovalReviewId_AssignmentId] ON [AssignmentReopenings] ([ApprovalReviewId], [AssignmentId]);

CREATE UNIQUE INDEX [IX_SubmissionReviews_AssignmentId_ReviewNo] ON [SubmissionReviews] ([AssignmentId], [ReviewNo]);

CREATE UNIQUE INDEX [IX_SubmissionReviews_ReviewerId_RequestId] ON [SubmissionReviews] ([ReviewerId], [RequestId]);

CREATE INDEX [IX_SubmissionReviews_SubmissionId_AssignmentId] ON [SubmissionReviews] ([SubmissionId], [AssignmentId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106175348_AddIndividualSubmissionReviews', N'10.0.14');

COMMIT;
GO

