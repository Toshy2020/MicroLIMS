using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LabSeparationPrivileges : Migration
    {
        /// <inheritdoc />
        // No model changes - DbSeeder.SeedPermissionsAndGrants seeds these two
        // permission codes on a fresh database. This migration carries the same
        // two INSERTs for databases that already exist, following
        // 20260909204150_AddSecurityAuditEvents. Both statements are idempotent
        // and no-op where the seeder already did the work.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (code, description) in new[]
            {
                ("Samples.Receive", "Receive samples for any laboratory on the main Receiving page and add a laboratory to a sample."),
                ("Samples.TrackAll", "View the sample tracking board across every laboratory.")
            })
            {
                migrationBuilder.Sql($@"
                    INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                    SELECT '{code}', '{description}', FALSE
                    WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = '{code}');");
                migrationBuilder.Sql($@"
                    INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                    SELECT r.""Id"", p.""Id""
                    FROM ""Roles"" r CROSS JOIN ""Permissions"" p
                    WHERE r.""Type"" IN (0, 1) -- SystemAdministrator, SectionHead
                      AND p.""Code"" = '{code}'
                      AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                      WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN
                (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" IN ('Samples.Receive','Samples.TrackAll'));");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" IN ('Samples.Receive','Samples.TrackAll');");
        }
    }
}
