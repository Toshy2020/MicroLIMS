using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SecurityAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    ActorIsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    TargetUserId = table.Column<int>(type: "integer", nullable: true),
                    TargetUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityAuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SecurityAuditEvents_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_ActorUserId",
                table: "SecurityAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_CorrelationId",
                table: "SecurityAuditEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_EventCode_OccurredAtUtc",
                table: "SecurityAuditEvents",
                columns: new[] { "EventCode", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_OccurredAtUtc",
                table: "SecurityAuditEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_TargetUserId_OccurredAtUtc",
                table: "SecurityAuditEvents",
                columns: new[] { "TargetUserId", "OccurredAtUtc" });

            // DbSeeder.SeedPermissionsAndGrants cannot deliver this code to a
            // database that already holds permission rows, so the row and its
            // System Administrator grant are inserted here instead. Both
            // statements are idempotent and no-op on a fresh database, where
            // the seeder does the work (Roles do not exist yet at this point).
            //
            // Granted to System Administrator only - deliberately not bundled
            // with Audit.View, which governs the GxP audit trail. The two are
            // separate audit domains and are not granted together.
            //
            // IsEnforced stays FALSE: the code is declared but no endpoint
            // checks it yet, and the Roles screen shows it as "Legacy-only"
            // until one does.
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'System.ViewSecurityAudit', 'View the security audit trail (authentication, session and account security events).', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'System.ViewSecurityAudit');");

            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r
                CROSS JOIN ""Permissions"" p
                WHERE r.""Type"" = 0 -- RoleType.SystemAdministrator
                  AND p.""Code"" = 'System.ViewSecurityAudit'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""RolePermissions"" rp
                      WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Grant first, then the permission row - the reverse of Up, and
            // required because RolePermissions references Permissions.
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions""
                WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'System.ViewSecurityAudit');");

            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'System.ViewSecurityAudit';");

            // Dropping this table discards security evidence. Reverting is a
            // deliberate act - take a copy of SecurityAuditEvents first if the
            // events matter. No GxP AuditLog row is touched either way.
            migrationBuilder.DropTable(
                name: "SecurityAuditEvents");
        }
    }
}
