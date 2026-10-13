using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowProfileVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowProfileVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    WorkflowType = table.Column<int>(type: "integer", nullable: false),
                    TestCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WorkUnits = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    TargetAnalystDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReasonForChange = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ElectronicSignatureId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowProfileVersions", x => x.Id);
                    table.CheckConstraint("CK_WorkflowProfileVersions_AnyValue", "\"WorkUnits\" IS NOT NULL OR \"TargetAnalystDays\" IS NOT NULL");
                    table.CheckConstraint("CK_WorkflowProfileVersions_TargetPositive", "\"TargetAnalystDays\" IS NULL OR \"TargetAnalystDays\" > 0");
                    table.CheckConstraint("CK_WorkflowProfileVersions_UnitsPositive", "\"WorkUnits\" IS NULL OR \"WorkUnits\" > 0");
                    table.ForeignKey(
                        name: "FK_WorkflowProfileVersions_ElectronicSignatures_ElectronicSign~",
                        column: x => x.ElectronicSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowProfileVersions_ElectronicSignatureId",
                table: "WorkflowProfileVersions",
                column: "ElectronicSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowProfileVersions_WorkflowType_TestCode_EffectiveFrom",
                table: "WorkflowProfileVersions",
                columns: new[] { "WorkflowType", "TestCode", "EffectiveFrom" });

            var migrationId = GetType().GetCustomAttributes(typeof(MigrationAttribute), false)
                .Cast<MigrationAttribute>().Single().Id;

            // Legacy import (spec 2.4): every WorkloadWeights row with a live test definition becomes a
            // Draft override. Old weights were relative (1.0 = a count test), not hours, so they have
            // no effect until a Section Head reviews and signs each one. Rows whose definition has a
            // retired workflow type (2, 3, 10, 11) are skipped: the service rejects those types, so
            // the Draft could never be signed. The legacy INVESTIGATION row has no definition.
            // WorkflowType ints: 0 CountTest, 1 Observation, 4 Measurement, 5 Gravimetric, 6 Qualitative,
            // 7 Dissolution, 8 Disintegration, 9 WeightVariation, 12 HplcMethodAssay, 13 Titration,
            // 14 IcpMethodAssay. Status 0 = Draft. ActorType 2 = system, ActionCategory 6 = Configuration.
            migrationBuilder.Sql($$"""
                WITH imported AS (
                    INSERT INTO "WorkflowProfileVersions"
                        ("WorkflowType", "TestCode", "WorkUnits", "TargetAnalystDays", "EffectiveFrom", "Status",
                         "ReasonForChange", "ElectronicSignatureId", "CreatedByUserId", "CreatedAt")
                    SELECT td."WorkflowType", w."TestCode", w."Weight", NULL, CURRENT_DATE, 0,
                           'Imported from legacy workload weight', NULL, 0, now()
                    FROM "WorkloadWeights" w
                    JOIN "TestDefinitions" td ON td."Code" = w."TestCode"
                    WHERE td."WorkflowType" IN (0, 1, 4, 5, 6, 7, 8, 9, 12, 13, 14)
                      AND w."Weight" > 0
                      AND NOT EXISTS (SELECT 1 FROM "WorkflowProfileVersions" v
                                      WHERE v."TestCode" = w."TestCode"
                                        AND v."ReasonForChange" = 'Imported from legacy workload weight')
                    RETURNING "Id", "TestCode", "WorkUnits"
                )
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                SELECT 'WorkflowProfileVersion', i."Id"::text, 'Create', '{}',
                       jsonb_build_object('TestCode', i."TestCode", 'WorkUnits', i."WorkUnits", 'Status', 'Draft')::text,
                       0, now(), 2, 'Migration {{migrationId}}', 'WorkflowProfileDraftImported', 6,
                       'Legacy workload weight imported as a Draft workflow profile (KPI remodel S1)'
                FROM imported i;
                """);

            // Append-only guard (same idea as trg_electronicsignatures_immutable): nothing is ever
            // deleted, and only a Draft may be updated - which is how signing turns it Effective.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_workflowprofileversion_modification() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'WorkflowProfileVersions is append-only: DELETE operations are prohibited';
                    END IF;
                    IF OLD."Status" = 1 THEN
                        RAISE EXCEPTION 'WorkflowProfileVersions is append-only: an Effective version cannot be modified';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_workflowprofileversions_immutable
                BEFORE UPDATE OR DELETE ON "WorkflowProfileVersions"
                FOR EACH ROW EXECUTE FUNCTION prevent_workflowprofileversion_modification();
                """);

            // DbSeeder seeds Kpi.ConfigureProfiles on a fresh database; these idempotent statements
            // cover existing databases. RoleType: 0 = SystemAdministrator, 1 = SectionHead.
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'Kpi.ConfigureProfiles', 'Create and sign workflow profile versions (work units and analyst-stage targets) used by the KPI page.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'Kpi.ConfigureProfiles');
                """);
            migrationBuilder.Sql("""
                UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'Kpi.ConfigureProfiles';
                """);
            migrationBuilder.Sql("""
                INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                SELECT r."Id", p."Id"
                FROM "Roles" r CROSS JOIN "Permissions" p
                WHERE r."Type" IN (0, 1)
                  AND p."Code" = 'Kpi.ConfigureProfiles'
                  AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp
                                  WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN
                    (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Kpi.ConfigureProfiles');
                """);
            migrationBuilder.Sql("""
                DELETE FROM "Permissions" WHERE "Code" = 'Kpi.ConfigureProfiles';
                """);
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_workflowprofileversions_immutable ON "WorkflowProfileVersions";
                """);
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS prevent_workflowprofileversion_modification();
                """);

            migrationBuilder.DropTable(
                name: "WorkflowProfileVersions");
        }
    }
}
