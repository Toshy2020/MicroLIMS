using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityPermissions : Migration
    {
        private const string Process = "Migration 20261124202014_AddQualityPermissions";
        private const string Reason = "Provision quality event permissions";
        private static readonly string[] Codes =
        [
            "Quality.View",
            "Quality.Raise",
            "Quality.Investigate",
            "Quality.LabConclude",
            "Quality.Approve"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT v.code, v.description, TRUE
                FROM (VALUES
                    ('Quality.View', 'View quality records (deviations, CAPAs, change controls, complaints).'),
                    ('Quality.Raise', 'Raise a deviation or propose a change control.'),
                    ('Quality.Investigate', 'Investigate quality records, write CAPA plans and assess change impacts.'),
                    ('Quality.LabConclude', 'Sign laboratory conclusions and laboratory reports on deviations and complaints.'),
                    ('Quality.Approve', 'Approve, verify, check effectiveness, close and cancel quality records and decide due-date extensions.')
                ) AS v(code, description)
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" p WHERE p."Code" = v.code);

                UPDATE "Permissions" SET "IsEnforced" = TRUE
                WHERE "Code" IN ('Quality.View', 'Quality.Raise', 'Quality.Investigate', 'Quality.LabConclude', 'Quality.Approve')
                  AND NOT "IsEnforced";
                """);

            migrationBuilder.Sql(RoleGrantSql.Grant(
                """ro."Type" = 1 AND ro."IsSystemRole" IS TRUE""",
                ["Quality.View", "Quality.Raise", "Quality.Investigate", "Quality.LabConclude"], Process, Reason));
            migrationBuilder.Sql(RoleGrantSql.Grant(
                """ro."Type" IN (2, 3) AND ro."IsSystemRole" IS TRUE""",
                ["Quality.View", "Quality.Raise"], Process, Reason));
            migrationBuilder.Sql(RoleGrantSql.Grant(
                """ro."Type" = 4 AND ro."IsSystemRole" IS TRUE""",
                ["Quality.View", "Quality.Raise", "Quality.LabConclude"], Process, Reason));
            migrationBuilder.Sql(RoleGrantSql.Grant(
                """ro."Type" = 5 AND ro."IsSystemRole" IS TRUE""",
                ["Quality.View", "Quality.Raise", "Quality.Approve"], Process, Reason));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(Codes, Process + " (Down)",
                "Roll back quality event permissions"));
            migrationBuilder.Sql("""
                DELETE FROM "Permissions"
                WHERE "Code" IN ('Quality.View', 'Quality.Raise', 'Quality.Investigate', 'Quality.LabConclude', 'Quality.Approve');
                """);
        }
    }
}
