using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDocumentControlPermissions : Migration
    {
        // The five Document Control codes are now checked server-side (DocumentAuthorizationService and the
        // review / approval / periodic-review services), on top of the existing assignment rules. A role that
        // already has people working in Document Control must not lose that ability overnight, so Up grants
        // each code to every role whose users hold the matching in-flight work:
        //   Documents.DraftEdit       owner of an active document, active Author assignee, or creator of a non-final revision
        //   Documents.RevisionCreate  owner of an active document, or active Author assignee
        //   Documents.Review          reviewer of a Pending/InProgress review task, or active TechnicalReviewer assignee
        //                             on a document with a non-final revision
        //   Documents.Approve         approver of a Pending approval task, or active Approver assignee on a document
        //                             with a non-final revision
        //   Documents.PeriodicReview  assignee of a Pending/InProgress periodic review task
        // Review and Approve are GxP codes under SegregationOfDutiesRules: a role holding Users.Manage or
        // Roles.Manage is never granted them. That role is skipped and an audit row records it.
        // One System-actor audit row per role changed, PermissionVersion bumped (raw SQL bypasses RoleService,
        // so already-issued tokens are invalidated here). Down revokes exactly the codes the Up audit rows list
        // under "Granted" (roles that already held a code are not in that list, so keep theirs).
        private const string Reason = "Document Control permissions are now enforced: granted to roles with in-flight Document Control work";
        private const string SkippedReason = "Not granted: the role holds Users.Manage or Roles.Manage and Documents.Review/Approve are GxP codes (segregation of duties)";
        private const string UpProcess = "Migration 20261027082626_EnforceDocumentControlPermissions";

        private const string Declarations = """
            DECLARE
                r RECORD;
                prev_codes text[];
                new_codes text[];
                granted text[];
                revoked text[];
                wanted text[];
                skipped text[];
            """;

        private const string SnapshotPrevious = """
                    SELECT COALESCE(array_agg(p."Code" ORDER BY p."Code"), '{}') INTO prev_codes
                    FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                    WHERE rp."RoleId" = r."Id";
            """;

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

        // Codes that mean "this role has in-flight work needing the code", one row per code the role qualifies for.
        private const string WantedCodes = """
                    wanted := ARRAY(
                        SELECT c FROM (
                            SELECT 'Documents.DraftEdit' AS c WHERE EXISTS (
                                SELECT 1 FROM "Users" u WHERE u."RoleId" = r."Id" AND u."IsActive" AND (
                                    EXISTS (SELECT 1 FROM "DocumentMasters" m WHERE m."DocumentOwnerUserId" = u."Id" AND m."RecordStatus" = 1)
                                    OR EXISTS (SELECT 1 FROM "DocumentMasterAssignments" a JOIN "DocumentMasters" m ON m."Id" = a."DocumentMasterId"
                                               WHERE a."UserId" = u."Id" AND a."AssignmentRole" = 2 AND a."IsActive" AND m."RecordStatus" = 1
                                                 AND EXISTS (SELECT 1 FROM "DocumentRevisions" v WHERE v."DocumentMasterId" = m."Id" AND v."RevisionStatus" IN (1, 3, 4, 5)))
                                    OR EXISTS (SELECT 1 FROM "DocumentRevisions" v WHERE v."CreatedByUserId" = u."Id" AND v."RevisionStatus" IN (1, 3, 4, 5))))
                            UNION ALL
                            SELECT 'Documents.RevisionCreate' WHERE EXISTS (
                                SELECT 1 FROM "Users" u WHERE u."RoleId" = r."Id" AND u."IsActive" AND (
                                    EXISTS (SELECT 1 FROM "DocumentMasters" m WHERE m."DocumentOwnerUserId" = u."Id" AND m."RecordStatus" = 1)
                                    OR EXISTS (SELECT 1 FROM "DocumentMasterAssignments" a JOIN "DocumentMasters" m ON m."Id" = a."DocumentMasterId"
                                               WHERE a."UserId" = u."Id" AND a."AssignmentRole" = 2 AND a."IsActive" AND m."RecordStatus" = 1)))
                            UNION ALL
                            SELECT 'Documents.Review' WHERE EXISTS (
                                SELECT 1 FROM "Users" u WHERE u."RoleId" = r."Id" AND u."IsActive" AND (
                                    EXISTS (SELECT 1 FROM "DocumentReviewTasks" t WHERE t."AssignedReviewerUserId" = u."Id" AND t."Status" IN (1, 2))
                                    OR EXISTS (SELECT 1 FROM "DocumentMasterAssignments" a JOIN "DocumentMasters" m ON m."Id" = a."DocumentMasterId"
                                               WHERE a."UserId" = u."Id" AND a."AssignmentRole" = 3 AND a."IsActive" AND m."RecordStatus" = 1
                                                 AND EXISTS (SELECT 1 FROM "DocumentRevisions" v WHERE v."DocumentMasterId" = m."Id" AND v."RevisionStatus" IN (1, 3, 4, 5)))))
                            UNION ALL
                            SELECT 'Documents.Approve' WHERE EXISTS (
                                SELECT 1 FROM "Users" u WHERE u."RoleId" = r."Id" AND u."IsActive" AND (
                                    EXISTS (SELECT 1 FROM "DocumentApprovalTasks" t WHERE t."AssignedApproverUserId" = u."Id" AND t."Status" = 1)
                                    OR EXISTS (SELECT 1 FROM "DocumentMasterAssignments" a JOIN "DocumentMasters" m ON m."Id" = a."DocumentMasterId"
                                               WHERE a."UserId" = u."Id" AND a."AssignmentRole" = 4 AND a."IsActive" AND m."RecordStatus" = 1
                                                 AND EXISTS (SELECT 1 FROM "DocumentRevisions" v WHERE v."DocumentMasterId" = m."Id" AND v."RevisionStatus" IN (1, 3, 4, 5)))))
                            UNION ALL
                            SELECT 'Documents.PeriodicReview' WHERE EXISTS (
                                SELECT 1 FROM "Users" u JOIN "PeriodicReviewTasks" t ON t."AssignedReviewerUserId" = u."Id"
                                WHERE u."RoleId" = r."Id" AND u."IsActive" AND t."Status" IN (1, 2))
                        ) x);
                    -- Segregation of duties: never hand a GxP code to a role that administers the system.
                    skipped := '{}';
                    IF prev_codes && ARRAY['Users.Manage', 'Roles.Manage'] THEN
                        skipped := ARRAY(SELECT unnest(wanted) INTERSECT SELECT unnest(ARRAY['Documents.Review', 'Documents.Approve']));
                        wanted := ARRAY(SELECT unnest(wanted) EXCEPT SELECT unnest(skipped));
                    END IF;
            """;

        private const string Codes = "'Documents.DraftEdit', 'Documents.RevisionCreate', 'Documents.Review', 'Documents.Approve', 'Documents.PeriodicReview'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" IN ({Codes});""");

            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT "Id" FROM "Roles" ORDER BY "Id" LOOP
                """ + "\n" + SnapshotPrevious + "\n" + WantedCodes + "\n" +
                """
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                    SELECT r."Id", p."Id" FROM "Permissions" p
                    WHERE p."Code" = ANY (wanted)
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                    IF cardinality(skipped) > 0 THEN
                        INSERT INTO "AuditLogs" (
                            "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                            "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                        VALUES (
                            'Role', r."Id"::text, 'ROLE_PERMISSION_GRANT_SKIPPED',
                            jsonb_build_object('PermissionCodes', to_jsonb(prev_codes))::text,
                            jsonb_build_object('Skipped', to_jsonb(skipped))::text,
                            0, now(), 2, '{PROCESS}', 'RolePermissionGrantSkipped', 7, '{SKIPPED}');
                    END IF;

                """.Replace("{PROCESS}", UpProcess).Replace("{SKIPPED}", SkippedReason) + "\n" +
                AuditAndBump.Replace("{PROCESS}", UpProcess).Replace("{REASON}", Reason) +
                "\nEND LOOP;\nEND $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""UPDATE "Permissions" SET "IsEnforced" = FALSE WHERE "Code" IN ({Codes});""");

            // Revoke exactly what Up granted, read back from its audit rows.
            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT DISTINCT a."EntityId"::int AS "Id" FROM "AuditLogs" a
                         WHERE a."EntityName" = 'Role' AND a."Action" = 'ROLE_PERMISSIONS_UPDATED'
                           AND a."SystemProcessName" = '{UPPROCESS}'
                           AND EXISTS (SELECT 1 FROM "Roles" ro WHERE ro."Id" = a."EntityId"::int) LOOP
                """.Replace("{UPPROCESS}", UpProcess) + "\n" + SnapshotPrevious + "\n" +
                """
                    DELETE FROM "RolePermissions"
                    WHERE "RoleId" = r."Id"
                      AND "PermissionId" IN (
                          SELECT p."Id" FROM "Permissions" p
                          WHERE p."Code" IN (
                              SELECT jsonb_array_elements_text(a."NewValue"::jsonb -> 'Granted')
                              FROM "AuditLogs" a
                              WHERE a."EntityName" = 'Role' AND a."EntityId" = r."Id"::text
                                AND a."Action" = 'ROLE_PERMISSIONS_UPDATED' AND a."SystemProcessName" = '{UPPROCESS}'));

                """.Replace("{UPPROCESS}", UpProcess) + "\n" +
                AuditAndBump.Replace("{PROCESS}", UpProcess + " (Down)").Replace("{REASON}", "Document Control permission grants rolled back (migration Down)") +
                "\nEND LOOP;\nEND $$;");
        }
    }
}
