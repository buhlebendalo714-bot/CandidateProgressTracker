using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipEndingDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "CohortMemberships",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndedAt",
                table: "CohortMemberships",
                type: "datetimeoffset(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndedBy",
                table: "CohortMemberships",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortMemberships_EndedBy",
                table: "CohortMemberships",
                column: "EndedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_CohortMemberships_AspNetUsers_EndedBy",
                table: "CohortMemberships",
                column: "EndedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CohortMemberships_AspNetUsers_EndedBy",
                table: "CohortMemberships");

            migrationBuilder.DropIndex(
                name: "IX_CohortMemberships_EndedBy",
                table: "CohortMemberships");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "CohortMemberships");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "CohortMemberships");

            migrationBuilder.DropColumn(
                name: "EndedBy",
                table: "CohortMemberships");
        }
    }
}
