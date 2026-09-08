using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorMonitoringEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    SeverityOverride = table.Column<int>(type: "integer", nullable: true),
                    ExceptionType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    StackTrace = table.Column<string>(type: "text", nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RawContext = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErrorLogs_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ErrorLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CorrelationId",
                table: "ErrorLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_IncidentId",
                table: "ErrorLogs",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OccurredAtUtc",
                table: "ErrorLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_UserId",
                table: "ErrorLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CorrelationId",
                table: "Incidents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_LastSeenUtc",
                table: "Incidents",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ResolvedByUserId",
                table: "Incidents",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Severity",
                table: "Incidents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Status",
                table: "Incidents",
                column: "Status");

            // DbSeeder.SeedPermissionsAndGrants cannot deliver this code to a
            // database that already holds permission rows, so the row and its
            // System Administrator grant are inserted here instead. Both
            // statements are idempotent and no-op on a fresh database, where
            // the seeder does the work (Roles do not exist yet at this point).
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'System.ViewErrorLog', 'View the technical error log and monitoring page.', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'System.ViewErrorLog');");

            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r
                CROSS JOIN ""Permissions"" p
                WHERE r.""Type"" = 0 -- RoleType.SystemAdministrator
                  AND p.""Code"" = 'System.ViewErrorLog'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""RolePermissions"" rp
                      WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions""
                WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'System.ViewErrorLog');");

            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'System.ViewErrorLog';");

            migrationBuilder.DropTable(
                name: "ErrorLogs");

            migrationBuilder.DropTable(
                name: "Incidents");
        }
    }
}
