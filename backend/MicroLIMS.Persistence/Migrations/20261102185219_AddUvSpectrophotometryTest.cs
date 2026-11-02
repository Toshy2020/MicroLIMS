using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUvSpectrophotometryTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UvCalculationMode",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UvSpecificAbsorbance",
                table: "TestDefinitions",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UvStandardVolumeMl",
                table: "TestDefinitions",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UvTotalVolumeMl",
                table: "TestDefinitions",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UvCalculationMode",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "UvSpecificAbsorbance",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "UvStandardVolumeMl",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "UvTotalVolumeMl",
                table: "TestDefinitions");
        }
    }
}
