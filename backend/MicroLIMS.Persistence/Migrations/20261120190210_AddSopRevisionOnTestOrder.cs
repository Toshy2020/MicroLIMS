using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSopRevisionOnTestOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SopCaptureStatus",
                table: "TestOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SopCapturedAtUtc",
                table: "TestOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SopDocumentCode",
                table: "TestOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SopDocumentMasterId",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SopDocumentTitle",
                table: "TestOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SopEffectiveDateUtc",
                table: "TestOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SopRevisionId",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SopRevisionNumber",
                table: "TestOrders",
                type: "text",
                nullable: true);

            // The SOP snapshot is written once at the first recorded step. Once
            // captured, no captured column may change; other columns stay mutable.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION test_orders_sop_capture_immutable_fn()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF OLD."SopCaptureStatus" IS NOT NULL AND (
                        NEW."SopCaptureStatus" IS DISTINCT FROM OLD."SopCaptureStatus"
                        OR NEW."SopDocumentMasterId" IS DISTINCT FROM OLD."SopDocumentMasterId"
                        OR NEW."SopDocumentCode" IS DISTINCT FROM OLD."SopDocumentCode"
                        OR NEW."SopDocumentTitle" IS DISTINCT FROM OLD."SopDocumentTitle"
                        OR NEW."SopRevisionId" IS DISTINCT FROM OLD."SopRevisionId"
                        OR NEW."SopRevisionNumber" IS DISTINCT FROM OLD."SopRevisionNumber"
                        OR NEW."SopEffectiveDateUtc" IS DISTINCT FROM OLD."SopEffectiveDateUtc"
                        OR NEW."SopCapturedAtUtc" IS DISTINCT FROM OLD."SopCapturedAtUtc"
                    ) THEN
                        RAISE EXCEPTION 'Test order % SOP capture is immutable (21 CFR Part 11, EU Annex 11).', OLD."Id";
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER test_orders_sop_capture_immutable
                BEFORE UPDATE ON "TestOrders"
                FOR EACH ROW EXECUTE FUNCTION test_orders_sop_capture_immutable_fn();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS test_orders_sop_capture_immutable ON "TestOrders";
                DROP FUNCTION IF EXISTS test_orders_sop_capture_immutable_fn();
                """);

            migrationBuilder.DropColumn(
                name: "SopCaptureStatus",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SopCapturedAtUtc",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SopDocumentCode",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SopDocumentMasterId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SopDocumentTitle",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SopEffectiveDateUtc",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SopRevisionId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SopRevisionNumber",
                table: "TestOrders");
        }
    }
}
