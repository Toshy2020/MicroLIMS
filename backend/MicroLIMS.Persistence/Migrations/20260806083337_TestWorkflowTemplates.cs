using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestWorkflowTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Plate1DefaultLabel",
                table: "TestWorkflowSteps",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plate2DefaultLabel",
                table: "TestWorkflowSteps",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepResultType",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MediaId",
                table: "PathogenObservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlateLabel",
                table: "PathogenObservations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Plate2MediaId",
                table: "Incubations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PathogenObservations_MediaId",
                table: "PathogenObservations",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Incubations_Plate2MediaId",
                table: "Incubations",
                column: "Plate2MediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incubations_Media_Plate2MediaId",
                table: "Incubations",
                column: "Plate2MediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PathogenObservations_Media_MediaId",
                table: "PathogenObservations",
                column: "MediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incubations_Media_Plate2MediaId",
                table: "Incubations");

            migrationBuilder.DropForeignKey(
                name: "FK_PathogenObservations_Media_MediaId",
                table: "PathogenObservations");

            migrationBuilder.DropIndex(
                name: "IX_PathogenObservations_MediaId",
                table: "PathogenObservations");

            migrationBuilder.DropIndex(
                name: "IX_Incubations_Plate2MediaId",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "Plate1DefaultLabel",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "Plate2DefaultLabel",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "StepResultType",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "PathogenObservations");

            migrationBuilder.DropColumn(
                name: "PlateLabel",
                table: "PathogenObservations");

            migrationBuilder.DropColumn(
                name: "Plate2MediaId",
                table: "Incubations");
        }
    }
}
