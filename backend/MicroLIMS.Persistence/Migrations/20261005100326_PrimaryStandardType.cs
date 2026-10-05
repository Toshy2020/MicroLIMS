using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrimaryStandardType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only: both enums are stored as int. 3 = MaterialMasterCategory.PrimaryStandard,
            // 13 = MaterialType.PrimaryStandard, 0 = Reagent, 6 = Chemical, SolutionMasters.Type 2 = Titrant.
            migrationBuilder.Sql("""
                WITH changed AS (
                    UPDATE "MaterialMasterEntries" SET "Category" = 3
                    WHERE "Category" = 0 AND EXISTS (SELECT 1 FROM "SolutionMasters" sm WHERE sm."Type" = 2 AND sm."StandardEntryId" = "MaterialMasterEntries"."Id")
                    RETURNING "Id"
                )
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                SELECT 'MaterialMasterEntry', c."Id"::text, 'Update', '{"Category":0}', '{"Category":3}', 0, now(),
                    2, 'Migration 20261005100326_PrimaryStandardType', 'MaterialReclassified', 9,
                    'Titrant primary standard reclassified to Primary Standard type (spec 2026-10-05 S1)'
                FROM changed c;
                """);

            migrationBuilder.Sql("""
                WITH changed AS (
                    UPDATE "Materials" SET "MaterialType" = 13
                    WHERE "MaterialType" = 6 AND "MaterialMasterEntryId" IN (SELECT "Id" FROM "MaterialMasterEntries" WHERE "Category" = 3)
                    RETURNING "Id"
                )
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                SELECT 'Material', c."Id"::text, 'Update', '{"MaterialType":6}', '{"MaterialType":13}', 0, now(),
                    2, 'Migration 20261005100326_PrimaryStandardType', 'MaterialReclassified', 9,
                    'Titrant primary standard lot reclassified to Primary Standard type (spec 2026-10-05 S1)'
                FROM changed c;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH changed AS (
                    UPDATE "Materials" SET "MaterialType" = 6, "Purity" = NULL
                    WHERE "MaterialType" = 13
                    RETURNING "Id"
                )
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                SELECT 'Material', c."Id"::text, 'Update', '{"MaterialType":13}', '{"MaterialType":6}', 0, now(),
                    2, 'Migration 20261005100326_PrimaryStandardType', 'MaterialReclassified', 9,
                    'Primary Standard type rolled back to Chemical; purity cleared (migration Down)'
                FROM changed c;
                """);

            migrationBuilder.Sql("""
                WITH changed AS (
                    UPDATE "MaterialMasterEntries" SET "Category" = 0
                    WHERE "Category" = 3
                    RETURNING "Id"
                )
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                SELECT 'MaterialMasterEntry', c."Id"::text, 'Update', '{"Category":3}', '{"Category":0}', 0, now(),
                    2, 'Migration 20261005100326_PrimaryStandardType', 'MaterialReclassified', 9,
                    'Primary Standard category rolled back to Reagent (migration Down)'
                FROM changed c;
                """);
        }
    }
}
