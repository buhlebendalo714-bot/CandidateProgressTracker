using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndividualSubmissionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Submissions_SubmissionId_AssignmentId",
                table: "Submissions",
                columns: new[] { "SubmissionId", "AssignmentId" });

            migrationBuilder.CreateTable(
                name: "SubmissionReviews",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewNo = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<byte>(type: "tinyint", nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    ReviewerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionReviews", x => x.ReviewId);
                    table.UniqueConstraint("AK_SubmissionReviews_ReviewId_AssignmentId", x => new { x.ReviewId, x.AssignmentId });
                    table.CheckConstraint("CK_SubmissionReviews_Decision", "[Decision] IN (1, 2)");
                    table.CheckConstraint("CK_SubmissionReviews_Feedback", "DATALENGTH([Feedback]) <= 20000 AND ([Decision] <> 2 OR LEN(LTRIM(RTRIM([Feedback]))) > 0)");
                    table.CheckConstraint("CK_SubmissionReviews_Number", "[ReviewNo] > 0");
                    table.CheckConstraint("CK_SubmissionReviews_Request", "[RequestId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_SubmissionReviews_Utc", "DATEPART(TZOFFSET, [DecidedAt]) = 0");
                    table.ForeignKey(
                        name: "FK_SubmissionReviews_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionReviews_Submissions_SubmissionId_AssignmentId",
                        columns: x => new { x.SubmissionId, x.AssignmentId },
                        principalTable: "Submissions",
                        principalColumns: new[] { "SubmissionId", "AssignmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentReopenings",
                columns: table => new
                {
                    ReopeningId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdministratorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReopenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentReopenings", x => x.ReopeningId);
                    table.CheckConstraint("CK_AssignmentReopenings_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.CheckConstraint("CK_AssignmentReopenings_Request", "[RequestId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_AssignmentReopenings_Utc", "DATEPART(TZOFFSET, [ReopenedAt]) = 0");
                    table.ForeignKey(
                        name: "FK_AssignmentReopenings_AspNetUsers_AdministratorId",
                        column: x => x.AdministratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentReopenings_SubmissionReviews_ApprovalReviewId_AssignmentId",
                        columns: x => new { x.ApprovalReviewId, x.AssignmentId },
                        principalTable: "SubmissionReviews",
                        principalColumns: new[] { "ReviewId", "AssignmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReopenings_AdministratorId_RequestId",
                table: "AssignmentReopenings",
                columns: new[] { "AdministratorId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReopenings_ApprovalReviewId",
                table: "AssignmentReopenings",
                column: "ApprovalReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReopenings_ApprovalReviewId_AssignmentId",
                table: "AssignmentReopenings",
                columns: new[] { "ApprovalReviewId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReviews_AssignmentId_ReviewNo",
                table: "SubmissionReviews",
                columns: new[] { "AssignmentId", "ReviewNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReviews_ReviewerId_RequestId",
                table: "SubmissionReviews",
                columns: new[] { "ReviewerId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReviews_SubmissionId_AssignmentId",
                table: "SubmissionReviews",
                columns: new[] { "SubmissionId", "AssignmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentReopenings");

            migrationBuilder.DropTable(
                name: "SubmissionReviews");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Submissions_SubmissionId_AssignmentId",
                table: "Submissions");
        }
    }
}
