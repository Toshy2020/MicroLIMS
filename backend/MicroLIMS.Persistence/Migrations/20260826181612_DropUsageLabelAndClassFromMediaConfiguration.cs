using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropUsageLabelAndClassFromMediaConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaConfigurations_Name_UsageLabel",
                table: "MediaConfigurations");

            migrationBuilder.DropColumn(
                name: "Class",
                table: "MediaConfigurations");

            migrationBuilder.DropColumn(
                name: "UsageLabel",
                table: "MediaConfigurations");

            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurations_Name_IncubationMinHours_IncubationMaxHo~",
                table: "MediaConfigurations",
                columns: new[] { "Name", "IncubationMinHours", "IncubationMaxHours", "TemperatureMin", "TemperatureMax" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaConfigurations_Name_IncubationMinHours_IncubationMaxHo~",
                table: "MediaConfigurations");

            migrationBuilder.AddColumn<int>(
                name: "Class",
                table: "MediaConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UsageLabel",
                table: "MediaConfigurations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurations_Name_UsageLabel",
                table: "MediaConfigurations",
                columns: new[] { "Name", "UsageLabel" },
                unique: true);
        }
    }
}
