using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantSectionHeadDocumentsReview : Migration
    {
        // Owner decision: the Section Head (Document Controller) may perform technical review, keeping the
        // fallback it had before Documents.Review was enforced. Granted only to the built-in role
        // (Type 1 = SectionHead AND IsSystemRole) and never to a role that also manages users or roles
        // (segregation of duties). Raw SQL bypasses RoleService, so PermissionVersion is bumped here.
        private const string Process = "Migration 20261027131619_GrantSectionHeadDocumentsReview";
        private const string Reason = "Section Heads perform technical review of documents (owner decision)";

        // Per role: snapshot the codes, apply the change, then (only if something changed) bump the
        // permission version and write the audit entry in the shape RoleService uses.
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
                    WHERE p."Code" = 'Documents.Review'
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", Process).Replace("{REASON}", Reason) +
                "\nEND LOOP;\nEND $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revoke only where this migration's own audit row records the grant, so a Review grant the role
            // already held (or received from EnforceDocumentControlPermissions) is kept.
            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                $$"""
                FOR r IN SELECT DISTINCT a."EntityId"::int AS "Id" FROM "AuditLogs" a
                         WHERE a."SystemProcessName" = '{{Process}}'
                           AND a."NewValue"::jsonb -> 'Granted' ? 'Documents.Review' LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    DELETE FROM "RolePermissions"
                    WHERE "RoleId" = r."Id"
                      AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Documents.Review');

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", Process + " (Down)").Replace("{REASON}", "Section Head review grant rolled back (migration Down)") +
                "\nEND LOOP;\nEND $$;");
        }
    }
}
