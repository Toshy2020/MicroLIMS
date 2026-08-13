using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestWorkflowTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TestWorkflowSteps (and the WorkflowType/StepName columns below) originally
            // shipped in two earlier migrations - AddTestWorkflowTemplates and
            // AddCountTestReadingStepName - that were deleted when this migration was
            // squashed together with follow-on schema drift. Their CreateTable/AddColumn
            // calls never made it into the squash, so a from-empty `database update`
            // could never provision TestWorkflowSteps at all. Restored here, ahead of
            // this migration's own AddColumn calls, so the table exists before those
            // columns are added to it and before AddPathogenWorkflowRefactor alters it.
            migrationBuilder.AddColumn<int>(
                name: "WorkflowType",
                table: "TestDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StepName",
                table: "CountTestReadings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TestWorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    StepName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MediaTypeId = table.Column<int>(type: "integer", nullable: false),
                    IncubationMinHours = table.Column<int>(type: "integer", nullable: false),
                    IncubationMaxHours = table.Column<int>(type: "integer", nullable: false),
                    TemperatureMin = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TemperatureMax = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IsFinalStep = table.Column<bool>(type: "boolean", nullable: false),
                    IsDualPlate = table.Column<bool>(type: "boolean", nullable: false)
                    // Plate1DefaultLabel, Plate2DefaultLabel, and StepResultType are NOT
                    // listed here - they are added immediately below by this migration's
                    // own AddColumn calls, so including them here would create the same
                    // column twice.
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestWorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestWorkflowSteps_MediaTypes_MediaTypeId",
                        column: x => x.MediaTypeId,
                        principalTable: "MediaTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestWorkflowSteps_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowSteps_MediaTypeId",
                table: "TestWorkflowSteps",
                column: "MediaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowSteps_TestDefinitionId_StepOrder",
                table: "TestWorkflowSteps",
                columns: new[] { "TestDefinitionId", "StepOrder" },
                unique: true);

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

            migrationBuilder.DropTable(
                name: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "StepName",
                table: "CountTestReadings");

            migrationBuilder.DropColumn(
                name: "WorkflowType",
                table: "TestDefinitions");
        }
    }
}
