using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamSubmissionRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreditedRosterAssignmentId",
                table: "Submissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TeamMemberships_TeamMembershipId_TeamId_ProjectId_UserId",
                table: "TeamMemberships",
                columns: new[] { "TeamMembershipId", "TeamId", "ProjectId", "UserId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CohortMemberships_MembershipId_CohortId_UserId",
                table: "CohortMemberships",
                columns: new[] { "MembershipId", "CohortId", "UserId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Assignments_AssignmentId_ProjectId",
                table: "Assignments",
                columns: new[] { "AssignmentId", "ProjectId" });

            migrationBuilder.CreateTable(
                name: "AssignmentCreditedRosters",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CohortId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FrozenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FrozenBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentCreditedRosters", x => x.AssignmentId);
                    table.UniqueConstraint("AK_AssignmentCreditedRosters_AssignmentId_TeamId_ProjectId_CohortId", x => new { x.AssignmentId, x.TeamId, x.ProjectId, x.CohortId });
                    table.CheckConstraint("CK_CreditedRosters_Utc", "DATEPART(TZOFFSET, [FrozenAt]) = 0");
                    table.ForeignKey(
                        name: "FK_AssignmentCreditedRosters_AspNetUsers_FrozenBy",
                        column: x => x.FrozenBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentCreditedRosters_Assignments_AssignmentId_ProjectId",
                        columns: x => new { x.AssignmentId, x.ProjectId },
                        principalTable: "Assignments",
                        principalColumns: new[] { "AssignmentId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentCreditedRosters_Cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohorts",
                        principalColumn: "CohortId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentCreditedRosters_ProjectTeams_TeamId_ProjectId",
                        columns: x => new { x.TeamId, x.ProjectId },
                        principalTable: "ProjectTeams",
                        principalColumns: new[] { "TeamId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentCreditedRosterMembers",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CohortId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TeamMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CohortMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentCreditedRosterMembers", x => new { x.AssignmentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AssignmentCreditedRosterMembers_AssignmentCreditedRosters_AssignmentId_TeamId_ProjectId_CohortId",
                        columns: x => new { x.AssignmentId, x.TeamId, x.ProjectId, x.CohortId },
                        principalTable: "AssignmentCreditedRosters",
                        principalColumns: new[] { "AssignmentId", "TeamId", "ProjectId", "CohortId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentCreditedRosterMembers_CohortMemberships_CohortMembershipId_CohortId_UserId",
                        columns: x => new { x.CohortMembershipId, x.CohortId, x.UserId },
                        principalTable: "CohortMemberships",
                        principalColumns: new[] { "MembershipId", "CohortId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentCreditedRosterMembers_TeamMemberships_TeamMembershipId_TeamId_ProjectId_UserId",
                        columns: x => new { x.TeamMembershipId, x.TeamId, x.ProjectId, x.UserId },
                        principalTable: "TeamMemberships",
                        principalColumns: new[] { "TeamMembershipId", "TeamId", "ProjectId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_CreditedRosterAssignmentId",
                table: "Submissions",
                column: "CreditedRosterAssignmentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submissions_RosterAssignment",
                table: "Submissions",
                sql: "[CreditedRosterAssignmentId] IS NULL OR [CreditedRosterAssignmentId] = [AssignmentId]");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCreditedRosterMembers_AssignmentId_TeamId_ProjectId_CohortId",
                table: "AssignmentCreditedRosterMembers",
                columns: new[] { "AssignmentId", "TeamId", "ProjectId", "CohortId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCreditedRosterMembers_CohortMembershipId_CohortId_UserId",
                table: "AssignmentCreditedRosterMembers",
                columns: new[] { "CohortMembershipId", "CohortId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCreditedRosterMembers_TeamMembershipId_TeamId_ProjectId_UserId",
                table: "AssignmentCreditedRosterMembers",
                columns: new[] { "TeamMembershipId", "TeamId", "ProjectId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCreditedRosters_AssignmentId_ProjectId",
                table: "AssignmentCreditedRosters",
                columns: new[] { "AssignmentId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCreditedRosters_CohortId",
                table: "AssignmentCreditedRosters",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCreditedRosters_FrozenBy",
                table: "AssignmentCreditedRosters",
                column: "FrozenBy");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentCreditedRosters_TeamId_ProjectId",
                table: "AssignmentCreditedRosters",
                columns: new[] { "TeamId", "ProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_AssignmentCreditedRosters_CreditedRosterAssignmentId",
                table: "Submissions",
                column: "CreditedRosterAssignmentId",
                principalTable: "AssignmentCreditedRosters",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Restrict);
            // Nullable assignment ownership cannot be a SQL alternate key without changing
            // individual assignments. Validate that part of the relationship in a trigger.
            CreateTrigger(migrationBuilder, """
                CREATE TRIGGER [TR_CreditedRosters_ImmutableScope] ON [AssignmentCreditedRosters]
                AFTER INSERT, UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51020, 'Credited rosters are immutable.', 1;
                    IF EXISTS (SELECT 1 FROM inserted r
                        LEFT JOIN Assignments a ON a.AssignmentId=r.AssignmentId
                        LEFT JOIN Projects p ON p.ProjectId=a.ProjectId
                        LEFT JOIN LearningCycles c ON c.CycleId=a.CycleId
                        WHERE a.TeamId IS NULL OR a.TeamId<>r.TeamId OR a.CandidateId IS NOT NULL
                            OR a.ProjectId<>r.ProjectId OR p.Mode<>2 OR c.CohortId<>r.CohortId)
                        THROW 51021, 'Roster must match its team assignment and cohort.', 1;
                END
                """);
            CreateTrigger(migrationBuilder, """
                CREATE TRIGGER [TR_CreditedRosterMembers_Immutable] ON [AssignmentCreditedRosterMembers]
                AFTER INSERT, UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51022, 'Credited members are immutable.', 1;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN Submissions s ON s.AssignmentId=i.AssignmentId)
                        THROW 51023, 'Cannot extend a roster after submission.', 1;
                END
                """);
            CreateTrigger(migrationBuilder, """
                CREATE TRIGGER [TR_Assignments_CreditedRosterScope] ON [Assignments] AFTER UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted a JOIN AssignmentCreditedRosters r ON r.AssignmentId=a.AssignmentId
                        JOIN LearningCycles c ON c.CycleId=a.CycleId
                        WHERE a.TeamId IS NULL OR a.TeamId<>r.TeamId OR a.CandidateId IS NOT NULL
                            OR a.ProjectId<>r.ProjectId OR c.CohortId<>r.CohortId)
                        THROW 51024, 'Cannot change the owner or scope of a credited assignment.', 1;
                END
                """);
            CreateTrigger(migrationBuilder, """
                CREATE TRIGGER [TR_Submissions_CreditedRoster] ON [Submissions] AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted s JOIN Assignments a ON a.AssignmentId=s.AssignmentId
                        WHERE (a.TeamId IS NOT NULL AND (s.CreditedRosterAssignmentId IS NULL
                            OR NOT EXISTS (SELECT 1 FROM AssignmentCreditedRosterMembers m
                                WHERE m.AssignmentId=s.AssignmentId AND m.UserId=s.SubmittedBy)))
                            OR (a.TeamId IS NULL AND s.CreditedRosterAssignmentId IS NOT NULL))
                        THROW 51025, 'Team attempts require their assignment roster and a credited submitter.', 1;
                END
                """);
        }

        // EXEC gives CREATE TRIGGER its own batch in both IMigrator and the generated
        // transaction-wrapped deployment script (CREATE TRIGGER must start a batch).
        private static void CreateTrigger(MigrationBuilder builder, string sql) =>
            builder.Sql("EXEC(N'" + sql.Replace("'", "''") + "');");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER [TR_Submissions_CreditedRoster]; DROP TRIGGER [TR_Assignments_CreditedRosterScope]; DROP TRIGGER [TR_CreditedRosterMembers_Immutable]; DROP TRIGGER [TR_CreditedRosters_ImmutableScope];");
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_AssignmentCreditedRosters_CreditedRosterAssignmentId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "AssignmentCreditedRosterMembers");

            migrationBuilder.DropTable(
                name: "AssignmentCreditedRosters");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TeamMemberships_TeamMembershipId_TeamId_ProjectId_UserId",
                table: "TeamMemberships");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_CreditedRosterAssignmentId",
                table: "Submissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Submissions_RosterAssignment",
                table: "Submissions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CohortMemberships_MembershipId_CohortId_UserId",
                table: "CohortMemberships");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Assignments_AssignmentId_ProjectId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "CreditedRosterAssignmentId",
                table: "Submissions");
        }
    }
}
