using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCohortArchiveDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Cohorts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Cohorts",
                type: "datetimeoffset(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Cohorts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cohorts_ArchivedBy",
                table: "Cohorts",
                column: "ArchivedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Cohorts_AspNetUsers_ArchivedBy",
                table: "Cohorts",
                column: "ArchivedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cohorts_AspNetUsers_ArchivedBy",
                table: "Cohorts");

            migrationBuilder.DropIndex(
                name: "IX_Cohorts_ArchivedBy",
                table: "Cohorts");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Cohorts");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Cohorts");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Cohorts");
        }
    }
}
