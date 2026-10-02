using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireLegacySystemSuitability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy SST retirement (spec 2026-10-02): delete Standard Comparison
            // orders and old SST rows. Abort if any Standard Comparison test other
            // than 'Water soluble' has orders - that data was not reviewed.
            migrationBuilder.Sql(@"
DO $$
DECLARE codes text;
BEGIN
  SELECT string_agg(DISTINCT o.""TestCode"", ', ') INTO codes
  FROM ""TestOrders"" o JOIN ""TestDefinitions"" d ON d.""Code"" = o.""TestCode""
  WHERE d.""WorkflowType"" = 11 AND d.""Code"" <> 'Water soluble';
  IF codes IS NOT NULL THEN
    RAISE EXCEPTION 'RetireLegacySystemSuitability: Standard Comparison tests still have orders: %', codes;
  END IF;
END $$;

CREATE TEMP TABLE sc_orders AS
  SELECT o.""Id"" FROM ""TestOrders"" o JOIN ""TestDefinitions"" d ON d.""Code"" = o.""TestCode"" WHERE d.""WorkflowType"" = 11;
CREATE TEMP TABLE sc_codes AS SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 11;

DELETE FROM ""ResultReadings"" WHERE ""ParameterResultId"" IN (SELECT ""Id"" FROM ""ParameterResults"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders));
DELETE FROM ""ParameterResults"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""TestAnalyses"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""ResultRecords"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""TestReturnEvents"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""WorkflowStepResults"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
UPDATE ""Incubations"" SET ""ParentIncubationId"" = NULL WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""Incubations"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""LocationPathogenObservations"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""HplcEvidences"" WHERE ""HplcRunSampleId"" IN (SELECT ""Id"" FROM ""HplcRunSamples"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders));
DELETE FROM ""HplcRunSamples"" WHERE ""TestOrderId"" IN (SELECT ""Id"" FROM sc_orders);
DELETE FROM ""TestOrders"" WHERE ""Id"" IN (SELECT ""Id"" FROM sc_orders);

DELETE FROM ""SystemSuitabilityRuns"";

DELETE FROM ""Specifications"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""SampleTests"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""SamplingConfigurations"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""MachinePartConfigurations"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""RoomTestConfigurations"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""WorkloadWeightHistories"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""WorkloadWeights"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM sc_codes);
DELETE FROM ""TestAnalytes"" WHERE ""TestDefinitionId"" IN (SELECT ""Id"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 11);
DELETE FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 11;

DROP TABLE sc_orders;
DROP TABLE sc_codes;
");

            migrationBuilder.DropForeignKey(
                name: "FK_TestOrders_SystemSuitabilityRuns_SystemSuitabilityRunId",
                table: "TestOrders");

            migrationBuilder.DropTable(
                name: "SystemSuitabilityStandardResponses");

            migrationBuilder.DropTable(
                name: "SystemSuitabilityRunAnalytes");

            migrationBuilder.DropTable(
                name: "SystemSuitabilityRuns");

            migrationBuilder.DropIndex(
                name: "IX_TestOrders_SystemSuitabilityRunId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SystemSuitabilityRunId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "HplcMaxPreparationRsdPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "ResponseMode",
                table: "TestDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data deleted in Up is not restored.
            migrationBuilder.AddColumn<int>(
                name: "SystemSuitabilityRunId",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HplcMaxPreparationRsdPercent",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseMode",
                table: "TestDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SystemSuitabilityRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChromatographyColumnId = table.Column<int>(type: "integer", nullable: true),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReferenceStandardMaterialId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ComputedRsdPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MoisturePercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Resolution = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    RsdPercent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    StandardDilution = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StandardMeanArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StandardPurityPercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    StandardWeighInDeviationPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    StandardWeighInOutOfWindow = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StandardWeightMg = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TailingFactor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TheoreticalPlates = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TheoreticalWeightMg = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    WeighInJustification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuitabilityRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRuns_ChromatographyColumns_ChromatographyC~",
                        column: x => x.ChromatographyColumnId,
                        principalTable: "ChromatographyColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRuns_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRuns_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRuns_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRuns_Materials_ReferenceStandardMaterialId",
                        column: x => x.ReferenceStandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRuns_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRuns_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuitabilityRunAnalytes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceStandardMaterialId = table.Column<int>(type: "integer", nullable: false),
                    SystemSuitabilityRunId = table.Column<int>(type: "integer", nullable: false),
                    TestAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    AnalyteName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BlankTitreMl = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    ComputedRsdPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MoisturePercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Resolution = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    RsdPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    StandardDilution = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    StandardMeanArea = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    StandardPurityPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    StandardWeighInDeviationPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    StandardWeighInOutOfWindow = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StandardWeightMg = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    TailingFactor = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TheoreticalPlates = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TheoreticalWeightMg = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    WavelengthNm = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    WeighInJustification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuitabilityRunAnalytes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRunAnalytes_Materials_ReferenceStandardMat~",
                        column: x => x.ReferenceStandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRunAnalytes_SystemSuitabilityRuns_SystemSu~",
                        column: x => x.SystemSuitabilityRunId,
                        principalTable: "SystemSuitabilityRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityRunAnalytes_TestAnalytes_TestAnalyteId",
                        column: x => x.TestAnalyteId,
                        principalTable: "TestAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuitabilityStandardResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SystemSuitabilityRunAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Response = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuitabilityStandardResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityStandardResponses_SystemSuitabilityRunAnal~",
                        column: x => x.SystemSuitabilityRunAnalyteId,
                        principalTable: "SystemSuitabilityRunAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestOrders_SystemSuitabilityRunId",
                table: "TestOrders",
                column: "SystemSuitabilityRunId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRunAnalytes_ReferenceStandardMaterialId",
                table: "SystemSuitabilityRunAnalytes",
                column: "ReferenceStandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRunAnalytes_SystemSuitabilityRunId",
                table: "SystemSuitabilityRunAnalytes",
                column: "SystemSuitabilityRunId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRunAnalytes_SystemSuitabilityRunId_TestAna~",
                table: "SystemSuitabilityRunAnalytes",
                columns: new[] { "SystemSuitabilityRunId", "TestAnalyteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRunAnalytes_TestAnalyteId",
                table: "SystemSuitabilityRunAnalytes",
                column: "TestAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_ChromatographyColumnId",
                table: "SystemSuitabilityRuns",
                column: "ChromatographyColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_Code",
                table: "SystemSuitabilityRuns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_EquipmentId",
                table: "SystemSuitabilityRuns",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_PerformedAt",
                table: "SystemSuitabilityRuns",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_PerformedByUserId",
                table: "SystemSuitabilityRuns",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_ReferenceStandardMaterialId",
                table: "SystemSuitabilityRuns",
                column: "ReferenceStandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_SectionId",
                table: "SystemSuitabilityRuns",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_SignatureId",
                table: "SystemSuitabilityRuns",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityRuns_TestDefinitionId",
                table: "SystemSuitabilityRuns",
                column: "TestDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityStandardResponses_SystemSuitabilityRunAna~1",
                table: "SystemSuitabilityStandardResponses",
                columns: new[] { "SystemSuitabilityRunAnalyteId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityStandardResponses_SystemSuitabilityRunAnal~",
                table: "SystemSuitabilityStandardResponses",
                column: "SystemSuitabilityRunAnalyteId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestOrders_SystemSuitabilityRuns_SystemSuitabilityRunId",
                table: "TestOrders",
                column: "SystemSuitabilityRunId",
                principalTable: "SystemSuitabilityRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
