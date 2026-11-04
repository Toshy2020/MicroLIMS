using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQcManagerRole : Migration
    {
        private const string Process = "Migration 20261103174920_AddQcManagerRole";

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

            // Section Head (built-in only) manages not-applicable tests and outsourcing; the System Administrator
            // does not (segregation of duties: administrators never execute GxP work). The QC Manager gets final
            // approval plus the reviewer's read-only codes, Discussions.View and MonitoringPlan.View.
            migrationBuilder.Sql(RoleGrantSql.Grant(@"ro.""Type"" = 1 AND ro.""IsSystemRole""",
                new[] { "tests.not-applicable.manage", "tests.outsource" }, Process, "Section Heads manage not-applicable and outsourced tests"));
            migrationBuilder.Sql(RoleGrantSql.Grant(@"ro.""Type"" = 4 AND ro.""IsSystemRole""",
                new[] { "tests.not-applicable.final-approve", "Discussions.View", "MonitoringPlan.View" }, Process, "QC Manager role created"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(new[] { "tests.not-applicable.manage", "tests.not-applicable.final-approve", "tests.outsource" },
                Process + " (Down)", "Not-applicable and outsourcing permissions rolled back (migration Down)"));
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""Roles"" WHERE ""Type"" = 4 AND ""IsSystemRole"");");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" IN ('tests.not-applicable.manage', 'tests.not-applicable.final-approve', 'tests.outsource');");
            // Kept if anyone already holds it: a user's role cannot be removed from under them.
            migrationBuilder.Sql(@"DELETE FROM ""Roles"" r WHERE r.""Type"" = 4 AND r.""IsSystemRole""
                AND NOT EXISTS (SELECT 1 FROM ""Users"" u WHERE u.""RoleId"" = r.""Id"");");
        }
    }
}
