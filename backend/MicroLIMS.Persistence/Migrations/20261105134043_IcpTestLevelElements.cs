using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IcpTestLevelElements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ICP redesign: the method master is gone, tests define their own elements. Not in production yet,
            // so all old ICP data is removed (the owner confirmed). Data steps run BEFORE the schema changes.
            // WorkflowType 14 = IcpMethodAssay.
            // 1. Runs and everything under them (calibration, CCV, samples, replicates, concentrations).
            //    Evidence first: it restricts its run sample.
            migrationBuilder.Sql(@"
DELETE FROM ""IcpEvidences"";
DELETE FROM ""IcpRuns"";");

            // 2. Results and orders of the ICP tests. Rows with ON DELETE CASCADE go with their parent.
            migrationBuilder.Sql(@"
DELETE FROM ""ParameterResults"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 14));
DELETE FROM ""TestAnalyses"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 14));
DELETE FROM ""ResultRecords"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 14));
DELETE FROM ""WorkflowStepResults"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 14));
DELETE FROM ""TestReturnEvents"" WHERE ""TestOrderId"" IN (SELECT o.""Id"" FROM ""TestOrders"" o WHERE o.""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 14));
DELETE FROM ""TestOrders"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 14);
DELETE FROM ""SampleTests"" WHERE ""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 14);");

            // 3. Item specifications were keyed by old method elements; the tests are kept but must be
            //    re-configured (mode, elements) in Test Master.
            migrationBuilder.Sql(@"
DELETE FROM ""Specifications"" WHERE ""IcpMethodElementId"" IS NOT NULL
   OR ""TestCode"" IN (SELECT ""Code"" FROM ""TestDefinitions"" WHERE ""WorkflowType"" = 14);");

            migrationBuilder.DropForeignKey(
                name: "FK_IcpCalibrations_Materials_IcvStandardMaterialId",
                table: "IcpCalibrations");

            migrationBuilder.DropForeignKey(
                name: "FK_IcpRuns_IcpMethods_IcpMethodId",
                table: "IcpRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Specifications_IcpMethodElements_IcpMethodElementId",
                table: "Specifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_IcpMethods_IcpMethodId",
                table: "TestDefinitions");

            migrationBuilder.DropTable(
                name: "IcpCcvReadings");

            migrationBuilder.DropTable(
                name: "IcpMethodElements");

            migrationBuilder.DropTable(
                name: "IcpReplicateConcentrations");

            migrationBuilder.DropTable(
                name: "IcpMethods");

            migrationBuilder.DropTable(
                name: "IcpSampleReplicates");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_IcpMethodId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_IcpRuns_IcpMethodId",
                table: "IcpRuns");

            migrationBuilder.DropIndex(
                name: "IX_IcpCalibrations_IcvStandardMaterialId",
                table: "IcpCalibrations");

            migrationBuilder.DropIndex(
                name: "IX_IcpCalibrationElements_IcpCalibrationId_IcpMethodElementId",
                table: "IcpCalibrationElements");

            migrationBuilder.DropColumn(
                name: "AmountUnit",
                table: "IcpRunSamples");

            migrationBuilder.DropColumn(
                name: "IcpMethodId",
                table: "IcpRuns");

            migrationBuilder.DropColumn(
                name: "MethodSnapshotJson",
                table: "IcpRuns");

            migrationBuilder.DropColumn(
                name: "IcvStandardMaterialId",
                table: "IcpCalibrations");

            migrationBuilder.DropColumn(
                name: "BlankMgPerL",
                table: "IcpCalibrationElements");

            migrationBuilder.DropColumn(
                name: "IcpMethodElementId",
                table: "IcpCalibrationElements");

            migrationBuilder.DropColumn(
                name: "IcvMeasuredMgPerL",
                table: "IcpCalibrationElements");

            migrationBuilder.DropColumn(
                name: "IcvRecoveryPercent",
                table: "IcpCalibrationElements");

            migrationBuilder.DropColumn(
                name: "IcpMethodId",
                table: "TestDefinitions");

            migrationBuilder.AddColumn<int>(
                name: "IcpSampleForm",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "IcpMethodElementId",
                table: "Specifications");

            migrationBuilder.AddColumn<int>(
                name: "IcpTestElementId",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_IcpTestElementId",
                table: "Specifications",
                column: "IcpTestElementId");

            migrationBuilder.DropColumn(
                name: "UnitAmount",
                table: "IcpRunSamples");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualWeightG",
                table: "IcpRunSamples",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IcpMode",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IcpSpecificGravity",
                table: "TestDefinitions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IcpTheoreticalAmount",
                table: "TestDefinitions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TestDefinitionIcpElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TargetPpm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDefinitionIcpElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestDefinitionIcpElements_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IcpSampleConcentrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpRunSampleId = table.Column<int>(type: "integer", nullable: false),
                    IcpTestElementId = table.Column<int>(type: "integer", nullable: false),
                    ResultPpm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpSampleConcentrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpSampleConcentrations_IcpRunSamples_IcpRunSampleId",
                        column: x => x.IcpRunSampleId,
                        principalTable: "IcpRunSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IcpSampleConcentrations_TestDefinitionIcpElements_IcpTestEl~",
                        column: x => x.IcpTestElementId,
                        principalTable: "TestDefinitionIcpElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IcpCalibrationElements_IcpCalibrationId_Symbol",
                table: "IcpCalibrationElements",
                columns: new[] { "IcpCalibrationId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpSampleConcentrations_IcpRunSampleId_IcpTestElementId",
                table: "IcpSampleConcentrations",
                columns: new[] { "IcpRunSampleId", "IcpTestElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpSampleConcentrations_IcpTestElementId",
                table: "IcpSampleConcentrations",
                column: "IcpTestElementId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitionIcpElements_TestDefinitionId_Symbol",
                table: "TestDefinitionIcpElements",
                columns: new[] { "TestDefinitionId", "Symbol" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Specifications_TestDefinitionIcpElements_IcpTestElementId",
                table: "Specifications",
                column: "IcpTestElementId",
                principalTable: "TestDefinitionIcpElements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Specifications_TestDefinitionIcpElements_IcpTestElementId",
                table: "Specifications");

            migrationBuilder.DropTable(
                name: "IcpSampleConcentrations");

            migrationBuilder.DropTable(
                name: "TestDefinitionIcpElements");

            migrationBuilder.DropIndex(
                name: "IX_IcpCalibrationElements_IcpCalibrationId_Symbol",
                table: "IcpCalibrationElements");

            migrationBuilder.DropColumn(
                name: "IcpMode",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "IcpSpecificGravity",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "IcpTheoreticalAmount",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "IcpSampleForm",
                table: "TestDefinitions");

            migrationBuilder.AddColumn<int>(
                name: "IcpMethodId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "IcpTestElementId",
                table: "Specifications");

            migrationBuilder.AddColumn<int>(
                name: "IcpMethodElementId",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_IcpMethodElementId",
                table: "Specifications",
                column: "IcpMethodElementId");

            migrationBuilder.DropColumn(
                name: "ActualWeightG",
                table: "IcpRunSamples");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitAmount",
                table: "IcpRunSamples",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmountUnit",
                table: "IcpRunSamples",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IcpMethodId",
                table: "IcpRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MethodSnapshotJson",
                table: "IcpRuns",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "IcvStandardMaterialId",
                table: "IcpCalibrations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BlankMgPerL",
                table: "IcpCalibrationElements",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IcpMethodElementId",
                table: "IcpCalibrationElements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "IcvMeasuredMgPerL",
                table: "IcpCalibrationElements",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IcvRecoveryPercent",
                table: "IcpCalibrationElements",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IcpCcvReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpRunId = table.Column<int>(type: "integer", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    IcpMethodElementId = table.Column<int>(type: "integer", nullable: false),
                    MeasuredMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    RecoveryPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpCcvReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpCcvReadings_IcpRuns_IcpRunId",
                        column: x => x.IcpRunId,
                        principalTable: "IcpRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IcpMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalibrationStandardEntryId = table.Column<int>(type: "integer", nullable: false),
                    IcvStandardEntryId = table.Column<int>(type: "integer", nullable: true),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BlankMaxMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CcvNominalMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CcvRecoveryHighPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CcvRecoveryLowPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    DilutionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IcvNominalMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    IcvRecoveryHighPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    IcvRecoveryLowPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    MaxCalibrationAgeHours = table.Column<int>(type: "integer", nullable: false),
                    MinCorrelation = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequireBlank = table.Column<bool>(type: "boolean", nullable: false),
                    RequireCcv = table.Column<bool>(type: "boolean", nullable: false),
                    RequireIcv = table.Column<bool>(type: "boolean", nullable: false),
                    SampleVolumeMl = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    StandardLevelsMgPerL = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "IcpSampleReplicates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpRunSampleId = table.Column<int>(type: "integer", nullable: false),
                    DilutionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReplicateNo = table.Column<int>(type: "integer", nullable: false),
                    SampleAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    VolumeMl = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpSampleReplicates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpSampleReplicates_IcpRunSamples_IcpRunSampleId",
                        column: x => x.IcpRunSampleId,
                        principalTable: "IcpRunSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IcpMethodElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpMethodId = table.Column<int>(type: "integer", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    View = table.Column<int>(type: "integer", nullable: false),
                    WavelengthNm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "IcpReplicateConcentrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpSampleReplicateId = table.Column<int>(type: "integer", nullable: false),
                    IcpMethodElementId = table.Column<int>(type: "integer", nullable: false),
                    SolutionMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpReplicateConcentrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpReplicateConcentrations_IcpSampleReplicates_IcpSampleRep~",
                        column: x => x.IcpSampleReplicateId,
                        principalTable: "IcpSampleReplicates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_IcpMethodId",
                table: "TestDefinitions",
                column: "IcpMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpRuns_IcpMethodId",
                table: "IcpRuns",
                column: "IcpMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpCalibrations_IcvStandardMaterialId",
                table: "IcpCalibrations",
                column: "IcvStandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpCalibrationElements_IcpCalibrationId_IcpMethodElementId",
                table: "IcpCalibrationElements",
                columns: new[] { "IcpCalibrationId", "IcpMethodElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpCcvReadings_IcpRunId_IcpMethodElementId",
                table: "IcpCcvReadings",
                columns: new[] { "IcpRunId", "IcpMethodElementId" });

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

            migrationBuilder.CreateIndex(
                name: "IX_IcpReplicateConcentrations_IcpSampleReplicateId_IcpMethodEl~",
                table: "IcpReplicateConcentrations",
                columns: new[] { "IcpSampleReplicateId", "IcpMethodElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpSampleReplicates_IcpRunSampleId_ReplicateNo",
                table: "IcpSampleReplicates",
                columns: new[] { "IcpRunSampleId", "ReplicateNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IcpCalibrations_Materials_IcvStandardMaterialId",
                table: "IcpCalibrations",
                column: "IcvStandardMaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IcpRuns_IcpMethods_IcpMethodId",
                table: "IcpRuns",
                column: "IcpMethodId",
                principalTable: "IcpMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
    }
}
