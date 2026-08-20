using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhenotypicTestTypeToTestWorkflowStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_SampleId",
                table: "SamplePreparations");

            migrationBuilder.AlterColumn<int>(
                name: "MediaTypeId",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "PhenotypicTestType",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_SampleId",
                table: "SamplePreparations",
                column: "SampleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_SampleId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "PhenotypicTestType",
                table: "TestWorkflowSteps");

            migrationBuilder.AlterColumn<int>(
                name: "MediaTypeId",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_SampleId",
                table: "SamplePreparations",
                column: "SampleId");
        }
    }
}
