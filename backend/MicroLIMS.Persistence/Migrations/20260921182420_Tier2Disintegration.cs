using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Tier2Disintegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisintegrationMaxStage1Failures",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisintegrationMinPassTotal",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisintegrationStage1Units",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisintegrationStage2Units",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisintegrationMaxStage1Failures",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "DisintegrationMinPassTotal",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "DisintegrationStage1Units",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "DisintegrationStage2Units",
                table: "TestDefinitions");
        }
    }
}
