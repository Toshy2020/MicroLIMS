using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestsSelfAssignPermission : Migration
    {
        private const string Process = "Migration 20261114075222_AddTestsSelfAssignPermission";

        // Data only, no schema change. Adds Tests.SelfAssign and grants it to the built-in Analyst role (RoleType.Analyst = 3).
        // Custom roles, Section Head and Reviewer do not get it: a lab grants it on the Roles screen where it wants it.
        // RoleGrantSql never grants to a role that manages users or roles, bumps PermissionVersion and writes the audit row.
        // DbSeeder covers a fresh database.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'Tests.SelfAssign', 'Take an unassigned test in your own laboratory section and release it again while nothing is recorded on it.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'Tests.SelfAssign');");
            migrationBuilder.Sql(@"UPDATE ""Permissions"" SET ""IsEnforced"" = TRUE WHERE ""Code"" = 'Tests.SelfAssign';");

            migrationBuilder.Sql(RoleGrantSql.Grant(@"ro.""Type"" = 3 AND ro.""IsSystemRole""",
                new[] { "Tests.SelfAssign" }, Process, "Analysts can take unassigned tests in their own section"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(new[] { "Tests.SelfAssign" }, Process + " (Down)", "Tests.SelfAssign rolled back (migration Down)"));
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'Tests.SelfAssign';");
        }
    }
}
