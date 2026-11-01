using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectTeams",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTeams", x => x.TeamId);
                    table.UniqueConstraint("AK_ProjectTeams_TeamId_ProjectId", x => new { x.TeamId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_ProjectTeams_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectTeams_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamMemberships",
                columns: table => new
                {
                    TeamMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMemberships", x => x.TeamMembershipId);
                    table.CheckConstraint("CK_TeamMemberships_DateOrder", "[EndsAt] IS NULL OR [EndsAt] > [StartsAt]");
                    table.ForeignKey(
                        name: "FK_TeamMemberships_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMemberships_ProjectTeams_TeamId_ProjectId",
                        columns: x => new { x.TeamId, x.ProjectId },
                        principalTable: "ProjectTeams",
                        principalColumns: new[] { "TeamId", "ProjectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTeams_CreatedBy",
                table: "ProjectTeams",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTeams_ProjectId_Name",
                table: "ProjectTeams",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_ChangedBy",
                table: "TeamMemberships",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_ProjectId_UserId",
                table: "TeamMemberships",
                columns: new[] { "ProjectId", "UserId" },
                unique: true,
                filter: "[EndsAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_TeamId_ProjectId",
                table: "TeamMemberships",
                columns: new[] { "TeamId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_UserId",
                table: "TeamMemberships",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamMemberships");

            migrationBuilder.DropTable(
                name: "ProjectTeams");
        }
    }
}
