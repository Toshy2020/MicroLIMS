using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFpHplcMasters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Purity",
                table: "Materials",
                type: "numeric(6,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CdsSoftware",
                table: "Equipment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionSettings",
                table: "Equipment",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "Equipment",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""Equipment""
                SET ""SectionId"" = (SELECT ""Id"" FROM ""DocumentSections"" WHERE ""Code"" = 'MICRO' LIMIT 1)
                WHERE ""SectionId"" IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "Equipment",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vendor",
                table: "Equipment",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChromatographyColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChromatographyColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChromatographyColumns_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChromatographyColumnEquipment",
                columns: table => new
                {
                    CompatibleColumnsId = table.Column<int>(type: "integer", nullable: false),
                    CompatibleEquipmentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChromatographyColumnEquipment", x => new { x.CompatibleColumnsId, x.CompatibleEquipmentId });
                    table.ForeignKey(
                        name: "FK_ChromatographyColumnEquipment_ChromatographyColumns_Compati~",
                        column: x => x.CompatibleColumnsId,
                        principalTable: "ChromatographyColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChromatographyColumnEquipment_Equipment_CompatibleEquipment~",
                        column: x => x.CompatibleEquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Code",
                table: "Equipment",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_SectionId",
                table: "Equipment",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChromatographyColumnEquipment_CompatibleEquipmentId",
                table: "ChromatographyColumnEquipment",
                column: "CompatibleEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChromatographyColumns_Code",
                table: "ChromatographyColumns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChromatographyColumns_SectionId",
                table: "ChromatographyColumns",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_DocumentSections_SectionId",
                table: "Equipment",
                column: "SectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_DocumentSections_SectionId",
                table: "Equipment");

            migrationBuilder.DropTable(
                name: "ChromatographyColumnEquipment");

            migrationBuilder.DropTable(
                name: "ChromatographyColumns");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_Code",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_SectionId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Purity",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "CdsSoftware",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "ConnectionSettings",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Vendor",
                table: "Equipment");
        }
    }
}
