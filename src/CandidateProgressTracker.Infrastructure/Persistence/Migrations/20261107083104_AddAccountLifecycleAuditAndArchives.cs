using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateProgressTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLifecycleAuditAndArchives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT NormalizedEmail FROM AspNetUsers WHERE NormalizedEmail IS NOT NULL GROUP BY NormalizedEmail HAVING COUNT(*)>1) THROW 51020, 'Duplicate normalized emails require explicit correction before this upgrade; no records were changed.', 1;");
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "AccountAccessStates",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    PermissionVersion = table.Column<long>(type: "bigint", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountAccessStates", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AccountAccessStates_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountAccessStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountChanges",
                columns: table => new
                {
                    AccountChangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountChanges", x => x.AccountChangeId);
                    table.ForeignKey(
                        name: "FK_AccountChanges_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountChanges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountCredentials",
                columns: table => new
                {
                    CredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IssuedBy = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCredentials", x => x.CredentialId);
                    table.CheckConstraint("CK_AccountCredentials_Expiry", "[ExpiresAt] > [IssuedAt] AND [Kind] IN ('Activation','Recovery')");
                    table.ForeignKey(
                        name: "FK_AccountCredentials_AspNetUsers_IssuedBy",
                        column: x => x.IssuedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    AuditEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.AuditEventId);
                });

            migrationBuilder.CreateTable(
                name: "ScopeArchiveEvents",
                columns: table => new
                {
                    ScopeArchiveEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Archived = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OutcomeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopeArchiveEvents", x => x.ScopeArchiveEventId);
                    table.CheckConstraint("CK_ScopeArchiveEvents_Kind", "[ScopeType] IN ('Project','Cycle') AND LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_ScopeArchiveEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountAccessStates_ChangedBy",
                table: "AccountAccessStates",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AccountChanges_ActorId",
                table: "AccountChanges",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountChanges_OccurredAt",
                table: "AccountChanges",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AccountChanges_UserId",
                table: "AccountChanges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCredentials_IssuedBy",
                table: "AccountCredentials",
                column: "IssuedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCredentials_TokenHash",
                table: "AccountCredentials",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountCredentials_UserId_IssuedAt",
                table: "AccountCredentials",
                columns: new[] { "UserId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt_AuditEventId",
                table: "AuditEvents",
                columns: new[] { "OccurredAt", "AuditEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScopeArchiveEvents_ActorId_RequestId",
                table: "ScopeArchiveEvents",
                columns: new[] { "ActorId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScopeArchiveEvents_ScopeId_OccurredAt",
                table: "ScopeArchiveEvents",
                columns: new[] { "ScopeId", "OccurredAt" });
            foreach(var table in new[]{"AuditEvents","AccountChanges","ScopeArchiveEvents"})
                migrationBuilder.Sql($"CREATE TRIGGER [TR_{table}_AppendOnly] ON [{table}] INSTEAD OF UPDATE, DELETE AS BEGIN THROW 51021, 'Audit records are append-only.', 1; END;");
            migrationBuilder.Sql("CREATE TRIGGER [TR_ScopeArchiveEvents_Scope] ON [ScopeArchiveEvents] AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted i WHERE (i.ScopeType='Project' AND NOT EXISTS(SELECT 1 FROM Projects p WHERE p.ProjectId=i.ScopeId)) OR (i.ScopeType='Cycle' AND NOT EXISTS(SELECT 1 FROM LearningCycles c WHERE c.CycleId=i.ScopeId))) THROW 51022, 'Archive scope does not exist.', 1; END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Downgrade would discard account credentials and protected audit history. Restore a verified backup instead.");
        }
    }
}
