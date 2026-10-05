using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropDeadSpecificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SampleMatrix",
                table: "TestAnalyses");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "SampleMatrix",
                table: "Specifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SampleMatrix",
                table: "TestAnalyses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Specifications",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1.0m);

            migrationBuilder.AddColumn<int>(
                name: "SampleMatrix",
                table: "Specifications",
                type: "integer",
                nullable: true);
        }
    }
}
