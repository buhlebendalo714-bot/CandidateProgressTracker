using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCohortMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CohortMemberships",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CohortId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MembershipKind = table.Column<byte>(type: "tinyint", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortMemberships", x => x.MembershipId);
                    table.CheckConstraint("CK_CohortMemberships_DateOrder", "[EndsAt] IS NULL OR [EndsAt] > [StartsAt]");
                    table.CheckConstraint("CK_CohortMemberships_Kind", "[MembershipKind] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_CohortMemberships_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortMemberships_Cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohorts",
                        principalColumn: "CohortId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CohortMemberships_CohortId_UserId_MembershipKind",
                table: "CohortMemberships",
                columns: new[] { "CohortId", "UserId", "MembershipKind" },
                unique: true,
                filter: "[EndsAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMemberships_CreatedBy",
                table: "CohortMemberships",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMemberships_UserId",
                table: "CohortMemberships",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CohortMemberships");
        }
    }
}
