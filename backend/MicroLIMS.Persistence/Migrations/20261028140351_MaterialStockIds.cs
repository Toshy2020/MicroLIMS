using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MaterialStockIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StockId",
                table: "Materials",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Backfill (Master Testing Materials phase 5, Q8): one running sequence for the whole app, shared with the
            // application (StockIdGenerator). Only lots still in stock get a stock ID now - quantity left and not expired -
            // numbered in receiving-date order (then Id); expired and depleted lots stay NULL for good. The year in the ID
            // is the year of the lot's ReceivingDate. One audit row per lot, then one summary row with the count and range.
            migrationBuilder.Sql("""
            CREATE SEQUENCE IF NOT EXISTS stock_id_seq START WITH 1 INCREMENT BY 1 NO CYCLE;

            DO $backfill$
            DECLARE
                v_process constant text := 'Migration 20261028140351_MaterialStockIds';
                v_reason constant text := 'Master Testing Materials backfill';
                v_lot record;
                v_stock_id text;
                v_assigned int := 0;
                v_first text;
                v_last text;
                v_skipped int;
            BEGIN
                FOR v_lot IN
                    SELECT m."Id", extract(year FROM m."ReceivingDate")::int AS yr
                    FROM "Materials" m
                    WHERE m."StockId" IS NULL
                      AND m."QuantityRemaining" > 0
                      AND (m."ExpiryDate" IS NULL OR m."ExpiryDate"::date >= current_date)
                    ORDER BY m."ReceivingDate", m."Id"
                LOOP
                    v_stock_id := 'STK-' || lpad(v_lot.yr::text, 4, '0') || '-' || lpad(nextval('stock_id_seq')::text, 5, '0');
                    UPDATE "Materials" SET "StockId" = v_stock_id WHERE "Id" = v_lot."Id";

                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    VALUES ('Material', v_lot."Id"::text, 'Update',
                        json_build_object('StockId', NULL)::text,
                        json_build_object('StockId', v_stock_id)::text,
                        0, now(), 2, v_process, 'MaterialStockIdAssigned', 6, v_reason);

                    v_assigned := v_assigned + 1;
                    IF v_first IS NULL THEN v_first := v_stock_id; END IF;
                    v_last := v_stock_id;
                END LOOP;

                SELECT count(*) INTO v_skipped FROM "Materials" WHERE "StockId" IS NULL;

                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                VALUES ('MaterialStockIdBackfill', 'MasterTestingMaterials', 'Update', NULL,
                    jsonb_build_object(
                        'LotsAssigned', v_assigned,
                        'FirstStockId', v_first,
                        'LastStockId', v_last,
                        'LotsLeftWithoutStockId', v_skipped)::text,
                    0, now(), 2, v_process, 'MaterialStockIdBackfillSummary', 6, v_reason);

                RAISE NOTICE 'Stock ID backfill: % in-stock lots numbered (% to %), % expired or depleted lots left without one',
                    v_assigned, v_first, v_last, v_skipped;
            END
            $backfill$;
            """);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_StockId",
                table: "Materials",
                column: "StockId",
                unique: true,
                filter: "\"StockId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema only: the assigned IDs are data (audited under the migration's system process name); the
            // sequence is kept so a later re-apply continues the numbering instead of reusing printed IDs.
            migrationBuilder.DropIndex(
                name: "IX_Materials_StockId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "StockId",
                table: "Materials");
        }
    }
}
