using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIcpRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IcpRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    IcpMethodId = table.Column<int>(type: "integer", nullable: false),
                    MethodSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    AnalystUserId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpRuns_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcpRuns_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcpRuns_IcpMethods_IcpMethodId",
                        column: x => x.IcpMethodId,
                        principalTable: "IcpMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IcpCalibrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpRunId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CalibrationStandardMaterialId = table.Column<int>(type: "integer", nullable: true),
                    IcvStandardMaterialId = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignatureId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpCalibrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpCalibrations_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcpCalibrations_IcpRuns_IcpRunId",
                        column: x => x.IcpRunId,
                        principalTable: "IcpRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IcpCalibrations_Materials_CalibrationStandardMaterialId",
                        column: x => x.CalibrationStandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcpCalibrations_Materials_IcvStandardMaterialId",
                        column: x => x.IcvStandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IcpCcvReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpRunId = table.Column<int>(type: "integer", nullable: false),
                    IcpMethodElementId = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MeasuredMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RecoveryPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "IcpRunSamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpRunId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AmountUnit = table.Column<int>(type: "integer", nullable: false),
                    UnitAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RemovedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpRunSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpRunSamples_IcpRuns_IcpRunId",
                        column: x => x.IcpRunId,
                        principalTable: "IcpRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IcpRunSamples_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IcpCalibrationElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpCalibrationId = table.Column<int>(type: "integer", nullable: false),
                    IcpMethodElementId = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CorrelationR = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: true),
                    BlankMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    IcvMeasuredMgPerL = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    IcvRecoveryPercent = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpCalibrationElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpCalibrationElements_IcpCalibrations_IcpCalibrationId",
                        column: x => x.IcpCalibrationId,
                        principalTable: "IcpCalibrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IcpEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpRunId = table.Column<int>(type: "integer", nullable: false),
                    IcpRunSampleId = table.Column<int>(type: "integer", nullable: true),
                    Context = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededByEvidenceId = table.Column<int>(type: "integer", nullable: true),
                    SupersedeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcpEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcpEvidences_IcpEvidences_SupersededByEvidenceId",
                        column: x => x.SupersededByEvidenceId,
                        principalTable: "IcpEvidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcpEvidences_IcpRunSamples_IcpRunSampleId",
                        column: x => x.IcpRunSampleId,
                        principalTable: "IcpRunSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IcpEvidences_IcpRuns_IcpRunId",
                        column: x => x.IcpRunId,
                        principalTable: "IcpRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IcpSampleReplicates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IcpRunSampleId = table.Column<int>(type: "integer", nullable: false),
                    ReplicateNo = table.Column<int>(type: "integer", nullable: false),
                    SampleAmount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    VolumeMl = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DilutionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
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
                name: "IX_IcpCalibrationElements_IcpCalibrationId_IcpMethodElementId",
                table: "IcpCalibrationElements",
                columns: new[] { "IcpCalibrationId", "IcpMethodElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpCalibrations_CalibrationStandardMaterialId",
                table: "IcpCalibrations",
                column: "CalibrationStandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpCalibrations_Code",
                table: "IcpCalibrations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpCalibrations_IcpRunId",
                table: "IcpCalibrations",
                column: "IcpRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpCalibrations_IcvStandardMaterialId",
                table: "IcpCalibrations",
                column: "IcvStandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpCalibrations_SignatureId",
                table: "IcpCalibrations",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpCcvReadings_IcpRunId_IcpMethodElementId",
                table: "IcpCcvReadings",
                columns: new[] { "IcpRunId", "IcpMethodElementId" });

            migrationBuilder.CreateIndex(
                name: "IX_IcpEvidences_IcpRunId_Context_Kind",
                table: "IcpEvidences",
                columns: new[] { "IcpRunId", "Context", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_IcpEvidences_IcpRunSampleId",
                table: "IcpEvidences",
                column: "IcpRunSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpEvidences_SupersededByEvidenceId",
                table: "IcpEvidences",
                column: "SupersededByEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpReplicateConcentrations_IcpSampleReplicateId_IcpMethodEl~",
                table: "IcpReplicateConcentrations",
                columns: new[] { "IcpSampleReplicateId", "IcpMethodElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpRuns_Code",
                table: "IcpRuns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpRuns_EquipmentId_Status",
                table: "IcpRuns",
                columns: new[] { "EquipmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IcpRuns_IcpMethodId",
                table: "IcpRuns",
                column: "IcpMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpRuns_SectionId",
                table: "IcpRuns",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpRunSamples_IcpRunId",
                table: "IcpRunSamples",
                column: "IcpRunId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpRunSamples_TestOrderId",
                table: "IcpRunSamples",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_IcpSampleReplicates_IcpRunSampleId_ReplicateNo",
                table: "IcpSampleReplicates",
                columns: new[] { "IcpRunSampleId", "ReplicateNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IcpCalibrationElements");

            migrationBuilder.DropTable(
                name: "IcpCcvReadings");

            migrationBuilder.DropTable(
                name: "IcpEvidences");

            migrationBuilder.DropTable(
                name: "IcpReplicateConcentrations");

            migrationBuilder.DropTable(
                name: "IcpCalibrations");

            migrationBuilder.DropTable(
                name: "IcpSampleReplicates");

            migrationBuilder.DropTable(
                name: "IcpRunSamples");

            migrationBuilder.DropTable(
                name: "IcpRuns");
        }
    }
}
