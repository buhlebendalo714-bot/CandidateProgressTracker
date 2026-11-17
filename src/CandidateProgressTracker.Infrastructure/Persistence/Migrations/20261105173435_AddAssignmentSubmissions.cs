using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNo = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.SubmissionId);
                    table.CheckConstraint("CK_Submissions_AttemptNo", "[AttemptNo] > 0");
                    table.CheckConstraint("CK_Submissions_Explanation", "LEN(LTRIM(RTRIM([Explanation]))) > 0");
                    table.CheckConstraint("CK_Submissions_RequestId", "[RequestId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Submissions_TimestampOrder", "[RecordedAt] >= [ReceivedAt]");
                    table.CheckConstraint("CK_Submissions_UtcTimestamps", "DATEPART(TZOFFSET, [ReceivedAt]) = 0\r\nAND DATEPART(TZOFFSET, [RecordedAt]) = 0");
                    table.ForeignKey(
                        name: "FK_Submissions_AspNetUsers_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Submissions_AssignmentRevisions_AssignmentRevisionId_AssignmentId",
                        columns: x => new { x.AssignmentRevisionId, x.AssignmentId },
                        principalTable: "AssignmentRevisions",
                        principalColumns: new[] { "RevisionId", "AssignmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionEvidence",
                columns: table => new
                {
                    SubmissionEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionEvidence", x => x.SubmissionEvidenceId);
                    table.CheckConstraint("CK_SubmissionEvidence_Label", "LEN(LTRIM(RTRIM([Label]))) > 0");
                    table.CheckConstraint("CK_SubmissionEvidence_SequenceNo", "[SequenceNo] > 0");
                    table.CheckConstraint("CK_SubmissionEvidence_Url", "LEN(LTRIM(RTRIM([Url]))) > 0");
                    table.ForeignKey(
                        name: "FK_SubmissionEvidence_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "SubmissionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionEvidence_SubmissionId_SequenceNo",
                table: "SubmissionEvidence",
                columns: new[] { "SubmissionId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_AssignmentId_AttemptNo",
                table: "Submissions",
                columns: new[] { "AssignmentId", "AttemptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_AssignmentRevisionId_AssignmentId",
                table: "Submissions",
                columns: new[] { "AssignmentRevisionId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_SubmittedBy_RequestId",
                table: "Submissions",
                columns: new[] { "SubmittedBy", "RequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionEvidence");

            migrationBuilder.DropTable(
                name: "Submissions");
        }
    }
}
