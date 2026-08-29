using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestWorkflowStepPhenotypicTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestWorkflowStepPhenotypicTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestWorkflowStepId = table.Column<int>(type: "integer", nullable: false),
                    PhenotypicTestType = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestWorkflowStepPhenotypicTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestWorkflowStepPhenotypicTests_TestWorkflowSteps_TestWorkf~",
                        column: x => x.TestWorkflowStepId,
                        principalTable: "TestWorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepPhenotypicTests_TestWorkflowStepId_Phenotyp~",
                table: "TestWorkflowStepPhenotypicTests",
                columns: new[] { "TestWorkflowStepId", "PhenotypicTestType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestWorkflowStepPhenotypicTests");
        }
    }
}
