using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncubationTransferStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresIncubationTransfer",
                table: "TestWorkflowSteps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentIncubationId",
                table: "Incubations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StageNumber",
                table: "Incubations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "StartedByUserId",
                table: "Incubations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TestWorkflowStepIncubationStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestWorkflowStepId = table.Column<int>(type: "integer", nullable: false),
                    StageNumber = table.Column<int>(type: "integer", nullable: false),
                    TempMin = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TempMax = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IncubationMinHours = table.Column<int>(type: "integer", nullable: false),
                    IncubationMaxHours = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestWorkflowStepIncubationStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestWorkflowStepIncubationStages_TestWorkflowSteps_TestWork~",
                        column: x => x.TestWorkflowStepId,
                        principalTable: "TestWorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incubations_ParentIncubationId",
                table: "Incubations",
                column: "ParentIncubationId");

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepIncubationStages_TestWorkflowStepId_StageNu~",
                table: "TestWorkflowStepIncubationStages",
                columns: new[] { "TestWorkflowStepId", "StageNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incubations_Incubations_ParentIncubationId",
                table: "Incubations",
                column: "ParentIncubationId",
                principalTable: "Incubations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incubations_Incubations_ParentIncubationId",
                table: "Incubations");

            migrationBuilder.DropTable(
                name: "TestWorkflowStepIncubationStages");

            migrationBuilder.DropIndex(
                name: "IX_Incubations_ParentIncubationId",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "RequiresIncubationTransfer",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ParentIncubationId",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "StageNumber",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "StartedByUserId",
                table: "Incubations");
        }
    }
}
