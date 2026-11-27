using Microsoft.EntityFrameworkCore.Migrations;

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOosInvestigationPermissions : Migration
    {
        private const string Process = "Migration 20261120131800_AddOosInvestigationPermissions";
        private const string Reason = "Provision out-of-specification investigation permissions";
        private static readonly string[] Codes = ["Oos.View", "Oos.Contribute", "Oos.RequestDeviation"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT v.code, v.description, TRUE
                FROM (VALUES
                    ('Oos.View', 'View out-of-specification investigations.'),
                    ('Oos.Contribute', 'Contribute statements and checklist answers to out-of-specification investigations.'),
                    ('Oos.RequestDeviation', 'Request a deviation on an out-of-specification investigation.')
                ) AS v(code, description)
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" p WHERE p."Code" = v.code);

                UPDATE "Permissions" SET "IsEnforced" = TRUE
                WHERE "Code" IN ('Oos.View', 'Oos.Contribute', 'Oos.RequestDeviation') AND NOT "IsEnforced";
                """);

            migrationBuilder.Sql(RoleGrantSql.Grant(
                """ro."Type" IN (1, 3) AND ro."IsSystemRole" IS TRUE""",
                ["Oos.View", "Oos.Contribute"], Process, Reason));
            migrationBuilder.Sql(RoleGrantSql.Grant(
                """ro."Type" IN (2, 4) AND ro."IsSystemRole" IS TRUE""",
                ["Oos.View"], Process, Reason));
            migrationBuilder.Sql(RoleGrantSql.Grant(
                """ro."Type" = 5 AND ro."IsSystemRole" IS TRUE""",
                ["Oos.View", "Oos.RequestDeviation"], Process, Reason));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(Codes, Process + " (Down)",
                "Roll back out-of-specification investigation permissions"));
            migrationBuilder.Sql("""
                DELETE FROM "Permissions" WHERE "Code" IN ('Oos.View', 'Oos.Contribute', 'Oos.RequestDeviation');
                """);
        }
    }
}
