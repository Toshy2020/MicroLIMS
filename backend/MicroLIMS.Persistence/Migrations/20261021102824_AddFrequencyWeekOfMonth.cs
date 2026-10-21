using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequencyWeekOfMonth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FrequencyWeekOfMonth",
                table: "WaterSamplingPoints",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyWeekOfMonth",
                table: "Rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyWeekOfMonth",
                table: "Departments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrequencyWeekOfMonth",
                table: "WaterSamplingPoints");

            migrationBuilder.DropColumn(
                name: "FrequencyWeekOfMonth",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "FrequencyWeekOfMonth",
                table: "Departments");
        }
    }
}
