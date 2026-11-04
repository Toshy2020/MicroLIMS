using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentReassignment : Migration
    {
        // 1. Users.DeactivatedAt: when an account was last deactivated, so reassignment waiting time can be measured.
        //    Backfilled from the append-only audit trail (the latest USER_DISABLED row of each currently inactive user);
        //    accounts deactivated before the audit trail recorded it stay null and are never flagged overdue.
        // 2. Documents.Reassign: the new permission (enforced) and its grant to the built-in Section Head role
        //    (Type 1 = SectionHead AND IsSystemRole), never to a role that also manages users or roles
        //    (segregation of duties). Raw SQL bypasses RoleService, so the audit row and the PermissionVersion bump
        //    (which invalidates issued tokens) are written here.
        private const string Process = "Migration 20261027133758_DocumentReassignment";
        private const string Reason = "Section Heads hand over document responsibilities of deactivated users (owner decision)";
        private const string Description = "Hand over the document responsibilities of a deactivated user to another active member of the same department.";

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
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Users" u SET "DeactivatedAt" = d."At"
                FROM (SELECT a."EntityId", MAX(a."Timestamp") AS "At" FROM "AuditLogs" a
                      WHERE a."EntityName" = 'User' AND a."Action" = 'USER_DISABLED' GROUP BY a."EntityId") d
                WHERE NOT u."IsActive" AND d."EntityId" = u."Id"::text;
                """);

            migrationBuilder.Sql($"""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'Documents.Reassign', '{Description}', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'Documents.Reassign');
                """);
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'Documents.Reassign';""");

            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT ro."Id" FROM "Roles" ro
                         WHERE ro."Type" = 1 AND ro."IsSystemRole"
                           AND NOT EXISTS (
                               SELECT 1 FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                               WHERE rp."RoleId" = ro."Id" AND p."Code" IN ('Users.Manage', 'Roles.Manage')) LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                    SELECT r."Id", p."Id" FROM "Permissions" p
                    WHERE p."Code" = 'Documents.Reassign'
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", Process).Replace("{REASON}", Reason) +
                "\nEND LOOP;\nEND $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revoke the grant this migration made (recorded in its own audit row) with an audit row and a version bump,
            // then drop every remaining grant and the permission row, then the column.
            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                $$"""
                FOR r IN SELECT DISTINCT a."EntityId"::int AS "Id" FROM "AuditLogs" a
                         WHERE a."SystemProcessName" = '{{Process}}'
                           AND a."NewValue"::jsonb -> 'Granted' ? 'Documents.Reassign' LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    DELETE FROM "RolePermissions"
                    WHERE "RoleId" = r."Id"
                      AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Documents.Reassign');

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", Process + " (Down)").Replace("{REASON}", "Section Head reassignment grant rolled back (migration Down)") +
                "\nEND LOOP;\nEND $$;");
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Documents.Reassign');
                """);
            migrationBuilder.Sql("""DELETE FROM "Permissions" WHERE "Code" = 'Documents.Reassign';""");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Users");
        }
    }
}
