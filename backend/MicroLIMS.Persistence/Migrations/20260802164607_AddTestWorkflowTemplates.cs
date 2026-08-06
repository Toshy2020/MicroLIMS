using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestWorkflowTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkflowType",
                table: "TestDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "WorkflowType",
                table: "TestDefinitions");
        }
    }
}
