using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQcManagerRole : Migration
    {
        /// <inheritdoc />
        // Data only. Not marked enforced yet: no endpoint checks these codes until the later tasks add them.
        // DbSeeder covers a fresh database (its role insert runs only when Roles is empty, so
        // the role row below is skipped there); this reaches ones already seeded.
        // RoleType: 0 = SystemAdministrator, 1 = SectionHead, 2 = Reviewer, 3 = Analyst, 4 = QcManager.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""Roles"" (""Type"", ""Name"", ""IsSystemRole"", ""IsActive"")
                SELECT 4, 'QC Manager', TRUE, TRUE
                WHERE EXISTS (SELECT 1 FROM ""Roles"")
                  AND NOT EXISTS (SELECT 1 FROM ""Roles"" WHERE ""Type"" = 4 AND ""IsSystemRole"");");

            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT v.code, v.descr, FALSE
                FROM (VALUES
                    ('tests.not-applicable.manage', 'Mark a test not applicable to an item and manage that request until final approval.'),
                    ('tests.not-applicable.final-approve', 'Give the final approval of a not-applicable test (QC Manager only).'),
                    ('tests.outsource', 'Outsource a test to an outside laboratory.')
                ) AS v(code, descr)
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" p WHERE p.""Code"" = v.code);");

            // QC Manager's read-only set is what Reviewer holds under a .View/.Read code: Discussions.View, MonitoringPlan.View.
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r CROSS JOIN ""Permissions"" p
                WHERE ((p.""Code"" IN ('tests.not-applicable.manage', 'tests.outsource') AND r.""Type"" IN (0, 1))
                    OR (p.""Code"" IN ('tests.not-applicable.final-approve', 'Discussions.View', 'MonitoringPlan.View') AND r.""Type"" = 4))
                  AND r.""IsSystemRole""
                  AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                  WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""Roles"" WHERE ""Type"" = 4 AND ""IsSystemRole"")
                OR ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" IN ('tests.not-applicable.manage', 'tests.not-applicable.final-approve', 'tests.outsource'));");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" IN ('tests.not-applicable.manage', 'tests.not-applicable.final-approve', 'tests.outsource');");
            // Kept if anyone already holds it: a user's role cannot be removed from under them.
            migrationBuilder.Sql(@"DELETE FROM ""Roles"" r WHERE r.""Type"" = 4 AND r.""IsSystemRole""
                AND NOT EXISTS (SELECT 1 FROM ""Users"" u WHERE u.""RoleId"" = r.""Id"");");
        }
    }
}
