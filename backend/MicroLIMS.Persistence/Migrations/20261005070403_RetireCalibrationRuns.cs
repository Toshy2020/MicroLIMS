using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireCalibrationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps run BEFORE the schema drops. No hard-coded ids.
            // 1. Repair: a mis-saved HPLC test (WorkflowType 3 with an HPLC method) goes back to
            //    WorkflowType 12 so step 2 does not delete it (production has one: HPLC-FOLIC).
            migrationBuilder.Sql(@"
UPDATE ""TestDefinitions"" SET ""WorkflowType"" = 12
WHERE ""EquationType"" = 13 AND ""HplcMethodId"" IS NOT NULL AND ""WorkflowType"" <> 12;");

            // 2. Calibration runs go first (a CalibrationRuns row restricts its TestDefinition).
            //    FK chain: ParameterResults.ValidityRecordItemId -> CalibrationRunAnalytes;
            //    CalibrationRunChecks/Documents/Analytes -> CalibrationRuns (cascade).
            migrationBuilder.Sql(@"
UPDATE ""ParameterResults"" SET ""ValidityRecordItemId"" = NULL WHERE ""ValidityRecordItemId"" IS NOT NULL;
DELETE FROM ""CalibrationRunChecks"";
DELETE FROM ""CalibrationRunDocuments"";
DELETE FROM ""CalibrationRunAnalytes"";
DELETE FROM ""CalibrationRuns"";");

            // 3. Everything that belongs to the elemental (WorkflowType 3) tests still left.
            //    Children first; rows with ON DELETE CASCADE go with their parent
            //    (ResultReadings <- ParameterResults; WorkflowStepResults children; SampleLocations;
            //    TestWorkflowSteps; TestDefinitionStageReplicates). Incubations, HplcRunSamples and
            //    IcpRunSamples are deliberately NOT deleted: an elemental order should have none, and
            //    if one does, the FK failure stops the migration instead of silently erasing evidence.
            migrationBuilder.Sql(@"
DELETE FROM ""ParameterResults"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3));
DELETE FROM ""TestAnalyses"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3));
DELETE FROM ""ResultRecords"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3));
DELETE FROM ""WorkflowStepResults"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3));
DELETE FROM ""LocationPathogenObservations"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3));
DELETE FROM ""TestReturnEvents"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3));
DELETE FROM ""TestOrders"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3);
DELETE FROM ""SampleTests"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3);
DELETE FROM ""Specifications"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3)
   OR ""TestAnalyteId"" IN (SELECT a.""Id"" FROM ""TestAnalytes"" a JOIN ""TestDefinitions"" d ON d.""Id"" = a.""TestDefinitionId"" WHERE d.""WorkflowType"" = 3);
DELETE FROM ""TestAnalytes"" WHERE ""TestDefinitionId"" IN (SELECT ""Id"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3);
DELETE FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 3;");

            migrationBuilder.DropForeignKey(
                name: "FK_ParameterResults_CalibrationRunAnalytes_ValidityRecordItemId",
                table: "ParameterResults");

            migrationBuilder.DropForeignKey(
                name: "FK_Specifications_TestAnalytes_TestAnalyteId",
                table: "Specifications");

            migrationBuilder.DropTable(
                name: "CalibrationRunChecks");

            migrationBuilder.DropTable(
                name: "CalibrationRunDocuments");

            migrationBuilder.DropTable(
                name: "CalibrationRunAnalytes");

            migrationBuilder.DropTable(
                name: "CalibrationRuns");

            migrationBuilder.DropTable(
                name: "TestAnalytes");

            migrationBuilder.DropIndex(
                name: "IX_Specifications_TestAnalyteId",
                table: "Specifications");

            migrationBuilder.DropIndex(
                name: "IX_ParameterResults_ValidityRecordItemId",
                table: "ParameterResults");

            migrationBuilder.DropColumn(
                name: "CalBlankMax",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalCheckRecoveryHighPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalCheckRecoveryLowPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalCorrelationType",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalInstrumentType",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalIsRecoveryHighPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalIsRecoveryLowPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalMaxRunAgeHours",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalMinCorrelation",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalMinStandards",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalRequireBlank",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalRequireCcv",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalRequireIcv",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalRequireInternalStandard",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalStandardLevelsMgPerL",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "CalibrationEntryMode",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "ReportedConcentrationBasis",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TestAnalyteId",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ValidityRecordItemId",
                table: "ParameterResults");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreates the retired schema only; the deleted data is not restored.
            migrationBuilder.AddColumn<decimal>(
                name: "CalBlankMax",
                table: "TestDefinitions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalCheckRecoveryHighPercent",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalCheckRecoveryLowPercent",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalCorrelationType",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalInstrumentType",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalIsRecoveryHighPercent",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalIsRecoveryLowPercent",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalMaxRunAgeHours",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalMinCorrelation",
                table: "TestDefinitions",
                type: "numeric(10,6)",
                precision: 10,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalMinStandards",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CalRequireBlank",
                table: "TestDefinitions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CalRequireCcv",
                table: "TestDefinitions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CalRequireIcv",
                table: "TestDefinitions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CalRequireInternalStandard",
                table: "TestDefinitions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalStandardLevelsMgPerL",
                table: "TestDefinitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalibrationEntryMode",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportedConcentrationBasis",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TestAnalyteId",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidityRecordItemId",
                table: "ParameterResults",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalibrationRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalibrationStandardMaterialId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    IcvStandardMaterialId = table.Column<int>(type: "integer", nullable: true),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    WithdrawalSignatureId = table.Column<int>(type: "integer", nullable: true),
                    WithdrawnByUserId = table.Column<int>(type: "integer", nullable: true),
                    AnalytesPassed = table.Column<int>(type: "integer", nullable: false),
                    AnalytesTotal = table.Column<int>(type: "integer", nullable: false),
                    CalibrationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WithdrawalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_ElectronicSignatures_WithdrawalSignatureId",
                        column: x => x.WithdrawalSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_Materials_CalibrationStandardMaterialId",
                        column: x => x.CalibrationStandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_Materials_IcvStandardMaterialId",
                        column: x => x.IcvStandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRuns_Users_WithdrawnByUserId",
                        column: x => x.WithdrawnByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestAnalytes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Element = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LoqMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    SstMaxRsdPercent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    SstMaxTailingFactor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    SstMinResolution = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    SstMinTheoreticalPlates = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    View = table.Column<int>(type: "integer", nullable: true),
                    WavelengthNm = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAnalytes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestAnalytes_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationRunDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalibrationRunId = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationRunDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationRunDocuments_CalibrationRuns_CalibrationRunId",
                        column: x => x.CalibrationRunId,
                        principalTable: "CalibrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalibrationRunDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationRunAnalytes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalibrationRunId = table.Column<int>(type: "integer", nullable: false),
                    TestAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    CorrelationType = table.Column<int>(type: "integer", nullable: false),
                    CorrelationValue = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    Element = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HighestStandardMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    LowestStandardMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    NumberOfStandards = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    View = table.Column<int>(type: "integer", nullable: false),
                    WavelengthNm = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationRunAnalytes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationRunAnalytes_CalibrationRuns_CalibrationRunId",
                        column: x => x.CalibrationRunId,
                        principalTable: "CalibrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalibrationRunAnalytes_TestAnalytes_TestAnalyteId",
                        column: x => x.TestAnalyteId,
                        principalTable: "TestAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationRunChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalibrationRunAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    CheckType = table.Column<int>(type: "integer", nullable: false),
                    MeasuredMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    NominalMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    RecoveryPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    SequencePosition = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationRunChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationRunChecks_CalibrationRunAnalytes_CalibrationRunA~",
                        column: x => x.CalibrationRunAnalyteId,
                        principalTable: "CalibrationRunAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_TestAnalyteId",
                table: "Specifications",
                column: "TestAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterResults_ValidityRecordItemId",
                table: "ParameterResults",
                column: "ValidityRecordItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRunAnalytes_CalibrationRunId",
                table: "CalibrationRunAnalytes",
                column: "CalibrationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRunAnalytes_TestAnalyteId",
                table: "CalibrationRunAnalytes",
                column: "TestAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRunChecks_CalibrationRunAnalyteId",
                table: "CalibrationRunChecks",
                column: "CalibrationRunAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRunDocuments_CalibrationRunId",
                table: "CalibrationRunDocuments",
                column: "CalibrationRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRunDocuments_UploadedByUserId",
                table: "CalibrationRunDocuments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_CalibrationAt",
                table: "CalibrationRuns",
                column: "CalibrationAt");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_CalibrationStandardMaterialId",
                table: "CalibrationRuns",
                column: "CalibrationStandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_Code",
                table: "CalibrationRuns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_EquipmentId",
                table: "CalibrationRuns",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_IcvStandardMaterialId",
                table: "CalibrationRuns",
                column: "IcvStandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_PerformedAt",
                table: "CalibrationRuns",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_PerformedByUserId",
                table: "CalibrationRuns",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_SectionId",
                table: "CalibrationRuns",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_SignatureId",
                table: "CalibrationRuns",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_Status",
                table: "CalibrationRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_TestDefinitionId",
                table: "CalibrationRuns",
                column: "TestDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_WithdrawalSignatureId",
                table: "CalibrationRuns",
                column: "WithdrawalSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRuns_WithdrawnByUserId",
                table: "CalibrationRuns",
                column: "WithdrawnByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAnalytes_TestDefinitionId_Element_WavelengthNm",
                table: "TestAnalytes",
                columns: new[] { "TestDefinitionId", "Element", "WavelengthNm" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ParameterResults_CalibrationRunAnalytes_ValidityRecordItemId",
                table: "ParameterResults",
                column: "ValidityRecordItemId",
                principalTable: "CalibrationRunAnalytes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Specifications_TestAnalytes_TestAnalyteId",
                table: "Specifications",
                column: "TestAnalyteId",
                principalTable: "TestAnalytes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
