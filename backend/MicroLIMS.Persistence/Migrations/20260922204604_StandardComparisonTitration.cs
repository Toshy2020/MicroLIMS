using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardComparisonTitration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResponseMode",
                table: "TestDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "ChromatographyColumnId",
                table: "SystemSuitabilityRuns",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "BlankTitreMl",
                table: "SystemSuitabilityRunAnalytes",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponseMode",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "BlankTitreMl",
                table: "SystemSuitabilityRunAnalytes");

            migrationBuilder.AlterColumn<int>(
                name: "ChromatographyColumnId",
                table: "SystemSuitabilityRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
