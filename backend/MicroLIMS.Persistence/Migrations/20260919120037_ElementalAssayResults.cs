using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ElementalAssayResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Specifications",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LabelClaim",
                table: "Specifications",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabelClaimUnit",
                table: "Specifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultBasis",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SampleMatrix",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TestAnalyteId",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElementalAssayEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    SampleMatrix = table.Column<int>(type: "integer", nullable: false),
                    UnitAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    AnalysedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementalAssayEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementalAssayEntries_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElementalAssayEntries_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElementalAssayEntries_Users_EnteredByUserId",
                        column: x => x.EnteredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElementalAssayResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntryId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    SpecificationId = table.Column<int>(type: "integer", nullable: false),
                    CalibrationRunAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    ParameterName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Element = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportedPpm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    OverRange = table.Column<bool>(type: "boolean", nullable: false),
                    BelowLoq = table.Column<bool>(type: "boolean", nullable: false),
                    MgPerUnit = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ResultClaim = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    PercentLabelClaim = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ReportedValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ReportedDisplay = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResultBasis = table.Column<int>(type: "integer", nullable: false),
                    SpecLimit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ComparisonStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementalAssayResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementalAssayResults_CalibrationRunAnalytes_CalibrationRun~",
                        column: x => x.CalibrationRunAnalyteId,
                        principalTable: "CalibrationRunAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElementalAssayResults_ElementalAssayEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "ElementalAssayEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElementalAssayResults_Specifications_SpecificationId",
                        column: x => x.SpecificationId,
                        principalTable: "Specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElementalAssayResults_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_TestAnalyteId",
                table: "Specifications",
                column: "TestAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementalAssayEntries_EnteredAt",
                table: "ElementalAssayEntries",
                column: "EnteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ElementalAssayEntries_EnteredByUserId",
                table: "ElementalAssayEntries",
                column: "EnteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementalAssayEntries_SignatureId",
                table: "ElementalAssayEntries",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementalAssayEntries_TestOrderId_IsActive",
                table: "ElementalAssayEntries",
                columns: new[] { "TestOrderId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ElementalAssayResults_CalibrationRunAnalyteId",
                table: "ElementalAssayResults",
                column: "CalibrationRunAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementalAssayResults_EntryId",
                table: "ElementalAssayResults",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementalAssayResults_SpecificationId",
                table: "ElementalAssayResults",
                column: "SpecificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementalAssayResults_TestOrderId_IsActive",
                table: "ElementalAssayResults",
                columns: new[] { "TestOrderId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Specifications_TestAnalytes_TestAnalyteId",
                table: "Specifications",
                column: "TestAnalyteId",
                principalTable: "TestAnalytes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Specifications_TestAnalytes_TestAnalyteId",
                table: "Specifications");

            migrationBuilder.DropTable(
                name: "ElementalAssayResults");

            migrationBuilder.DropTable(
                name: "ElementalAssayEntries");

            migrationBuilder.DropIndex(
                name: "IX_Specifications_TestAnalyteId",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "LabelClaim",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "LabelClaimUnit",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ResultBasis",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "SampleMatrix",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "TestAnalyteId",
                table: "Specifications");
        }
    }
}
