using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIcpMethodMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IcpMethodId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IcpMethodElementId",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IcpMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    StandardLevelsMgPerL = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CalibrationStandardEntryId = table.Column<int>(type: "integer", nullable: false),
                    MinCorrelation = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    RequireBlank = table.Column<bool>(type: "boolean", nullable: false),
                    BlankMaxMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RequireIcv = table.Column<bool>(type: "boolean", nullable: false),
                    IcvStandardEntryId = table.Column<int>(type: "integer", nullable: true),
                    IcvNominalMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    IcvRecoveryLowPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    IcvRecoveryHighPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RequireCcv = table.Column<bool>(type: "boolean", nullable: false),
                    CcvNominalMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CcvRecoveryLowPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CcvRecoveryHighPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    MaxCalibrationAgeHours = table.Column<int>(type: "integer", nullable: false),
                    SampleVolumeMl = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DilutionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpMethods_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcpMethods_MaterialMasterEntries_CalibrationStandardEntryId",
                        column: x => x.CalibrationStandardEntryId,
                        principalTable: "MaterialMasterEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcpMethods_MaterialMasterEntries_IcvStandardEntryId",
                        column: x => x.IcvStandardEntryId,
                        principalTable: "MaterialMasterEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IcpMethodElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpMethodId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    WavelengthNm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    View = table.Column<int>(type: "integer", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpMethodElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpMethodElements_IcpMethods_IcpMethodId",
                        column: x => x.IcpMethodId,
                        principalTable: "IcpMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_IcpMethodId",
                table: "TestDefinitions",
                column: "IcpMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_IcpMethodElementId",
                table: "Specifications",
                column: "IcpMethodElementId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpMethodElements_IcpMethodId_DisplayOrder",
                table: "IcpMethodElements",
                columns: new[] { "IcpMethodId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_IcpMethods_CalibrationStandardEntryId",
                table: "IcpMethods",
                column: "CalibrationStandardEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpMethods_IcvStandardEntryId",
                table: "IcpMethods",
                column: "IcvStandardEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpMethods_SectionId_Abbreviation",
                table: "IcpMethods",
                columns: new[] { "SectionId", "Abbreviation" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Specifications_IcpMethodElements_IcpMethodElementId",
                table: "Specifications",
                column: "IcpMethodElementId",
                principalTable: "IcpMethodElements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_IcpMethods_IcpMethodId",
                table: "TestDefinitions",
                column: "IcpMethodId",
                principalTable: "IcpMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Specifications_IcpMethodElements_IcpMethodElementId",
                table: "Specifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_IcpMethods_IcpMethodId",
                table: "TestDefinitions");

            migrationBuilder.DropTable(
                name: "IcpMethodElements");

            migrationBuilder.DropTable(
                name: "IcpMethods");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_IcpMethodId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Specifications_IcpMethodElementId",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "IcpMethodId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "IcpMethodElementId",
                table: "Specifications");
        }
    }
}
