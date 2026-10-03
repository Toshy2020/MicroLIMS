using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTitrationTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureC",
                table: "TitrantStandardizations",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitrantSolutionMasterId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TitrationBlankRequired",
                table: "TestDefinitions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitrationCalculation",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitrationEndpoint",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TitrationEquivalencyFactor",
                table: "TestDefinitions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitrationExcessSolutionMasterId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TitrationExcessVolumeMl",
                table: "TestDefinitions",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TitrationExpansionCoefficient",
                table: "TestDefinitions",
                type: "numeric(8,6)",
                precision: 8,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitrationIndicator",
                table: "TestDefinitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TitrationMaxRsdPercent",
                table: "TestDefinitions",
                type: "numeric(6,3)",
                precision: 6,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitrationMode",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TitrationNonAqueous",
                table: "TestDefinitions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitrationStandardEntryId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TitrationTempCorrection",
                table: "TestDefinitions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitrationType",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_TitrantSolutionMasterId",
                table: "TestDefinitions",
                column: "TitrantSolutionMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_TitrationExcessSolutionMasterId",
                table: "TestDefinitions",
                column: "TitrationExcessSolutionMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_TitrationStandardEntryId",
                table: "TestDefinitions",
                column: "TitrationStandardEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_MaterialMasterEntries_TitrationStandardEntr~",
                table: "TestDefinitions",
                column: "TitrationStandardEntryId",
                principalTable: "MaterialMasterEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_SolutionMasters_TitrantSolutionMasterId",
                table: "TestDefinitions",
                column: "TitrantSolutionMasterId",
                principalTable: "SolutionMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_SolutionMasters_TitrationExcessSolutionMast~",
                table: "TestDefinitions",
                column: "TitrationExcessSolutionMasterId",
                principalTable: "SolutionMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_MaterialMasterEntries_TitrationStandardEntr~",
                table: "TestDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_SolutionMasters_TitrantSolutionMasterId",
                table: "TestDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_SolutionMasters_TitrationExcessSolutionMast~",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_TitrantSolutionMasterId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_TitrationExcessSolutionMasterId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_TitrationStandardEntryId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TemperatureC",
                table: "TitrantStandardizations");

            migrationBuilder.DropColumn(
                name: "TitrantSolutionMasterId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationBlankRequired",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationCalculation",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationEndpoint",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationEquivalencyFactor",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationExcessSolutionMasterId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationExcessVolumeMl",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationExpansionCoefficient",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationIndicator",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationMaxRsdPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationMode",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationNonAqueous",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationStandardEntryId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationTempCorrection",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationType",
                table: "TestDefinitions");
        }
    }
}
