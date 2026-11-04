namespace MicroLIMS.Persistence.Migrations
{
    // Raw-SQL role grants in the shape GrantSectionHeadDocumentsReview set: never to a role that
    // manages users or roles (segregation of duties), and, per role that changed, PermissionVersion
    // is bumped (raw SQL bypasses RoleService) and an audit row records what was granted or revoked.
    internal static class RoleGrantSql
    {
        private const string Declarations = """
            DECLARE
                r RECORD;
                prev_codes text[];
                new_codes text[];
                granted text[];
                revoked text[];
            """;

        private const string Codes = """
            SELECT COALESCE(array_agg(p."Code" ORDER BY p."Code"), ARRAY[]::text[]) INTO {VAR}
            FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
            WHERE rp."RoleId" = r."Id";
            """;

        private const string AuditAndBump = """
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

        private static string InList(string[] codes) => string.Join(", ", codes.Select(c => $"'{c}'"));

        private static string Loop(string roleQuery, string change, string process, string reason) =>
            "DO $$\n" + Declarations + "\nBEGIN\nFOR r IN " + roleQuery + " LOOP\n"
            + Codes.Replace("{VAR}", "prev_codes") + "\n" + change + "\n" + Codes.Replace("{VAR}", "new_codes") + "\n"
            + AuditAndBump.Replace("{PROCESS}", process).Replace("{REASON}", reason)
            + "\nEND LOOP;\nEND $$;";

        // Grants codes to every role matching roleFilter (SQL over alias ro) that holds neither Users.Manage nor Roles.Manage.
        public static string Grant(string roleFilter, string[] codes, string process, string reason) => Loop(
            $"""
            SELECT ro."Id" FROM "Roles" ro
            WHERE ({roleFilter})
              AND NOT EXISTS (
                  SELECT 1 FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                  WHERE rp."RoleId" = ro."Id" AND p."Code" IN ('Users.Manage', 'Roles.Manage'))
            """,
            $"""
            INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
            SELECT r."Id", p."Id" FROM "Permissions" p
            WHERE p."Code" IN ({InList(codes)})
              AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");
            """,
            process, reason);

        // Revokes codes from every role holding any of them.
        public static string Revoke(string[] codes, string process, string reason) => Loop(
            $"""
            SELECT DISTINCT rp."RoleId" AS "Id" FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
            WHERE p."Code" IN ({InList(codes)})
            """,
            $"""
            DELETE FROM "RolePermissions"
            WHERE "RoleId" = r."Id" AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" IN ({InList(codes)}));
            """,
            process, reason);
    }
}
