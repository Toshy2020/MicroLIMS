using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestrictSystemAdministratorToSystemPermissions : Migration
    {
        // Segregation of duties (21 CFR Part 11 11.10(d)(g), EU GMP Annex 11 s12): administrators
        // control the system, never GxP data. Mirrors PermissionConstants.SystemAdministratorGrants
        // and applies only to the built-in role (Type 0 = SystemAdministrator AND IsSystemRole);
        // custom roles are left alone. Raw SQL bypasses RoleService, so PermissionVersion is bumped
        // here to invalidate the administrators' already-issued access tokens.
        private const string Reason = "Segregation of duties: administrators control the system, not GxP data";

        // The codes the role loses (and Down gives back): the old "all codes" grant minus the keep-list.
        private const string RemovedCodes = """
            'Samples.Review', 'Samples.Approve', 'Samples.Receive', 'Samples.ReceiveOwnLab', 'Samples.Correct', 'Samples.AssignAnalyst',
            'TestWorkflow.Execute', 'TestWorkflow.BiochemicalDecision', 'TestWorkflow.Supervise',
            'Hplc.Operate', 'Solutions.Prepare', 'WorkingStandards.Qualify',
            'Media.Prepare', 'Media.Release', 'Cryovials.Manage', 'Cryovials.Approve', 'Oos.Manage',
            'Materials.Manage', 'Materials.DocumentControl', 'Equipment.Manage', 'Equipment.DocumentControl',
            'Items.DocumentUpload', 'Discussions.EditAny',
            'Documents.Register', 'Documents.DraftEdit', 'Documents.RevisionCreate', 'Documents.Review', 'Documents.Approve',
            'Documents.PeriodicReview', 'Documents.TrainingAssign',
            'MonitoringPlan.Manage', 'MonitoringPlan.Approve',
            'EquipmentPlan.Manage', 'EquipmentPlan.Approve', 'EquipmentWork.Record', 'CalendarEvent.Manage'
            """;

        // The codes the role keeps (everything else is revoked on Up).
        private const string KeptCodes = """
            'Users.Manage', 'Roles.Manage', 'Audit.View', 'Reporting.Admin',
            'System.ViewErrorLog', 'System.ViewSecurityAudit',
            'MasterData.Manage', 'Items.Manage', 'Documents.ConfigManage', 'Kpi.ConfigureProfiles',
            'Discussions.View', 'Discussions.Create', 'Messages.Use', 'Signatures.Manage',
            'Dashboards.LabOverview', 'Dashboards.Review', 'Kpi.View', 'Kpi.ViewTeam',
            'Samples.TrackAll', 'MonitoringPlan.View', 'Documents.TrainingViewMatrix', 'Sections.ViewAll'
            """;

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
            // DbSeeder seeds Sections.ViewAll on a fresh database; this idempotent insert covers existing ones.
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'Sections.ViewAll', 'View every laboratory section''s records read-only.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'Sections.ViewAll');
                """);
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'Sections.ViewAll';""");

            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT "Id" FROM "Roles" WHERE "Type" = 0 AND "IsSystemRole" LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                $$"""
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                    SELECT r."Id", p."Id" FROM "Permissions" p
                    WHERE p."Code" = 'Sections.ViewAll'
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                    DELETE FROM "RolePermissions"
                    WHERE "RoleId" = r."Id"
                      AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" NOT IN ({{KeptCodes}}));

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", "Migration 20261026164146_RestrictSystemAdministratorToSystemPermissions").Replace("{REASON}", Reason) +
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
                $$"""
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                    SELECT r."Id", p."Id" FROM "Permissions" p
                    WHERE p."Code" IN ({{RemovedCodes}})
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                    DELETE FROM "RolePermissions"
                    WHERE "RoleId" = r."Id"
                      AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Sections.ViewAll');

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", "Migration 20261026164146_RestrictSystemAdministratorToSystemPermissions (Down)").Replace("{REASON}", "Segregation-of-duties restriction rolled back (migration Down)") +
                "\nEND LOOP;\nEND $$;");

            // Any other role that was granted the code is cleared too, so the permission row can go.
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Sections.ViewAll');
                """);
            migrationBuilder.Sql("""DELETE FROM "Permissions" WHERE "Code" = 'Sections.ViewAll';""");
        }
    }
}
