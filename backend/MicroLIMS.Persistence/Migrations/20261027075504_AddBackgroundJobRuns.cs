using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobRuns : Migration
    {
        // Grants System.ManageJobs to the built-in System Administrator only (Type 0 AND IsSystemRole), following
        // GrantSystemAdministratorConfigureAllSections: raw SQL bypasses RoleService, so the audit row and the
        // PermissionVersion bump (which invalidates issued tokens) are written here.
        private const string Reason = "Administrators view background job status and run housekeeping jobs";

        private const string AuditAndBump = """
                    SELECT COALESCE(array_agg(p."Code" ORDER BY p."Code"), '{}') INTO new_codes
                    FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                    WHERE rp."RoleId" = r."Id";
                    granted := ARRAY(SELECT unnest(new_codes) EXCEPT SELECT unnest(prev_codes));
                    revoked := ARRAY(SELECT unnest(prev_codes) EXCEPT SELECT unnest(new_codes));
                    IF cardinality(granted) > 0 OR cardinality(revoked) > 0 THEN
                        UPDATE "Roles" SET "PermissionVersion" = "PermissionVersion" + 1 WHERE "Id" = r."Id";
                        INSERT INTO "AuditLogs" (
                            "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                            "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                        VALUES (
                            'Role', r."Id"::text, 'ROLE_PERMISSIONS_UPDATED',
                            jsonb_build_object('PermissionCodes', to_jsonb(prev_codes))::text,
                            jsonb_build_object('PermissionCodes', to_jsonb(new_codes), 'Granted', to_jsonb(granted), 'Revoked', to_jsonb(revoked))::text,
                            0, now(), 2, '{PROCESS}', 'RolePermissionsUpdated', 7, '{REASON}');
                    END IF;
            """;

        private const string Declarations = """
            DECLARE
                r RECORD;
                prev_codes text[];
                new_codes text[];
                granted text[];
                revoked text[];
            """;

        private const string SnapshotPrevious = """
                    SELECT COALESCE(array_agg(p."Code" ORDER BY p."Code"), '{}') INTO prev_codes
                    FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                    WHERE rp."RoleId" = r."Id";
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackgroundJobRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ItemsProcessed = table.Column<int>(type: "integer", nullable: true),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TriggeredBy = table.Column<int>(type: "integer", nullable: false),
                    TriggeredByUserId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRuns_JobName_StartedAt",
                table: "BackgroundJobRuns",
                columns: new[] { "JobName", "StartedAt" });

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'System.ManageJobs', 'View background job status and run history; run housekeeping jobs manually.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'System.ManageJobs');
                """);
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'System.ManageJobs';""");

            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT "Id" FROM "Roles" WHERE "Type" = 0 AND "IsSystemRole" LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                    SELECT r."Id", p."Id" FROM "Permissions" p
                    WHERE p."Code" = 'System.ManageJobs'
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", "Migration 20261027075504_AddBackgroundJobRuns").Replace("{REASON}", Reason) +
                "\nEND LOOP;\nEND $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT "Id" FROM "Roles" WHERE "Type" = 0 AND "IsSystemRole" LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    DELETE FROM "RolePermissions"
                    WHERE "RoleId" = r."Id"
                      AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'System.ManageJobs');

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", "Migration 20261027075504_AddBackgroundJobRuns (Down)").Replace("{REASON}", "Manage-jobs grant rolled back (migration Down)") +
                "\nEND LOOP;\nEND $$;");
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'System.ManageJobs');
                """);
            migrationBuilder.Sql("""DELETE FROM "Permissions" WHERE "Code" = 'System.ManageJobs';""");


            migrationBuilder.DropTable(
                name: "BackgroundJobRuns");
        }
    }
}
