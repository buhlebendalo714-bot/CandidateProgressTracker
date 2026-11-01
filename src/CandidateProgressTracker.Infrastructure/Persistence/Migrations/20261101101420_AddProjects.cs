using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Brief = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
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
                    table.PrimaryKey("PK_Projects", x => x.ProjectId);
                    table.UniqueConstraint("AK_Projects_ProjectId_CycleId", x => new { x.ProjectId, x.CycleId });
                    table.CheckConstraint("CK_Projects_ArchiveDetails", "([IsArchived] = 0 AND [ArchivedAt] IS NULL\r\n    AND [ArchivedBy] IS NULL AND [ArchiveReason] IS NULL)\r\nOR\r\n([IsArchived] = 1 AND [ArchivedAt] IS NOT NULL\r\n    AND [ArchivedBy] IS NOT NULL\r\n    AND [ArchiveReason] IS NOT NULL\r\n    AND LEN(LTRIM(RTRIM([ArchiveReason]))) > 0)");
                    table.CheckConstraint("CK_Projects_Mode", "[Mode] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Projects_AspNetUsers_ArchivedBy",
                        column: x => x.ArchivedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_LearningCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "LearningCycles",
                        principalColumn: "CycleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ArchivedBy",
                table: "Projects",
                column: "ArchivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedBy",
                table: "Projects",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CycleId",
                table: "Projects",
                column: "CycleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
