using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSuitabilityRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SystemSuitabilityRunId",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquationType",
                table: "TestDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MethodAbbreviation",
                table: "TestDefinitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSystemSuitability",
                table: "TestDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SstMaxRsdPercent",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SstMaxTailingFactor",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SstMinResolution",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SstMinTheoreticalPlates",
                table: "TestDefinitions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemSuitabilityRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    ChromatographyColumnId = table.Column<int>(type: "integer", nullable: false),
                    ReferenceStandardMaterialId = table.Column<int>(type: "integer", nullable: false),
                    StandardPurityPercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    StandardWeightMg = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StandardDilution = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StandardMeanArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RsdPercent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Resolution = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TailingFactor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TheoreticalPlates = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_TestOrders_SystemSuitabilityRunId",
                table: "TestOrders",
                column: "SystemSuitabilityRunId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_TestOrders_SystemSuitabilityRuns_SystemSuitabilityRunId",
                table: "TestOrders",
                column: "SystemSuitabilityRunId",
                principalTable: "SystemSuitabilityRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestOrders_SystemSuitabilityRuns_SystemSuitabilityRunId",
                table: "TestOrders");

            migrationBuilder.DropTable(
                name: "SystemSuitabilityRuns");

            migrationBuilder.DropIndex(
                name: "IX_TestOrders_SystemSuitabilityRunId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SystemSuitabilityRunId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "EquationType",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "MethodAbbreviation",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "RequiresSystemSuitability",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "SstMaxRsdPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "SstMaxTailingFactor",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "SstMinResolution",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "SstMinTheoreticalPlates",
                table: "TestDefinitions");
        }
    }
}
