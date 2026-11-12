using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQaRole : Migration
    {
        private const string Process = "Migration 20261112205032_AddQaRole";

        /// <inheritdoc />
        // Data only. DbSeeder covers a fresh database (its role insert runs only when Roles is empty,
        // so the role row below is skipped there); this reaches ones already seeded.
        // RoleType: 0 = SystemAdministrator, 1 = SectionHead, 2 = Reviewer, 3 = Analyst, 4 = QcManager, 5 = Qa.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""Roles"" (""Type"", ""Name"", ""IsSystemRole"", ""IsActive"")
                SELECT 5, 'QA', TRUE, TRUE
                WHERE EXISTS (SELECT 1 FROM ""Roles"")
                  AND NOT EXISTS (SELECT 1 FROM ""Roles"" WHERE ""Type"" = 5 AND ""IsSystemRole"");");

            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT v.code, v.descr, TRUE
                FROM (VALUES
                    ('Audit.Review', 'Perform and sign an audit trail review.'),
                    ('Access.Review.Sign', 'Decide and sign a user access review (Quality Assurance).')
                ) AS v(code, descr)
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" p WHERE p.""Code"" = v.code);");

            migrationBuilder.Sql(@"
                UPDATE ""Permissions"" SET ""IsEnforced"" = TRUE
                WHERE ""Code"" IN ('Audit.Review', 'Access.Review.Sign') AND NOT ""IsEnforced"";");

            migrationBuilder.Sql(RoleGrantSql.Grant(
                @"ro.""Type"" = 5 AND ro.""IsSystemRole""",
                new[]
                {
                    "Discussions.View",
                    "MonitoringPlan.View",
                    "Audit.View",
                    "Audit.Review",
                    "Access.Review",
                    "Access.Review.Sign",
                    "Sections.ViewAll",
                    "Samples.TrackAll",
                    "Dashboards.LabOverview",
                    "Dashboards.Review",
                    "Kpi.View",
                    "Kpi.ViewTeam",
                    "Messages.Use",
                    "System.ViewSecurityAudit",
                    "Documents.Review",
                    "Documents.Approve",
                    "Documents.PeriodicReview",
                    "Documents.TrainingViewMatrix"
                },
                Process,
                "QA role created"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(
                new[] { "Audit.Review", "Access.Review.Sign" },
                Process + " (Down)",
                "QA permissions rolled back (migration Down)"));
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""Roles"" WHERE ""Type"" = 5 AND ""IsSystemRole"");");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" IN ('Audit.Review', 'Access.Review.Sign');");
            // Kept if anyone already holds it: a user's role cannot be removed from under them.
            migrationBuilder.Sql(@"DELETE FROM ""Roles"" r WHERE r.""Type"" = 5 AND r.""IsSystemRole""
                AND NOT EXISTS (SELECT 1 FROM ""Users"" u WHERE u.""RoleId"" = r.""Id"");");
        }
    }
}
