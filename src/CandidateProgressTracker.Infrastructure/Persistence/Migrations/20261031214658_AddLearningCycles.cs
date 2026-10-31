using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningCycles",
                columns: table => new
                {
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CohortId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ArchivedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ArchiveReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningCycles", x => x.CycleId);
                    table.CheckConstraint("CK_LearningCycles_ArchiveDetails", "([IsArchived] = 0 AND [ArchivedAt] IS NULL\r\n    AND [ArchivedBy] IS NULL AND [ArchiveReason] IS NULL)\r\nOR\r\n([IsArchived] = 1 AND [ArchivedAt] IS NOT NULL\r\n    AND [ArchivedBy] IS NOT NULL\r\n    AND [ArchiveReason] IS NOT NULL\r\n    AND LEN(LTRIM(RTRIM([ArchiveReason]))) > 0)");
                    table.CheckConstraint("CK_LearningCycles_DateOrder", "[EndsOn] >= [StartsOn]");
                    table.ForeignKey(
                        name: "FK_LearningCycles_AspNetUsers_ArchivedBy",
                        column: x => x.ArchivedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningCycles_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningCycles_Cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohorts",
                        principalColumn: "CohortId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningCycles_ArchivedBy",
                table: "LearningCycles",
                column: "ArchivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LearningCycles_CohortId_Name",
                table: "LearningCycles",
                columns: new[] { "CohortId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningCycles_CreatedBy",
                table: "LearningCycles",
                column: "CreatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningCycles");
        }
    }
}
