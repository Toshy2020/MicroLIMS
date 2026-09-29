using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHplcWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HplcRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    ChromatographyColumnId = table.Column<int>(type: "integer", nullable: false),
                    HplcMethodId = table.Column<int>(type: "integer", nullable: false),
                    MethodSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    AnalystUserId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcRuns_ChromatographyColumns_ChromatographyColumnId",
                        column: x => x.ChromatographyColumnId,
                        principalTable: "ChromatographyColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcRuns_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcRuns_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcRuns_HplcMethods_HplcMethodId",
                        column: x => x.HplcMethodId,
                        principalTable: "HplcMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HplcRunMobilePhases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcRunId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    SolutionPreparationId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcRunMobilePhases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcRunMobilePhases_HplcRuns_HplcRunId",
                        column: x => x.HplcRunId,
                        principalTable: "HplcRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HplcRunMobilePhases_SolutionPreparations_SolutionPreparatio~",
                        column: x => x.SolutionPreparationId,
                        principalTable: "SolutionPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HplcRunSamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcRunId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RemovedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcRunSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcRunSamples_HplcRuns_HplcRunId",
                        column: x => x.HplcRunId,
                        principalTable: "HplcRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HplcRunSamples_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HplcSstRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcRunId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignatureId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcSstRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcSstRecords_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcSstRecords_HplcRuns_HplcRunId",
                        column: x => x.HplcRunId,
                        principalTable: "HplcRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HplcEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcRunId = table.Column<int>(type: "integer", nullable: false),
                    HplcRunSampleId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_HplcEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcEvidences_HplcEvidences_SupersededByEvidenceId",
                        column: x => x.SupersededByEvidenceId,
                        principalTable: "HplcEvidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcEvidences_HplcRunSamples_HplcRunSampleId",
                        column: x => x.HplcRunSampleId,
                        principalTable: "HplcRunSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcEvidences_HplcRuns_HplcRunId",
                        column: x => x.HplcRunId,
                        principalTable: "HplcRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HplcSampleReplicates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcRunSampleId = table.Column<int>(type: "integer", nullable: false),
                    ReplicateNo = table.Column<int>(type: "integer", nullable: false),
                    ActualWeightMg = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcSampleReplicates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcSampleReplicates_HplcRunSamples_HplcRunSampleId",
                        column: x => x.HplcRunSampleId,
                        principalTable: "HplcRunSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HplcSstAnalytes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcSstRecordId = table.Column<int>(type: "integer", nullable: false),
                    HplcMethodAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    AnalyteName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StandardMaterialId = table.Column<int>(type: "integer", nullable: true),
                    StandardPurityPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    StandardMoisturePercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    StandardWeightMg = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    MeanResponse = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ComputedRsdPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ReportedRsdPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Resolution = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TailingFactor = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TheoreticalPlates = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    RetentionFactor = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    SignalToNoise = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    PeakToValley = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcSstAnalytes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcSstAnalytes_HplcSstRecords_HplcSstRecordId",
                        column: x => x.HplcSstRecordId,
                        principalTable: "HplcSstRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HplcSstAnalytes_Materials_StandardMaterialId",
                        column: x => x.StandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HplcReplicateResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcSampleReplicateId = table.Column<int>(type: "integer", nullable: false),
                    HplcMethodAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    Response = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcReplicateResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcReplicateResponses_HplcSampleReplicates_HplcSampleRepli~",
                        column: x => x.HplcSampleReplicateId,
                        principalTable: "HplcSampleReplicates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HplcSstInjections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcSstAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    InjectionNo = table.Column<int>(type: "integer", nullable: false),
                    Response = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcSstInjections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcSstInjections_HplcSstAnalytes_HplcSstAnalyteId",
                        column: x => x.HplcSstAnalyteId,
                        principalTable: "HplcSstAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HplcEvidences_HplcRunId_Context_Kind",
                table: "HplcEvidences",
                columns: new[] { "HplcRunId", "Context", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_HplcEvidences_HplcRunSampleId",
                table: "HplcEvidences",
                column: "HplcRunSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcEvidences_SupersededByEvidenceId",
                table: "HplcEvidences",
                column: "SupersededByEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcReplicateResponses_HplcSampleReplicateId_HplcMethodAnal~",
                table: "HplcReplicateResponses",
                columns: new[] { "HplcSampleReplicateId", "HplcMethodAnalyteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcRunMobilePhases_HplcRunId_Channel",
                table: "HplcRunMobilePhases",
                columns: new[] { "HplcRunId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcRunMobilePhases_SolutionPreparationId",
                table: "HplcRunMobilePhases",
                column: "SolutionPreparationId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcRuns_ChromatographyColumnId",
                table: "HplcRuns",
                column: "ChromatographyColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcRuns_Code",
                table: "HplcRuns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcRuns_EquipmentId_Status",
                table: "HplcRuns",
                columns: new[] { "EquipmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HplcRuns_HplcMethodId",
                table: "HplcRuns",
                column: "HplcMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcRuns_SectionId",
                table: "HplcRuns",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcRunSamples_HplcRunId",
                table: "HplcRunSamples",
                column: "HplcRunId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcRunSamples_TestOrderId",
                table: "HplcRunSamples",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcSampleReplicates_HplcRunSampleId_ReplicateNo",
                table: "HplcSampleReplicates",
                columns: new[] { "HplcRunSampleId", "ReplicateNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcSstAnalytes_HplcSstRecordId",
                table: "HplcSstAnalytes",
                column: "HplcSstRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcSstAnalytes_StandardMaterialId",
                table: "HplcSstAnalytes",
                column: "StandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcSstInjections_HplcSstAnalyteId",
                table: "HplcSstInjections",
                column: "HplcSstAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcSstRecords_Code",
                table: "HplcSstRecords",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcSstRecords_HplcRunId",
                table: "HplcSstRecords",
                column: "HplcRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcSstRecords_SignatureId",
                table: "HplcSstRecords",
                column: "SignatureId");

            // DbSeeder.SeedPermissionsAndGrants seeds Hplc.Operate on a fresh
            // database. This block carries the same INSERTs for databases that
            // already exist, following 20260929173324_AddSolutionPreparation.
            // Both statements are idempotent and no-op where the seeder already
            // did the work. RoleType: 0 = SystemAdministrator, 1 = SectionHead,
            // 3 = Analyst (see RoleType.cs).
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'Hplc.Operate', 'Operate the HPLC workspace: start runs, confirm system suitability, assign samples, enter replicates and submit for review.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'Hplc.Operate');");
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r CROSS JOIN ""Permissions"" p
                WHERE r.""Type"" IN (0, 1, 3) -- SystemAdministrator, SectionHead, Analyst
                  AND p.""Code"" = 'Hplc.Operate'
                  AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                  WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN
                (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'Hplc.Operate');");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'Hplc.Operate';");

            migrationBuilder.DropTable(
                name: "HplcEvidences");

            migrationBuilder.DropTable(
                name: "HplcReplicateResponses");

            migrationBuilder.DropTable(
                name: "HplcRunMobilePhases");

            migrationBuilder.DropTable(
                name: "HplcSstInjections");

            migrationBuilder.DropTable(
                name: "HplcSampleReplicates");

            migrationBuilder.DropTable(
                name: "HplcSstAnalytes");

            migrationBuilder.DropTable(
                name: "HplcRunSamples");

            migrationBuilder.DropTable(
                name: "HplcSstRecords");

            migrationBuilder.DropTable(
                name: "HplcRuns");
        }
    }
}
