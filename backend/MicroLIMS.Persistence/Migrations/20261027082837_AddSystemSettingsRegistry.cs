using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettingsRegistry : Migration
    {
        // System.ConfigureSettings: change operating settings in the admin Settings registry (never GxP data).
        // Granted only to the built-in role (Type 0 = SystemAdministrator AND IsSystemRole); custom roles are left
        // alone. Raw SQL bypasses RoleService, so PermissionVersion is bumped here to invalidate issued tokens,
        // exactly as in 20261027033248_GrantSystemAdministratorConfigureAllSections.
        private const string Reason = "Administrators change system operating settings in the admin Settings registry";

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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsGxpRelevant = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettingVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SettingId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ElectronicSignatureId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettingVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSettingVersions_ElectronicSignatures_ElectronicSignat~",
                        column: x => x.ElectronicSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSettingVersions_SystemSettings_SettingId",
                        column: x => x.SettingId,
                        principalTable: "SystemSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSettingVersions_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettingVersions_ChangedByUserId",
                table: "SystemSettingVersions",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettingVersions_ElectronicSignatureId",
                table: "SystemSettingVersions",
                column: "ElectronicSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettingVersions_SettingId_Version",
                table: "SystemSettingVersions",
                columns: new[] { "SettingId", "Version" },
                unique: true);

            // The settings history is append-only (21 CFR Part 11 / Annex 11): the shared trigger blocks UPDATE and
            // DELETE, and the statement-level guard blocks TRUNCATE (see HardenAppendOnlyAuditTables).
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_systemsettingversions_immutable
                BEFORE UPDATE OR DELETE ON "SystemSettingVersions"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_systemsettingversions_no_truncate
                BEFORE TRUNCATE ON "SystemSettingVersions"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);

            // One lab time zone: Lab:TimeZoneId (Africa/Cairo) drives display and day boundaries and timestamps are
            // stored in UTC. The seeded System.TimeZone row duplicated it with a conflicting value and nothing reads it.
            migrationBuilder.Sql("""DELETE FROM "ConfigurationSettings" WHERE "SettingKey" = 'System.TimeZone';""");

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'System.ConfigureSettings', 'Change system operating settings (security thresholds, retention, worker limits) in the admin Settings registry.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'System.ConfigureSettings');
                """);
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'System.ConfigureSettings';""");

            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT "Id" FROM "Roles" WHERE "Type" = 0 AND "IsSystemRole" LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                    SELECT r."Id", p."Id" FROM "Permissions" p
                    WHERE p."Code" = 'System.ConfigureSettings'
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", "Migration 20261027082837_AddSystemSettingsRegistry").Replace("{REASON}", Reason) +
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
                      AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'System.ConfigureSettings');

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", "Migration 20261027082837_AddSystemSettingsRegistry (Down)").Replace("{REASON}", "Settings registry grant rolled back (migration Down)") +
                "\nEND LOOP;\nEND $$;");

            // Any other role that was granted the code is cleared too, so the permission row can go.
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'System.ConfigureSettings');
                """);
            migrationBuilder.Sql("""DELETE FROM "Permissions" WHERE "Code" = 'System.ConfigureSettings';""");

            // The System.TimeZone row is not restored: it was a duplicate of Lab:TimeZoneId that nothing read.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_systemsettingversions_no_truncate ON "SystemSettingVersions";
                DROP TRIGGER IF EXISTS trg_systemsettingversions_immutable ON "SystemSettingVersions";
                """);

            migrationBuilder.DropTable(
                name: "SystemSettingVersions");

            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
