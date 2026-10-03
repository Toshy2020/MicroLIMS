using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingStandards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TestOrderId",
                table: "HplcRunSamples",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "WorkingStandardQualificationId",
                table: "HplcRunSamples",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkingStandardQualifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WorkingStandardMaterialId = table.Column<int>(type: "integer", nullable: true),
                    MaterialMasterEntryId = table.Column<int>(type: "integer", nullable: false),
                    SourceSampleId = table.Column<int>(type: "integer", nullable: true),
                    SourceMaterialName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceBatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QuantityGrams = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MoisturePercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    HplcMethodAnalyteId = table.Column<int>(type: "integer", nullable: true),
                    MeanAssayPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    RsdPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    PotencyPercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReasons = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReplicateAssaysJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreparedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreparedSignatureId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedSignatureId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedSignatureId = table.Column<int>(type: "integer", nullable: true),
                    RejectedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedSignatureId = table.Column<int>(type: "integer", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReturnReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingStandardQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_ElectronicSignatures_Approved~",
                        column: x => x.ApprovedSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_ElectronicSignatures_Prepared~",
                        column: x => x.PreparedSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_ElectronicSignatures_Rejected~",
                        column: x => x.RejectedSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_ElectronicSignatures_Reviewed~",
                        column: x => x.ReviewedSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_HplcMethodAnalytes_HplcMethod~",
                        column: x => x.HplcMethodAnalyteId,
                        principalTable: "HplcMethodAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_MaterialMasterEntries_Materia~",
                        column: x => x.MaterialMasterEntryId,
                        principalTable: "MaterialMasterEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_Materials_WorkingStandardMate~",
                        column: x => x.WorkingStandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingStandardQualifications_Samples_SourceSampleId",
                        column: x => x.SourceSampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkingStandardDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkingStandardQualificationId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededByDocumentId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingStandardDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkingStandardDocuments_WorkingStandardQualifications_Work~",
                        column: x => x.WorkingStandardQualificationId,
                        principalTable: "WorkingStandardQualifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_WorkingStandardCode",
                table: "Materials",
                column: "Code",
                unique: true,
                filter: "\"MaterialType\" = 12");

            migrationBuilder.CreateIndex(
                name: "IX_HplcRunSamples_WorkingStandardQualificationId",
                table: "HplcRunSamples",
                column: "WorkingStandardQualificationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HplcRunSamples_OneSubject",
                table: "HplcRunSamples",
                sql: "(\"TestOrderId\" IS NULL) <> (\"WorkingStandardQualificationId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardDocuments_WorkingStandardQualificationId",
                table: "WorkingStandardDocuments",
                column: "WorkingStandardQualificationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_ApprovedSignatureId",
                table: "WorkingStandardQualifications",
                column: "ApprovedSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_Code",
                table: "WorkingStandardQualifications",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_HplcMethodAnalyteId",
                table: "WorkingStandardQualifications",
                column: "HplcMethodAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_MaterialMasterEntryId",
                table: "WorkingStandardQualifications",
                column: "MaterialMasterEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_PreparedSignatureId",
                table: "WorkingStandardQualifications",
                column: "PreparedSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_RejectedSignatureId",
                table: "WorkingStandardQualifications",
                column: "RejectedSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_ReviewedSignatureId",
                table: "WorkingStandardQualifications",
                column: "ReviewedSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_SectionId",
                table: "WorkingStandardQualifications",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_SourceSampleId",
                table: "WorkingStandardQualifications",
                column: "SourceSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingStandardQualifications_WorkingStandardMaterialId_Sta~",
                table: "WorkingStandardQualifications",
                columns: new[] { "WorkingStandardMaterialId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_HplcRunSamples_WorkingStandardQualifications_WorkingStandar~",
                table: "HplcRunSamples",
                column: "WorkingStandardQualificationId",
                principalTable: "WorkingStandardQualifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // DbSeeder seeds WorkingStandards.Qualify on a fresh database; these
            // idempotent INSERTs cover existing databases. RoleType: 0 =
            // SystemAdministrator, 1 = SectionHead, 3 = Analyst - the roles that
            // hold Solutions.Prepare.
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'WorkingStandards.Qualify', 'Create and prepare working standard qualifications, attach documents, enter and submit qualification replicates.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'WorkingStandards.Qualify');");
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r CROSS JOIN ""Permissions"" p
                WHERE r.""Type"" IN (0, 1, 3)
                  AND p.""Code"" = 'WorkingStandards.Qualify'
                  AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                  WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN
                (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'WorkingStandards.Qualify');");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'WorkingStandards.Qualify';");

            migrationBuilder.DropForeignKey(
                name: "FK_HplcRunSamples_WorkingStandardQualifications_WorkingStandar~",
                table: "HplcRunSamples");

            migrationBuilder.DropTable(
                name: "WorkingStandardDocuments");

            migrationBuilder.DropTable(
                name: "WorkingStandardQualifications");

            migrationBuilder.DropIndex(
                name: "IX_Materials_WorkingStandardCode",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_HplcRunSamples_WorkingStandardQualificationId",
                table: "HplcRunSamples");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HplcRunSamples_OneSubject",
                table: "HplcRunSamples");

            migrationBuilder.DropColumn(
                name: "WorkingStandardQualificationId",
                table: "HplcRunSamples");

            migrationBuilder.Sql(@"DELETE FROM ""HplcEvidences"" WHERE ""HplcRunSampleId"" IN
                (SELECT ""Id"" FROM ""HplcRunSamples"" WHERE ""TestOrderId"" IS NULL);");
            // Replicates and their responses cascade with the run sample.
            migrationBuilder.Sql(@"DELETE FROM ""HplcRunSamples"" WHERE ""TestOrderId"" IS NULL;");
            migrationBuilder.Sql(@"DELETE FROM ""Materials"" WHERE ""MaterialType"" = 12;");

            migrationBuilder.AlterColumn<int>(
                name: "TestOrderId",
                table: "HplcRunSamples",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
