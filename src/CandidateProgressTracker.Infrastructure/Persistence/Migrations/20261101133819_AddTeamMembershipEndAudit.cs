using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMembershipEndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "TeamMemberships",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndedBy",
                table: "TeamMemberships",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_EndedBy",
                table: "TeamMemberships",
                column: "EndedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMemberships_AspNetUsers_EndedBy",
                table: "TeamMemberships",
                column: "EndedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamMemberships_AspNetUsers_EndedBy",
                table: "TeamMemberships");

            migrationBuilder.DropIndex(
                name: "IX_TeamMemberships_EndedBy",
                table: "TeamMemberships");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "TeamMemberships");

            migrationBuilder.DropColumn(
                name: "EndedBy",
                table: "TeamMemberships");
        }
    }
}
