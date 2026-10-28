using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StepMediaDiluentMediaProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiluentTypes_Materials_MaterialId",
                table: "DiluentTypes");

            migrationBuilder.DropIndex(
                name: "IX_TestWorkflowStepMedias_TestWorkflowStepId_MaterialId",
                table: "TestWorkflowStepMedias");

            // The stock lot on a step medium is kept for history only.
            migrationBuilder.AlterColumn<int>(
                name: "MaterialId",
                table: "TestWorkflowStepMedias",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "MediaProductId",
                table: "TestWorkflowStepMedias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaProductId",
                table: "DiluentTypes",
                type: "integer",
                nullable: true);

            // Backfill (Master Testing Materials phase 4, one audited run, no review step): step media take the Culture
            // Media product of their incubation condition, else of their stock lot; diluent types take the product of
            // their stock lot. Nothing is invented: rows that can't be resolved stay without a product and are listed in
            // the summary row. TestWorkflowStepMedias.MediaProductId (and DiluentTypes.MediaProductId) stay nullable in
            // the database for that reason; Test Master requires the product on every row it writes.
            migrationBuilder.Sql("""
            DO $backfill$
            DECLARE
                v_process constant text := 'Migration 20261028130815_StepMediaDiluentMediaProduct';
                v_reason constant text := 'Master Testing Materials backfill';
                v_sm_audited int := 0;
                v_sm_condition int := 0;
                v_sm_material int := 0;
                v_sm_disagree int := 0;
                v_sm_unresolved jsonb;
                v_dt_linked int := 0;
                v_dt_unresolved jsonb;
            BEGIN
                -- a. Step media. The condition's product wins over the lot's (the rule StepMediumMatcher used to apply).
                -- A second row of the same product on one step is left unresolved: the new unique index allows one.
                CREATE TEMP TABLE tmp_sm ON COMMIT DROP AS
                SELECT x.id, x.step_id, x.material_id, x.product_id, x.source, x.disagree,
                       CASE WHEN x.product_id IS NULL THEN 'NoProductOnConditionOrMaterial'
                            WHEN row_number() OVER (PARTITION BY x.step_id, x.product_id ORDER BY x.id) > 1 THEN 'DuplicateProductOnStep'
                       END AS unresolved_reason
                FROM (
                    SELECT sm."Id" AS id, sm."TestWorkflowStepId" AS step_id, sm."MaterialId" AS material_id,
                           COALESCE(ic."MediaProductId", mt."MediaProductId") AS product_id,
                           CASE WHEN ic."MediaProductId" IS NOT NULL THEN 'IncubationCondition' WHEN mt."MediaProductId" IS NOT NULL THEN 'Material' END AS source,
                           (ic."MediaProductId" IS NOT NULL AND mt."MediaProductId" IS NOT NULL AND ic."MediaProductId" <> mt."MediaProductId") AS disagree
                    FROM "TestWorkflowStepMedias" sm
                    LEFT JOIN "MediaIncubationConditions" ic ON ic."Id" = sm."MediaIncubationConditionId"
                    LEFT JOIN "Materials" mt ON mt."Id" = sm."MaterialId"
                    WHERE sm."MediaProductId" IS NULL
                ) x;

                WITH linked AS (
                    UPDATE "TestWorkflowStepMedias" sm SET "MediaProductId" = t.product_id
                    FROM tmp_sm t
                    WHERE sm."Id" = t.id AND t.unresolved_reason IS NULL
                    RETURNING sm."Id", t.product_id, t.source, t.material_id, t.disagree
                ),
                audited AS (
                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    SELECT 'TestWorkflowStepMedia', k."Id"::text, 'Update',
                        json_build_object('MediaProductId', NULL, 'MaterialId', k.material_id)::text,
                        json_build_object('MediaProductId', k.product_id, 'Source', k.source)::text,
                        0, now(), 2, v_process, 'StepMediaLinkedToMediaProduct', 6, v_reason
                    FROM linked k
                    RETURNING 1
                )
                SELECT count(*) INTO v_sm_audited FROM audited;

                SELECT count(*) FILTER (WHERE source = 'IncubationCondition' AND unresolved_reason IS NULL),
                       count(*) FILTER (WHERE source = 'Material' AND unresolved_reason IS NULL),
                       count(*) FILTER (WHERE disagree AND unresolved_reason IS NULL)
                INTO v_sm_condition, v_sm_material, v_sm_disagree FROM tmp_sm;

                SELECT COALESCE(jsonb_agg(jsonb_build_object('StepMediaId', id, 'StepId', step_id, 'MaterialId', material_id, 'Reason', unresolved_reason) ORDER BY id), '[]'::jsonb)
                INTO v_sm_unresolved FROM tmp_sm WHERE unresolved_reason IS NOT NULL;

                -- b. Diluent types: the product of the stock lot the type pointed at. A batch-tracked type that ends up
                -- without a product (no lot, or a lot with no product) is listed. MaterialId is dropped afterwards; the
                -- audit row keeps the old value.
                CREATE TEMP TABLE tmp_dt ON COMMIT DROP AS
                SELECT d."Id" AS id, d."MaterialId" AS material_id, mt."MediaProductId" AS product_id, d."RequiresBatchTracking" AS tracked
                FROM "DiluentTypes" d
                LEFT JOIN "Materials" mt ON mt."Id" = d."MaterialId";

                WITH linked AS (
                    UPDATE "DiluentTypes" d SET "MediaProductId" = t.product_id
                    FROM tmp_dt t
                    WHERE d."Id" = t.id AND t.product_id IS NOT NULL
                    RETURNING d."Id", t.product_id, t.material_id
                ),
                audited AS (
                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    SELECT 'DiluentType', k."Id"::text, 'Update',
                        json_build_object('MaterialId', k.material_id, 'MediaProductId', NULL)::text,
                        json_build_object('MaterialId', NULL, 'MediaProductId', k.product_id)::text,
                        0, now(), 2, v_process, 'DiluentTypeLinkedToMediaProduct', 6, v_reason
                    FROM linked k
                    RETURNING 1
                )
                SELECT count(*) INTO v_dt_linked FROM audited;

                SELECT COALESCE(jsonb_agg(jsonb_build_object('DiluentTypeId', id, 'MaterialId', material_id) ORDER BY id), '[]'::jsonb)
                INTO v_dt_unresolved FROM tmp_dt WHERE tracked AND product_id IS NULL;

                -- c. One summary row: the run's report.
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                VALUES ('StepMediaDiluentBackfill', 'MasterTestingMaterials', 'Update', NULL,
                    jsonb_build_object(
                        'StepMediaLinkedFromCondition', v_sm_condition,
                        'StepMediaLinkedFromMaterial', v_sm_material,
                        'StepMediaConditionOverrodeMaterial', v_sm_disagree,
                        'StepMediaUnresolvedCount', jsonb_array_length(v_sm_unresolved),
                        'StepMediaUnresolved', v_sm_unresolved,
                        'DiluentTypesLinked', v_dt_linked,
                        'DiluentTypesUnresolvedCount', jsonb_array_length(v_dt_unresolved),
                        'DiluentTypesUnresolved', v_dt_unresolved)::text,
                    0, now(), 2, v_process, 'StepMediaDiluentBackfillSummary', 6, v_reason);

                RAISE NOTICE 'Step media / diluent backfill: % step media linked (% by condition, % by lot), % unresolved; % diluent types linked, % unresolved',
                    v_sm_condition + v_sm_material, v_sm_condition, v_sm_material, jsonb_array_length(v_sm_unresolved),
                    v_dt_linked, jsonb_array_length(v_dt_unresolved);

                DROP TABLE tmp_sm;
                DROP TABLE tmp_dt;
            END
            $backfill$;
            """);

            migrationBuilder.DropIndex(
                name: "IX_DiluentTypes_MaterialId",
                table: "DiluentTypes");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "DiluentTypes");

            migrationBuilder.CreateIndex(
                name: "IX_DiluentTypes_MediaProductId",
                table: "DiluentTypes",
                column: "MediaProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepMedias_MediaProductId",
                table: "TestWorkflowStepMedias",
                column: "MediaProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepMedias_TestWorkflowStepId_MediaProductId",
                table: "TestWorkflowStepMedias",
                columns: new[] { "TestWorkflowStepId", "MediaProductId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DiluentTypes_MediaProducts_MediaProductId",
                table: "DiluentTypes",
                column: "MediaProductId",
                principalTable: "MediaProducts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestWorkflowStepMedias_MediaProducts_MediaProductId",
                table: "TestWorkflowStepMedias",
                column: "MediaProductId",
                principalTable: "MediaProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema only: the backfilled links are data and are not reversed (they are audited under the
            // migration's system process name). A diluent type's old stock lot is not restored; the audit row keeps it.
            // Step media written after this migration have no stock lot, so the old NOT NULL column can't be restored for them.
            migrationBuilder.DropForeignKey(
                name: "FK_DiluentTypes_MediaProducts_MediaProductId",
                table: "DiluentTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_TestWorkflowStepMedias_MediaProducts_MediaProductId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropIndex(
                name: "IX_DiluentTypes_MediaProductId",
                table: "DiluentTypes");

            migrationBuilder.DropIndex(
                name: "IX_TestWorkflowStepMedias_MediaProductId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropIndex(
                name: "IX_TestWorkflowStepMedias_TestWorkflowStepId_MediaProductId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropColumn(
                name: "MediaProductId",
                table: "DiluentTypes");

            migrationBuilder.DropColumn(
                name: "MediaProductId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "DiluentTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MaterialId",
                table: "TestWorkflowStepMedias",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiluentTypes_MaterialId",
                table: "DiluentTypes",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepMedias_TestWorkflowStepId_MaterialId",
                table: "TestWorkflowStepMedias",
                columns: new[] { "TestWorkflowStepId", "MaterialId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DiluentTypes_Materials_MaterialId",
                table: "DiluentTypes",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id");
        }
    }
}
