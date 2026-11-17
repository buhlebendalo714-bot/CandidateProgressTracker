using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTheoryCoursesAndPrivateEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentCreditedRosters_Assignments_AssignmentId_ProjectId",
                table: "AssignmentCreditedRosters");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Assignments_AssignmentId_ProjectId",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_MilestoneId_CandidateId",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentCreditedRosters_AssignmentId_ProjectId",
                table: "AssignmentCreditedRosters");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "MilestoneId",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "OfferingId",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.CourseId);
                    table.ForeignKey(
                        name: "FK_Courses_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UploadAttempts",
                columns: table => new
                {
                    UploadAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    LinksJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RosterConfirmation = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpectedVersion = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Failures = table.Column<int>(type: "int", nullable: false),
                    NextTryAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Hold = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadAttempts", x => x.UploadAttemptId);
                    table.CheckConstraint("CK_UploadAttempts_State", "([Status]='Committed' AND [SubmissionId] IS NOT NULL) OR ([Status] IN ('Processing','Rejected','Expired') AND [SubmissionId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_UploadAttempts_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadAttempts_AssignmentRevisions_RevisionId_AssignmentId",
                        columns: x => new { x.RevisionId, x.AssignmentId },
                        principalTable: "AssignmentRevisions",
                        principalColumns: new[] { "RevisionId", "AssignmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadAttempts_Submissions_SubmissionId_AssignmentId",
                        columns: x => new { x.SubmissionId, x.AssignmentId },
                        principalTable: "Submissions",
                        principalColumns: new[] { "SubmissionId", "AssignmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseOfferings",
                columns: table => new
                {
                    OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlatformSnapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UrlSnapshot = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOfferings", x => x.OfferingId);
                    table.UniqueConstraint("AK_CourseOfferings_OfferingId_CycleId", x => new { x.OfferingId, x.CycleId });
                    table.ForeignKey(
                        name: "FK_CourseOfferings_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "CourseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_LearningCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "LearningCycles",
                        principalColumn: "CycleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceFiles",
                columns: table => new
                {
                    EvidenceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScannedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScanResult = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceFiles", x => x.EvidenceFileId);
                    table.CheckConstraint("CK_EvidenceFiles_Size", "[SizeBytes]>0 AND [SizeBytes]<=10485760 AND [SequenceNo] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_EvidenceFiles_UploadAttempts_UploadAttemptId",
                        column: x => x.UploadAttemptId,
                        principalTable: "UploadAttempts",
                        principalColumn: "UploadAttemptId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MilestoneId_CandidateId",
                table: "Assignments",
                columns: new[] { "MilestoneId", "CandidateId" },
                unique: true,
                filter: "[CandidateId] IS NOT NULL AND [MilestoneId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_OfferingId_CandidateId",
                table: "Assignments",
                columns: new[] { "OfferingId", "CandidateId" },
                unique: true,
                filter: "[OfferingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_OfferingId_CycleId",
                table: "Assignments",
                columns: new[] { "OfferingId", "CycleId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assignments_Target",
                table: "Assignments",
                sql: "([OfferingId] IS NULL AND [ProjectId] IS NOT NULL AND [MilestoneId] IS NOT NULL) OR ([OfferingId] IS NOT NULL AND [ProjectId] IS NULL AND [MilestoneId] IS NULL AND [CandidateId] IS NOT NULL AND [TeamId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CourseId_CycleId",
                table: "CourseOfferings",
                columns: new[] { "CourseId", "CycleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CreatedBy",
                table: "CourseOfferings",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CycleId",
                table: "CourseOfferings",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_CreatedBy",
                table: "Courses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceFiles_ObjectKey",
                table: "EvidenceFiles",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceFiles_UploadAttemptId_SequenceNo",
                table: "EvidenceFiles",
                columns: new[] { "UploadAttemptId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadAttempts_ActorId_RequestId",
                table: "UploadAttempts",
                columns: new[] { "ActorId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadAttempts_RevisionId_AssignmentId",
                table: "UploadAttempts",
                columns: new[] { "RevisionId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadAttempts_Status_NextTryAt_LeaseUntil",
                table: "UploadAttempts",
                columns: new[] { "Status", "NextTryAt", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadAttempts_SubmissionId",
                table: "UploadAttempts",
                column: "SubmissionId",
                unique: true,
                filter: "[SubmissionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UploadAttempts_SubmissionId_AssignmentId",
                table: "UploadAttempts",
                columns: new[] { "SubmissionId", "AssignmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentCreditedRosters_Assignments_AssignmentId",
                table: "AssignmentCreditedRosters",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_CourseOfferings_OfferingId_CycleId",
                table: "Assignments",
                columns: new[] { "OfferingId", "CycleId" },
                principalTable: "CourseOfferings",
                principalColumns: new[] { "OfferingId", "CycleId" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
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
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("THROW 51034, 'Course/file evidence downgrade is unsafe after retaining data; use a reviewed forward repair.', 1;");
        }
    }
}
