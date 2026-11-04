using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.AssignmentId);
                    table.CheckConstraint("CK_Assignments_ExactlyOneOwner", "([CandidateId] IS NOT NULL AND [TeamId] IS NULL)\r\nOR\r\n([CandidateId] IS NULL AND [TeamId] IS NOT NULL)");
                    table.CheckConstraint("CK_Assignments_UtcTimestamps", "DATEPART(TZOFFSET, [DueAt]) = 0\r\nAND DATEPART(TZOFFSET, [CreatedAt]) = 0");
                    table.ForeignKey(
                        name: "FK_Assignments_AspNetUsers_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Milestones_MilestoneId_ProjectId",
                        columns: x => new { x.MilestoneId, x.ProjectId },
                        principalTable: "Milestones",
                        principalColumns: new[] { "MilestoneId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_ProjectTeams_TeamId_ProjectId",
                        columns: x => new { x.TeamId, x.ProjectId },
                        principalTable: "ProjectTeams",
                        principalColumns: new[] { "TeamId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Projects_ProjectId_CycleId",
                        columns: x => new { x.ProjectId, x.CycleId },
                        principalTable: "Projects",
                        principalColumns: new[] { "ProjectId", "CycleId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentRevisions",
                columns: table => new
                {
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    InstructionsSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AcceptanceSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidencePolicySnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRevisions", x => x.RevisionId);
                    table.UniqueConstraint("AK_AssignmentRevisions_RevisionId_AssignmentId", x => new { x.RevisionId, x.AssignmentId });
                    table.CheckConstraint("CK_AssignmentRevisions_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.CheckConstraint("CK_AssignmentRevisions_RevisionNo", "[RevisionNo] > 0");
                    table.CheckConstraint("CK_AssignmentRevisions_UtcTimestamps", "DATEPART(TZOFFSET, [DueAt]) = 0\r\nAND DATEPART(TZOFFSET, [ChangedAt]) = 0");
                    table.ForeignKey(
                        name: "FK_AssignmentRevisions_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentRevisions_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRevisions_AssignmentId_RevisionNo",
                table: "AssignmentRevisions",
                columns: new[] { "AssignmentId", "RevisionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRevisions_ChangedBy",
                table: "AssignmentRevisions",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_CandidateId",
                table: "Assignments",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_CreatedBy",
                table: "Assignments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_CycleId_CandidateId_DueAt",
                table: "Assignments",
                columns: new[] { "CycleId", "CandidateId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MilestoneId_CandidateId",
                table: "Assignments",
                columns: new[] { "MilestoneId", "CandidateId" },
                unique: true,
                filter: "[CandidateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MilestoneId_ProjectId",
                table: "Assignments",
                columns: new[] { "MilestoneId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MilestoneId_TeamId",
                table: "Assignments",
                columns: new[] { "MilestoneId", "TeamId" },
                unique: true,
                filter: "[TeamId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ProjectId_CycleId",
                table: "Assignments",
                columns: new[] { "ProjectId", "CycleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TeamId_DueAt",
                table: "Assignments",
                columns: new[] { "TeamId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TeamId_ProjectId",
                table: "Assignments",
                columns: new[] { "TeamId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentRevisions");

            migrationBuilder.DropTable(
                name: "Assignments");
        }
    }
}
