using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GlobalMaterialMasterCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaterialMasterEntries_SectionId_Code",
                table: "MaterialMasterEntries");

            // Reagent codes become unique across the app (Master Testing Materials, phase 1).
            // Reconcile cross-section duplicates first: within each group of equal codes the
            // lowest Id keeps the code. Another row with the same cleaned-up name (trimmed,
            // inner whitespace collapsed, case ignored) is a duplicate of the keeper: it is
            // deactivated and its code gets the owner section code appended. A row with a
            // different name stays active and only gets the suffix. If the suffixed code is
            // still taken, the row Id is appended. Linked stock lots and other records keep
            // their MaterialMasterEntryId; lots keep their own Code snapshot.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT e."Id", e."Code", e."IsActive", s."Code" AS "SectionCode",
                        min(e."Id") OVER (PARTITION BY e."Code") AS "KeeperId",
                        count(*) OVER (PARTITION BY e."Code") AS "GroupSize",
                        lower(regexp_replace(btrim(e."Name"), '\s+', ' ', 'g')) AS "NormName"
                    FROM "MaterialMasterEntries" e
                    JOIN "DocumentSections" s ON s."Id" = e."SectionId"
                ),
                plan AS (
                    SELECT r."Id", r."Code" AS "OldCode", r."IsActive" AS "WasActive",
                        (r."NormName" = k."NormName") AS "SameName",
                        CASE WHEN EXISTS (SELECT 1 FROM "MaterialMasterEntries" x WHERE x."Code" = left(r."Code" || '-' || r."SectionCode", 50))
                            THEN left(r."Code", 50 - length('-' || r."SectionCode" || '-' || r."Id"::text)) || '-' || r."SectionCode" || '-' || r."Id"::text
                            ELSE left(r."Code" || '-' || r."SectionCode", 50)
                        END AS "NewCode"
                    FROM ranked r
                    JOIN ranked k ON k."Id" = r."KeeperId"
                    WHERE r."GroupSize" > 1 AND r."Id" <> r."KeeperId"
                ),
                changed AS (
                    UPDATE "MaterialMasterEntries" e
                    SET "Code" = p."NewCode",
                        "IsActive" = CASE WHEN p."SameName" THEN false ELSE e."IsActive" END
                    FROM plan p
                    WHERE e."Id" = p."Id"
                    RETURNING e."Id", p."OldCode", p."NewCode", p."WasActive", p."SameName"
                )
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                SELECT 'MaterialMasterEntry', c."Id"::text, 'Update',
                    json_build_object('Code', c."OldCode", 'IsActive', c."WasActive")::text,
                    json_build_object('Code', c."NewCode", 'IsActive', CASE WHEN c."SameName" THEN false ELSE c."WasActive" END)::text,
                    0, now(),
                    2, 'Migration 20261028111111_GlobalMaterialMasterCodes', 'MaterialMasterCodeReconciled', 9,
                    CASE WHEN c."SameName"
                        THEN 'Duplicate of the same-named entry with this code in another lab: deactivated and code suffixed with the owner lab (Master Testing Materials phase 1)'
                        ELSE 'Same code as a differently named entry in another lab: code suffixed with the owner lab (Master Testing Materials phase 1)'
                    END
                FROM changed c;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialMasterEntries_Code",
                table: "MaterialMasterEntries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialMasterEntries_SectionId",
                table: "MaterialMasterEntries",
                column: "SectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Index change only: the code reconciliation done in Up() is data and is not reversed
            // (the suffixed codes and deactivations stay, and are audited).
            migrationBuilder.DropIndex(
                name: "IX_MaterialMasterEntries_Code",
                table: "MaterialMasterEntries");

            migrationBuilder.DropIndex(
                name: "IX_MaterialMasterEntries_SectionId",
                table: "MaterialMasterEntries");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialMasterEntries_SectionId_Code",
                table: "MaterialMasterEntries",
                columns: new[] { "SectionId", "Code" },
                unique: true);
        }
    }
}
