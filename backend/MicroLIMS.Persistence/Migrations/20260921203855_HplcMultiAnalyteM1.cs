using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HplcMultiAnalyteM1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HplcInjectionsPerPreparation",
                table: "TestDefinitions",
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
                name: "HplcPreparations",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "View",
                table: "TestAnalytes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "LoqMgPerL",
                table: "TestAnalytes",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<string>(
                name: "Element",
                table: "TestAnalytes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<decimal>(
                name: "SstMaxRsdPercent",
                table: "TestAnalytes",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SstMaxTailingFactor",
                table: "TestAnalytes",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SstMinResolution",
                table: "TestAnalytes",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SstMinTheoreticalPlates",
                table: "TestAnalytes",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemSuitabilityRunAnalytes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SystemSuitabilityRunId = table.Column<int>(type: "integer", nullable: false),
                    TestAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    AnalyteName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WavelengthNm = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    ReferenceStandardMaterialId = table.Column<int>(type: "integer", nullable: false),
                    StandardPurityPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    StandardWeightMg = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    StandardDilution = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    StandardMeanArea = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    RsdPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Resolution = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TailingFactor = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TheoreticalPlates = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSuitabilityRunAnalytes");

            migrationBuilder.DropColumn(
                name: "HplcInjectionsPerPreparation",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "HplcMaxPreparationRsdPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "HplcPreparations",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "SstMaxRsdPercent",
                table: "TestAnalytes");

            migrationBuilder.DropColumn(
                name: "SstMaxTailingFactor",
                table: "TestAnalytes");

            migrationBuilder.DropColumn(
                name: "SstMinResolution",
                table: "TestAnalytes");

            migrationBuilder.DropColumn(
                name: "SstMinTheoreticalPlates",
                table: "TestAnalytes");

            migrationBuilder.AlterColumn<int>(
                name: "View",
                table: "TestAnalytes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "LoqMgPerL",
                table: "TestAnalytes",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Element",
                table: "TestAnalytes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
