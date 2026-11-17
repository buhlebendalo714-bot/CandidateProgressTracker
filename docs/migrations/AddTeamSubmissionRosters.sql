BEGIN TRANSACTION;
ALTER TABLE [Submissions] ADD [CreditedRosterAssignmentId] uniqueidentifier NULL;

ALTER TABLE [TeamMemberships] ADD CONSTRAINT [AK_TeamMemberships_TeamMembershipId_TeamId_ProjectId_UserId] UNIQUE ([TeamMembershipId], [TeamId], [ProjectId], [UserId]);

ALTER TABLE [CohortMemberships] ADD CONSTRAINT [AK_CohortMemberships_MembershipId_CohortId_UserId] UNIQUE ([MembershipId], [CohortId], [UserId]);

ALTER TABLE [Assignments] ADD CONSTRAINT [AK_Assignments_AssignmentId_ProjectId] UNIQUE ([AssignmentId], [ProjectId]);

CREATE TABLE [AssignmentCreditedRosters] (
    [AssignmentId] uniqueidentifier NOT NULL,
    [TeamId] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [CohortId] uniqueidentifier NOT NULL,
    [TeamNameSnapshot] nvarchar(150) NOT NULL,
    [FrozenAt] datetimeoffset NOT NULL,
    [FrozenBy] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AssignmentCreditedRosters] PRIMARY KEY ([AssignmentId]),
    CONSTRAINT [AK_AssignmentCreditedRosters_AssignmentId_TeamId_ProjectId_CohortId] UNIQUE ([AssignmentId], [TeamId], [ProjectId], [CohortId]),
    CONSTRAINT [CK_CreditedRosters_Utc] CHECK (DATEPART(TZOFFSET, [FrozenAt]) = 0),
    CONSTRAINT [FK_AssignmentCreditedRosters_AspNetUsers_FrozenBy] FOREIGN KEY ([FrozenBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssignmentCreditedRosters_Assignments_AssignmentId_ProjectId] FOREIGN KEY ([AssignmentId], [ProjectId]) REFERENCES [Assignments] ([AssignmentId], [ProjectId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssignmentCreditedRosters_Cohorts_CohortId] FOREIGN KEY ([CohortId]) REFERENCES [Cohorts] ([CohortId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssignmentCreditedRosters_ProjectTeams_TeamId_ProjectId] FOREIGN KEY ([TeamId], [ProjectId]) REFERENCES [ProjectTeams] ([TeamId], [ProjectId]) ON DELETE NO ACTION
);

CREATE TABLE [AssignmentCreditedRosterMembers] (
    [AssignmentId] uniqueidentifier NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [TeamId] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [CohortId] uniqueidentifier NOT NULL,
    [UserNameSnapshot] nvarchar(256) NOT NULL,
    [TeamMembershipId] uniqueidentifier NOT NULL,
    [CohortMembershipId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssignmentCreditedRosterMembers] PRIMARY KEY ([AssignmentId], [UserId]),
    CONSTRAINT [FK_AssignmentCreditedRosterMembers_AssignmentCreditedRosters_AssignmentId_TeamId_ProjectId_CohortId] FOREIGN KEY ([AssignmentId], [TeamId], [ProjectId], [CohortId]) REFERENCES [AssignmentCreditedRosters] ([AssignmentId], [TeamId], [ProjectId], [CohortId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssignmentCreditedRosterMembers_CohortMemberships_CohortMembershipId_CohortId_UserId] FOREIGN KEY ([CohortMembershipId], [CohortId], [UserId]) REFERENCES [CohortMemberships] ([MembershipId], [CohortId], [UserId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssignmentCreditedRosterMembers_TeamMemberships_TeamMembershipId_TeamId_ProjectId_UserId] FOREIGN KEY ([TeamMembershipId], [TeamId], [ProjectId], [UserId]) REFERENCES [TeamMemberships] ([TeamMembershipId], [TeamId], [ProjectId], [UserId]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Submissions_CreditedRosterAssignmentId] ON [Submissions] ([CreditedRosterAssignmentId]);

ALTER TABLE [Submissions] ADD CONSTRAINT [CK_Submissions_RosterAssignment] CHECK ([CreditedRosterAssignmentId] IS NULL OR [CreditedRosterAssignmentId] = [AssignmentId]);

CREATE INDEX [IX_AssignmentCreditedRosterMembers_AssignmentId_TeamId_ProjectId_CohortId] ON [AssignmentCreditedRosterMembers] ([AssignmentId], [TeamId], [ProjectId], [CohortId]);

CREATE INDEX [IX_AssignmentCreditedRosterMembers_CohortMembershipId_CohortId_UserId] ON [AssignmentCreditedRosterMembers] ([CohortMembershipId], [CohortId], [UserId]);

CREATE INDEX [IX_AssignmentCreditedRosterMembers_TeamMembershipId_TeamId_ProjectId_UserId] ON [AssignmentCreditedRosterMembers] ([TeamMembershipId], [TeamId], [ProjectId], [UserId]);

CREATE INDEX [IX_AssignmentCreditedRosters_AssignmentId_ProjectId] ON [AssignmentCreditedRosters] ([AssignmentId], [ProjectId]);

CREATE INDEX [IX_AssignmentCreditedRosters_CohortId] ON [AssignmentCreditedRosters] ([CohortId]);

CREATE INDEX [IX_AssignmentCreditedRosters_FrozenBy] ON [AssignmentCreditedRosters] ([FrozenBy]);

CREATE INDEX [IX_AssignmentCreditedRosters_TeamId_ProjectId] ON [AssignmentCreditedRosters] ([TeamId], [ProjectId]);

ALTER TABLE [Submissions] ADD CONSTRAINT [FK_Submissions_AssignmentCreditedRosters_CreditedRosterAssignmentId] FOREIGN KEY ([CreditedRosterAssignmentId]) REFERENCES [AssignmentCreditedRosters] ([AssignmentId]) ON DELETE NO ACTION;

EXEC(N'CREATE TRIGGER [TR_CreditedRosters_ImmutableScope] ON [AssignmentCreditedRosters]
AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51020, ''Credited rosters are immutable.'', 1;
    IF EXISTS (SELECT 1 FROM inserted r
        LEFT JOIN Assignments a ON a.AssignmentId=r.AssignmentId
        LEFT JOIN Projects p ON p.ProjectId=a.ProjectId
        LEFT JOIN LearningCycles c ON c.CycleId=a.CycleId
        WHERE a.TeamId IS NULL OR a.TeamId<>r.TeamId OR a.CandidateId IS NOT NULL
            OR a.ProjectId<>r.ProjectId OR p.Mode<>2 OR c.CohortId<>r.CohortId)
        THROW 51021, ''Roster must match its team assignment and cohort.'', 1;
END');

EXEC(N'CREATE TRIGGER [TR_CreditedRosterMembers_Immutable] ON [AssignmentCreditedRosterMembers]
AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51022, ''Credited members are immutable.'', 1;
    IF EXISTS (SELECT 1 FROM inserted i JOIN Submissions s ON s.AssignmentId=i.AssignmentId)
        THROW 51023, ''Cannot extend a roster after submission.'', 1;
END');

EXEC(N'CREATE TRIGGER [TR_Assignments_CreditedRosterScope] ON [Assignments] AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted a JOIN AssignmentCreditedRosters r ON r.AssignmentId=a.AssignmentId
        JOIN LearningCycles c ON c.CycleId=a.CycleId
        WHERE a.TeamId IS NULL OR a.TeamId<>r.TeamId OR a.CandidateId IS NOT NULL
            OR a.ProjectId<>r.ProjectId OR c.CohortId<>r.CohortId)
        THROW 51024, ''Cannot change the owner or scope of a credited assignment.'', 1;
END');

EXEC(N'CREATE TRIGGER [TR_Submissions_CreditedRoster] ON [Submissions] AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted s JOIN Assignments a ON a.AssignmentId=s.AssignmentId
        WHERE (a.TeamId IS NOT NULL AND (s.CreditedRosterAssignmentId IS NULL
            OR NOT EXISTS (SELECT 1 FROM AssignmentCreditedRosterMembers m
                WHERE m.AssignmentId=s.AssignmentId AND m.UserId=s.SubmittedBy)))
            OR (a.TeamId IS NULL AND s.CreditedRosterAssignmentId IS NOT NULL))
        THROW 51025, ''Team attempts require their assignment roster and a credited submitter.'', 1;
END');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106192833_AddTeamSubmissionRosters', N'10.0.14');

COMMIT;
GO

