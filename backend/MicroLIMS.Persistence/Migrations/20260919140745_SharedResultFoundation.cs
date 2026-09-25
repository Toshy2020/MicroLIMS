using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedResultFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    AnalysisType = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: true),
                    AnalysedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnitAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SampleMatrix = table.Column<int>(type: "integer", nullable: true),
                    ConditionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ValidityRecordType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ValidityRecordId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestAnalyses_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestAnalyses_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestAnalyses_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestAnalyses_Users_EnteredByUserId",
                        column: x => x.EnteredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParameterResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestAnalysisId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    SpecificationId = table.Column<int>(type: "integer", nullable: false),
                    ParameterName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ReportedValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ReportedDisplay = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SpecLimit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResultBasis = table.Column<int>(type: "integer", nullable: true),
                    ComparisonStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OverRange = table.Column<bool>(type: "boolean", nullable: false),
                    BelowLoq = table.Column<bool>(type: "boolean", nullable: false),
                    ValidityRecordItemId = table.Column<int>(type: "integer", nullable: true),
                    CalculationJson = table.Column<string>(type: "jsonb", nullable: true),
                    StageReached = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParameterResults_CalibrationRunAnalytes_ValidityRecordItemId",
                        column: x => x.ValidityRecordItemId,
                        principalTable: "CalibrationRunAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParameterResults_Specifications_SpecificationId",
                        column: x => x.SpecificationId,
                        principalTable: "Specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParameterResults_TestAnalyses_TestAnalysisId",
                        column: x => x.TestAnalysisId,
                        principalTable: "TestAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParameterResults_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParameterResultId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: true),
                    TimePointMinutes = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Value1 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Value2 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Value3 = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ComputedValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultReadings_ParameterResults_ParameterResultId",
                        column: x => x.ParameterResultId,
                        principalTable: "ParameterResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterResults_SpecificationId",
                table: "ParameterResults",
                column: "SpecificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterResults_TestAnalysisId",
                table: "ParameterResults",
                column: "TestAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterResults_TestOrderId_IsActive",
                table: "ParameterResults",
                columns: new[] { "TestOrderId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterResults_ValidityRecordItemId",
                table: "ParameterResults",
                column: "ValidityRecordItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultReadings_ParameterResultId",
                table: "ResultReadings",
                column: "ParameterResultId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAnalyses_EnteredAt",
                table: "TestAnalyses",
                column: "EnteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_TestAnalyses_EnteredByUserId",
                table: "TestAnalyses",
                column: "EnteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAnalyses_EquipmentId",
                table: "TestAnalyses",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAnalyses_SignatureId",
                table: "TestAnalyses",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAnalyses_TestOrderId_IsActive",
                table: "TestAnalyses",
                columns: new[] { "TestOrderId", "IsActive" });

            migrationBuilder.Sql(@"
INSERT INTO ""TestAnalyses"" (""Id"", ""TestOrderId"", ""AnalysisType"", ""EquipmentId"", ""AnalysedAt"", ""UnitAmount"", ""SampleMatrix"", ""ConditionsJson"", ""ValidityRecordType"", ""ValidityRecordId"", ""IsActive"", ""EnteredByUserId"", ""EnteredAt"", ""SignatureId"", ""Comment"")
SELECT ""Id"", ""TestOrderId"", 3, NULL, ""AnalysedAt"", ""UnitAmount"", ""SampleMatrix"", NULL, 'CalibrationRun', NULL, ""IsActive"", ""EnteredByUserId"", ""EnteredAt"", ""SignatureId"", ""Comment""
FROM ""ElementalAssayEntries"";

SELECT setval(pg_get_serial_sequence('""TestAnalyses""', 'Id'), COALESCE(MAX(""Id""), 1), MAX(""Id"") IS NOT NULL) FROM ""TestAnalyses"";

INSERT INTO ""ParameterResults"" (""Id"", ""TestAnalysisId"", ""TestOrderId"", ""SpecificationId"", ""ParameterName"", ""ReportedValue"", ""ReportedDisplay"", ""Unit"", ""SpecLimit"", ""ResultBasis"", ""ComparisonStatus"", ""OverRange"", ""BelowLoq"", ""ValidityRecordItemId"", ""CalculationJson"", ""StageReached"", ""IsActive"")
SELECT r.""Id"", r.""EntryId"", r.""TestOrderId"", r.""SpecificationId"", r.""ParameterName"", r.""ReportedValue"", r.""ReportedDisplay"", r.""Unit"", r.""SpecLimit"", r.""ResultBasis"", r.""ComparisonStatus"", r.""OverRange"", r.""BelowLoq"", r.""CalibrationRunAnalyteId"",
       json_build_object(
           'element', r.""Element"",
           'reportedPpm', r.""ReportedPpm"",
           'overRange', r.""OverRange"",
           'belowLoq', r.""BelowLoq"",
           'mgPerUnit', r.""MgPerUnit"",
           'resultClaim', r.""ResultClaim"",
           'percentLabelClaim', r.""PercentLabelClaim"",
           'runCode', COALESCE(cr.""Code"", ''),
           'runAnalytePassed', COALESCE(cra.""Passed"", false)
       )::jsonb,
       NULL, r.""IsActive""
FROM ""ElementalAssayResults"" r
LEFT JOIN ""CalibrationRunAnalytes"" cra ON cra.""Id"" = r.""CalibrationRunAnalyteId""
LEFT JOIN ""CalibrationRuns"" cr ON cr.""Id"" = cra.""CalibrationRunId"";

SELECT setval(pg_get_serial_sequence('""ParameterResults""', 'Id'), COALESCE(MAX(""Id""), 1), MAX(""Id"") IS NOT NULL) FROM ""ParameterResults"";

UPDATE ""ResultRecords""
SET ""SourceTable"" = 'ParameterResult'
WHERE ""SourceTable"" = 'ElementalAssayResult';
");

            migrationBuilder.DropTable(
                name: "ElementalAssayResults");

            migrationBuilder.DropTable(
                name: "ElementalAssayEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElementalAssayEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    AnalysedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SampleMatrix = table.Column<int>(type: "integer", nullable: false),
                    UnitAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
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
                    CalibrationRunAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    EntryId = table.Column<int>(type: "integer", nullable: false),
                    SpecificationId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    BelowLoq = table.Column<bool>(type: "boolean", nullable: false),
                    ComparisonStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Element = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MgPerUnit = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    OverRange = table.Column<bool>(type: "boolean", nullable: false),
                    ParameterName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PercentLabelClaim = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ReportedDisplay = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReportedPpm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReportedValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ResultBasis = table.Column<int>(type: "integer", nullable: false),
                    ResultClaim = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    SpecLimit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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

            migrationBuilder.Sql(@"
INSERT INTO ""ElementalAssayEntries"" (""Id"", ""TestOrderId"", ""SampleMatrix"", ""UnitAmount"", ""AnalysedAt"", ""IsActive"", ""EnteredByUserId"", ""EnteredAt"", ""SignatureId"", ""Comment"")
SELECT ""Id"", ""TestOrderId"", COALESCE(""SampleMatrix"", 0), COALESCE(""UnitAmount"", 0), ""AnalysedAt"", ""IsActive"", ""EnteredByUserId"", ""EnteredAt"", ""SignatureId"", ""Comment""
FROM ""TestAnalyses""
WHERE ""AnalysisType"" = 3;

SELECT setval(pg_get_serial_sequence('""ElementalAssayEntries""', 'Id'), COALESCE(MAX(""Id""), 1), MAX(""Id"") IS NOT NULL) FROM ""ElementalAssayEntries"";

INSERT INTO ""ElementalAssayResults"" (""Id"", ""EntryId"", ""TestOrderId"", ""SpecificationId"", ""CalibrationRunAnalyteId"", ""ParameterName"", ""Element"", ""ReportedPpm"", ""OverRange"", ""BelowLoq"", ""MgPerUnit"", ""ResultClaim"", ""PercentLabelClaim"", ""ReportedValue"", ""ReportedDisplay"", ""ResultBasis"", ""SpecLimit"", ""Unit"", ""ComparisonStatus"", ""IsActive"")
SELECT r.""Id"", r.""TestAnalysisId"", r.""TestOrderId"", r.""SpecificationId"", COALESCE(r.""ValidityRecordItemId"", 0), r.""ParameterName"",
       COALESCE((r.""CalculationJson""::jsonb->>'element'), ''),
       COALESCE((r.""CalculationJson""::jsonb->>'reportedPpm')::numeric, 0),
       r.""OverRange"", r.""BelowLoq"",
       (r.""CalculationJson""::jsonb->>'mgPerUnit')::numeric,
       (r.""CalculationJson""::jsonb->>'resultClaim')::numeric,
       (r.""CalculationJson""::jsonb->>'percentLabelClaim')::numeric,
       r.""ReportedValue"", r.""ReportedDisplay"", COALESCE(r.""ResultBasis"", 1), r.""SpecLimit"", r.""Unit"", r.""ComparisonStatus"", r.""IsActive""
FROM ""ParameterResults"" r
JOIN ""TestAnalyses"" a ON a.""Id"" = r.""TestAnalysisId""
WHERE a.""AnalysisType"" = 3;

SELECT setval(pg_get_serial_sequence('""ElementalAssayResults""', 'Id'), COALESCE(MAX(""Id""), 1), MAX(""Id"") IS NOT NULL) FROM ""ElementalAssayResults"";

UPDATE ""ResultRecords""
SET ""SourceTable"" = 'ElementalAssayResult'
WHERE ""SourceTable"" = 'ParameterResult'
  AND ""SourceId"" IN (SELECT ""Id"" FROM ""ElementalAssayResults"");
");

            migrationBuilder.DropTable(
                name: "ResultReadings");

            migrationBuilder.DropTable(
                name: "ParameterResults");

            migrationBuilder.DropTable(
                name: "TestAnalyses");
        }
    }
}
