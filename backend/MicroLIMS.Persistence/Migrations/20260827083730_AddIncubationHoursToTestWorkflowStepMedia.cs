using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncubationHoursToTestWorkflowStepMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IncubationMaxHours",
                table: "TestWorkflowStepMedias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IncubationMinHours",
                table: "TestWorkflowStepMedias",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncubationMaxHours",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropColumn(
                name: "IncubationMinHours",
                table: "TestWorkflowStepMedias");
        }
    }
}
