using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHplcMethodMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HplcMethodId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HplcMethodAnalyteId",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HplcMethods",
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
                    ColumnDesignation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ColumnLengthMm = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    ColumnInternalDiameterMm = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    ParticleSizeUm = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    ColumnBrand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ColumnPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ColumnTemperatureC = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    ElutionMode = table.Column<int>(type: "integer", nullable: false),
                    EquilibrationMin = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    FlowRateMlPerMin = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    DetectorType = table.Column<int>(type: "integer", nullable: false),
                    InjectionVolumeUl = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    RunTimeMin = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    DiluentSolutionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcMethods_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcMethods_SolutionMasters_DiluentSolutionId",
                        column: x => x.DiluentSolutionId,
                        principalTable: "SolutionMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HplcMethodAnalytes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcMethodId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WavelengthNm = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    StandardEntryId = table.Column<int>(type: "integer", nullable: false),
                    TheoreticalWeightStdMg = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    TheoreticalWeightTestMg = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    StandardInjections = table.Column<int>(type: "integer", nullable: false),
                    SstMaxRsdPercent = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    SstMinResolution = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    SstMaxTailingFactor = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    SstMinTheoreticalPlates = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    SstMinRetentionFactor = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    SstMinSignalToNoise = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    SstMinPeakToValley = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcMethodAnalytes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcMethodAnalytes_HplcMethods_HplcMethodId",
                        column: x => x.HplcMethodId,
                        principalTable: "HplcMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HplcMethodAnalytes_MaterialMasterEntries_StandardEntryId",
                        column: x => x.StandardEntryId,
                        principalTable: "MaterialMasterEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HplcMethodGradientSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcMethodId = table.Column<int>(type: "integer", nullable: false),
                    TimeMin = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    PercentA = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    PercentB = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    PercentC = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    PercentD = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcMethodGradientSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcMethodGradientSteps_HplcMethods_HplcMethodId",
                        column: x => x.HplcMethodId,
                        principalTable: "HplcMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HplcMethodMobilePhases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcMethodId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    SolutionMasterId = table.Column<int>(type: "integer", nullable: false),
                    RatioPercent = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcMethodMobilePhases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcMethodMobilePhases_HplcMethods_HplcMethodId",
                        column: x => x.HplcMethodId,
                        principalTable: "HplcMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HplcMethodMobilePhases_SolutionMasters_SolutionMasterId",
                        column: x => x.SolutionMasterId,
                        principalTable: "SolutionMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_HplcMethodId",
                table: "TestDefinitions",
                column: "HplcMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_HplcMethodAnalyteId",
                table: "Specifications",
                column: "HplcMethodAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethodAnalytes_HplcMethodId_Name",
                table: "HplcMethodAnalytes",
                columns: new[] { "HplcMethodId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethodAnalytes_StandardEntryId",
                table: "HplcMethodAnalytes",
                column: "StandardEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethodGradientSteps_HplcMethodId",
                table: "HplcMethodGradientSteps",
                column: "HplcMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethodMobilePhases_HplcMethodId_Channel",
                table: "HplcMethodMobilePhases",
                columns: new[] { "HplcMethodId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethodMobilePhases_SolutionMasterId",
                table: "HplcMethodMobilePhases",
                column: "SolutionMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethods_DiluentSolutionId",
                table: "HplcMethods",
                column: "DiluentSolutionId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethods_SectionId_Abbreviation",
                table: "HplcMethods",
                columns: new[] { "SectionId", "Abbreviation" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Specifications_HplcMethodAnalytes_HplcMethodAnalyteId",
                table: "Specifications",
                column: "HplcMethodAnalyteId",
                principalTable: "HplcMethodAnalytes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_HplcMethods_HplcMethodId",
                table: "TestDefinitions",
                column: "HplcMethodId",
                principalTable: "HplcMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Specifications_HplcMethodAnalytes_HplcMethodAnalyteId",
                table: "Specifications");

            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_HplcMethods_HplcMethodId",
                table: "TestDefinitions");

            migrationBuilder.DropTable(
                name: "HplcMethodAnalytes");

            migrationBuilder.DropTable(
                name: "HplcMethodGradientSteps");

            migrationBuilder.DropTable(
                name: "HplcMethodMobilePhases");

            migrationBuilder.DropTable(
                name: "HplcMethods");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_HplcMethodId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Specifications_HplcMethodAnalyteId",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "HplcMethodId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "HplcMethodAnalyteId",
                table: "Specifications");
        }
    }
}
