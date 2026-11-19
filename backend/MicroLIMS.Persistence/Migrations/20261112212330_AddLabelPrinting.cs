using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelPrinting : Migration
    {
        private const string Process = "Migration 20261112212330_AddLabelPrinting";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EquipmentInventoryId",
                table: "Equipment",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Cryovials",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "LabelFormats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    WidthMm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    HeightMm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Layout = table.Column<int>(type: "integer", nullable: false),
                    DefaultCopies = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelFormats", x => x.Id);
                    table.CheckConstraint("CK_LabelFormats_DefaultCopies", "\"DefaultCopies\" BETWEEN 1 AND 50");
                    table.CheckConstraint("CK_LabelFormats_HeightMm", "\"HeightMm\" BETWEEN 10 AND 150");
                    table.CheckConstraint("CK_LabelFormats_WidthMm", "\"WidthMm\" BETWEEN 10 AND 150");
                });

            migrationBuilder.CreateTable(
                name: "LabelPrintLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    PrintNumber = table.Column<int>(type: "integer", nullable: false),
                    IsReprint = table.Column<bool>(type: "boolean", nullable: false),
                    Copies = table.Column<int>(type: "integer", nullable: false),
                    LabelFormatId = table.Column<int>(type: "integer", nullable: false),
                    LabelFormatVersion = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrintedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PrintedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelPrintLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabelPrintLogs_LabelFormats_LabelFormatId",
                        column: x => x.LabelFormatId,
                        principalTable: "LabelFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabelPrintLogs_Users_PrintedByUserId",
                        column: x => x.PrintedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_EquipmentInventoryId",
                table: "Equipment",
                column: "EquipmentInventoryId",
                unique: true,
                filter: "\"EquipmentInventoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabelFormats_EntityType",
                table: "LabelFormats",
                column: "EntityType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabelPrintLogs_BatchId",
                table: "LabelPrintLogs",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LabelPrintLogs_EntityType_EntityId_PrintNumber",
                table: "LabelPrintLogs",
                columns: new[] { "EntityType", "EntityId", "PrintNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabelPrintLogs_LabelFormatId",
                table: "LabelPrintLogs",
                column: "LabelFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_LabelPrintLogs_PrintedByUserId",
                table: "LabelPrintLogs",
                column: "PrintedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_EquipmentInventories_EquipmentInventoryId",
                table: "Equipment",
                column: "EquipmentInventoryId",
                principalTable: "EquipmentInventories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Immutability (21 CFR Part 11 / Annex 11). LabelPrintLogs is strictly append-only.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_label_print_log_modification()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'Table % is append-only: % is prohibited by GMP regulations (21 CFR Part 11, EU Annex 11).', TG_TABLE_NAME, TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_labelprintlogs_immutable
                BEFORE UPDATE OR DELETE ON "LabelPrintLogs"
                FOR EACH ROW EXECUTE FUNCTION prevent_label_print_log_modification();

                CREATE TRIGGER trg_labelprintlogs_no_truncate
                BEFORE TRUNCATE ON "LabelPrintLogs"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);

            migrationBuilder.Sql("""
                INSERT INTO "LabelFormats" ("EntityType", "WidthMm", "HeightMm", "Layout", "DefaultCopies")
                SELECT v.entity_type, v.width_mm, v.height_mm, v.layout, v.default_copies
                FROM (VALUES
                    (1, 50.00, 25.00, 1, 1),
                    (2, 40.00, 20.00, 1, 1),
                    (3, 50.00, 25.00, 1, 1),
                    (4, 25.00, 10.00, 2, 1),
                    (5, 50.00, 25.00, 1, 1),
                    (6, 50.00, 25.00, 1, 1)
                ) AS v(entity_type, width_mm, height_mm, layout, default_copies)
                WHERE NOT EXISTS (SELECT 1 FROM "LabelFormats" f WHERE f."EntityType" = v.entity_type);
                """);

            // Not marked enforced yet: endpoints/services checking these codes arrive in Tasks 6-7.
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT v.code, v.descr, FALSE
                FROM (VALUES
                    ('Labels.Reprint', 'Reprint barcode labels with a documented reason.'),
                    ('Labels.Configure', 'Configure barcode label sizes, layouts and default copy counts.')
                ) AS v(code, descr)
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" p WHERE p."Code" = v.code);
                """);

            migrationBuilder.Sql(RoleGrantSql.Grant(
                @"ro.""Type"" = 1 AND ro.""IsSystemRole""",
                new[] { "Labels.Reprint", "Labels.Configure" },
                Process,
                "Section Heads reprint labels and configure label formats"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(
                new[] { "Labels.Reprint", "Labels.Configure" },
                Process + " (Down)",
                "Label printing permissions rolled back (migration Down)"));

            migrationBuilder.Sql("""
                DELETE FROM "Permissions" WHERE "Code" IN ('Labels.Reprint', 'Labels.Configure');
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_labelprintlogs_no_truncate ON "LabelPrintLogs";
                DROP TRIGGER IF EXISTS trg_labelprintlogs_immutable ON "LabelPrintLogs";
                DROP FUNCTION IF EXISTS prevent_label_print_log_modification();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_EquipmentInventories_EquipmentInventoryId",
                table: "Equipment");

            migrationBuilder.DropTable(
                name: "LabelPrintLogs");

            migrationBuilder.DropTable(
                name: "LabelFormats");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_EquipmentInventoryId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "EquipmentInventoryId",
                table: "Equipment");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Cryovials",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);
        }
    }
}
