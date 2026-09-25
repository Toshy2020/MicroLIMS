using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Tier2Dissolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DissolutionS1Offset",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DissolutionS2MinOffset",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DissolutionS3MaxBelowS2Min",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DissolutionS3MinOffset",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DissolutionS1Offset",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "DissolutionS2MinOffset",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "DissolutionS3MaxBelowS2Min",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "DissolutionS3MinOffset",
                table: "TestDefinitions");
        }
    }
}
