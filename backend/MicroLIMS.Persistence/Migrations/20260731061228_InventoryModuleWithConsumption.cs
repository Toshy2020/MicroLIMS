using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryModuleWithConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiscsRemaining",
                table: "ReferenceStrains",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EquipmentInventories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ManufacturerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CalibrationDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentInventories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialType = table.Column<int>(type: "integer", nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ManufacturerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    QuantityRemaining = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    MinimumStockLevel = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentInventories_Code",
                table: "EquipmentInventories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Code",
                table: "Materials",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_MaterialType",
                table: "Materials",
                column: "MaterialType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentInventories");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropColumn(
                name: "DiscsRemaining",
                table: "ReferenceStrains");
        }
    }
}
