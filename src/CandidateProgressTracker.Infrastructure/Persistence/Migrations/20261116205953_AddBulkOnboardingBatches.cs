using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkOnboardingBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingBatches",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CohortId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingBatches", x => x.BatchId);
                    table.ForeignKey(
                        name: "FK_OnboardingBatches_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingBatches_Cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohorts",
                        principalColumn: "CohortId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingBatchRows",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Row = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpectedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingBatchRows", x => new { x.BatchId, x.Row });
                    table.CheckConstraint("CK_OnboardingBatchRows_Action", "[Action] IN ('Create','EnrolExisting','Skip')");
                    table.CheckConstraint("CK_OnboardingBatchRows_Row", "[Row] > 1");
                    table.CheckConstraint("CK_OnboardingBatchRows_Status", "[Status] IN ('Pending','Failed','Created','Enrolled','Skipped')");
                    table.ForeignKey(
                        name: "FK_OnboardingBatchRows_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingBatchRows_CohortMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "CohortMemberships",
                        principalColumn: "MembershipId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingBatchRows_OnboardingBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "OnboardingBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingBatches_ActorId",
                table: "OnboardingBatches",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingBatches_CohortId",
                table: "OnboardingBatches",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingBatchRows_MembershipId",
                table: "OnboardingBatchRows",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingBatchRows_UserId",
                table: "OnboardingBatchRows",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [OnboardingBatches]) OR EXISTS (SELECT 1 FROM [OnboardingBatchRows]) THROW 51030, 'Cannot remove populated onboarding history.', 1;");
            migrationBuilder.DropTable(
                name: "OnboardingBatchRows");

            migrationBuilder.DropTable(
                name: "OnboardingBatches");
        }
    }
}
