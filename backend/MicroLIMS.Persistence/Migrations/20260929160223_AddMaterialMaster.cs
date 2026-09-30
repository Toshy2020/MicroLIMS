using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaterialMasterEntryId",
                table: "Materials",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MoisturePercent",
                table: "Materials",
                type: "numeric(6,3)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaterialMasterEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Grade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Source = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BaseUnit = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    WorkingConcentration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Solvent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TransitionRangeFrom = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    TransitionRangeTo = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    ColourChange = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IndicatorUse = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialMasterEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialMasterEntries_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_MaterialMasterEntryId",
                table: "Materials",
                column: "MaterialMasterEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialMasterEntries_SectionId_Code",
                table: "MaterialMasterEntries",
                columns: new[] { "SectionId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_MaterialMasterEntries_MaterialMasterEntryId",
                table: "Materials",
                column: "MaterialMasterEntryId",
                principalTable: "MaterialMasterEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_MaterialMasterEntries_MaterialMasterEntryId",
                table: "Materials");

            migrationBuilder.DropTable(
                name: "MaterialMasterEntries");

            migrationBuilder.DropIndex(
                name: "IX_Materials_MaterialMasterEntryId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "MaterialMasterEntryId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "MoisturePercent",
                table: "Materials");
        }
    }
}
