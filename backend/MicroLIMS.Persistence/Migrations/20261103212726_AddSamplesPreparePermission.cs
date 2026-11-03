using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplesPreparePermission : Migration
    {
        // Data only. Sample preparation writes used to need just a login; they now need samples.prepare.
        // Every role whose base type is 0-3 (SystemAdministrator, SectionHead, Reviewer, Analyst), system or
        // custom, receives it so nobody who prepares today loses the screen. A custom role keeps its own
        // grant list, but its Type is the base the old [Authorize] passed it by, so Type is the test.
        // QC Manager (4) is approval-only and is left out. DbSeeder covers a fresh database.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'samples.prepare', 'Prepare a sample for testing: record, confirm or batch-confirm a sample preparation.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'samples.prepare');");
            migrationBuilder.Sql(@"UPDATE ""Permissions"" SET ""IsEnforced"" = TRUE WHERE ""Code"" = 'samples.prepare';");

            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r CROSS JOIN ""Permissions"" p
                WHERE p.""Code"" = 'samples.prepare'
                  AND r.""Type"" IN (0, 1, 2, 3)
                  AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                  WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'samples.prepare');");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'samples.prepare';");
        }
    }
}
