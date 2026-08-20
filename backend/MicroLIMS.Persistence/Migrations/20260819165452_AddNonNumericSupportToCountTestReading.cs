using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNonNumericSupportToCountTestReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CalculatedResult",
                table: "CountTestReadings",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "Average",
                table: "CountTestReadings",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<bool>(
                name: "HasNonNumericReading",
                table: "CountTestReadings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NonNumericValue",
                table: "CountTestReadings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresReview",
                table: "CountTestReadings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasNonNumericReading",
                table: "CountTestReadings");

            migrationBuilder.DropColumn(
                name: "NonNumericValue",
                table: "CountTestReadings");

            migrationBuilder.DropColumn(
                name: "RequiresReview",
                table: "CountTestReadings");

            migrationBuilder.AlterColumn<decimal>(
                name: "CalculatedResult",
                table: "CountTestReadings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Average",
                table: "CountTestReadings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }
    }
}
