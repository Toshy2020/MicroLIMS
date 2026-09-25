using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSuitabilityStandardResponsesAndWeighInWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ComputedRsdPercent",
                table: "SystemSuitabilityRuns",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MoisturePercent",
                table: "SystemSuitabilityRuns",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StandardWeighInDeviationPercent",
                table: "SystemSuitabilityRuns",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StandardWeighInOutOfWindow",
                table: "SystemSuitabilityRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalWeightMg",
                table: "SystemSuitabilityRuns",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighInJustification",
                table: "SystemSuitabilityRuns",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ComputedRsdPercent",
                table: "SystemSuitabilityRunAnalytes",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MoisturePercent",
                table: "SystemSuitabilityRunAnalytes",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StandardWeighInDeviationPercent",
                table: "SystemSuitabilityRunAnalytes",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StandardWeighInOutOfWindow",
                table: "SystemSuitabilityRunAnalytes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalWeightMg",
                table: "SystemSuitabilityRunAnalytes",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighInJustification",
                table: "SystemSuitabilityRunAnalytes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemSuitabilityStandardResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SystemSuitabilityRunAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Response = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuitabilityStandardResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuitabilityStandardResponses_SystemSuitabilityRunAnal~",
                        column: x => x.SystemSuitabilityRunAnalyteId,
                        principalTable: "SystemSuitabilityRunAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityStandardResponses_SystemSuitabilityRunAna~1",
                table: "SystemSuitabilityStandardResponses",
                columns: new[] { "SystemSuitabilityRunAnalyteId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuitabilityStandardResponses_SystemSuitabilityRunAnal~",
                table: "SystemSuitabilityStandardResponses",
                column: "SystemSuitabilityRunAnalyteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSuitabilityStandardResponses");

            migrationBuilder.DropColumn(
                name: "ComputedRsdPercent",
                table: "SystemSuitabilityRuns");

            migrationBuilder.DropColumn(
                name: "MoisturePercent",
                table: "SystemSuitabilityRuns");

            migrationBuilder.DropColumn(
                name: "StandardWeighInDeviationPercent",
                table: "SystemSuitabilityRuns");

            migrationBuilder.DropColumn(
                name: "StandardWeighInOutOfWindow",
                table: "SystemSuitabilityRuns");

            migrationBuilder.DropColumn(
                name: "TheoreticalWeightMg",
                table: "SystemSuitabilityRuns");

            migrationBuilder.DropColumn(
                name: "WeighInJustification",
                table: "SystemSuitabilityRuns");

            migrationBuilder.DropColumn(
                name: "ComputedRsdPercent",
                table: "SystemSuitabilityRunAnalytes");

            migrationBuilder.DropColumn(
                name: "MoisturePercent",
                table: "SystemSuitabilityRunAnalytes");

            migrationBuilder.DropColumn(
                name: "StandardWeighInDeviationPercent",
                table: "SystemSuitabilityRunAnalytes");

            migrationBuilder.DropColumn(
                name: "StandardWeighInOutOfWindow",
                table: "SystemSuitabilityRunAnalytes");

            migrationBuilder.DropColumn(
                name: "TheoreticalWeightMg",
                table: "SystemSuitabilityRunAnalytes");

            migrationBuilder.DropColumn(
                name: "WeighInJustification",
                table: "SystemSuitabilityRunAnalytes");
        }
    }
}
