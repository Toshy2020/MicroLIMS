using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOosRetestPlanReportableRetest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReportableRetestNumber",
                table: "OosRetestPlans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportableSampleId",
                table: "OosRetestPlans",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosRetestPlans_ReportableSampleId",
                table: "OosRetestPlans",
                column: "ReportableSampleId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OosRetestPlans_ReportableRetest",
                table: "OosRetestPlans",
                sql: "(\"ReportableRetestNumber\" IS NULL OR (\"ReportableRetestNumber\" BETWEEN 1 AND \"RetestCount\")) AND (\"SignedAtUtc\" IS NULL OR (\"ReportableRetestNumber\" IS NULL) = (\"ReportableSampleId\" IS NULL))");

            migrationBuilder.Sql("""
                ALTER TABLE "OosRetestPlans"
                ADD CONSTRAINT "FK_OosRetestPlans_ReportableSample"
                FOREIGN KEY ("Id", "ReportableSampleId")
                REFERENCES "OosRetestPlanSamples" ("PlanId", "SampleId")
                ON DELETE RESTRICT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "OosRetestPlans"
                DROP CONSTRAINT "FK_OosRetestPlans_ReportableSample";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_OosRetestPlans_ReportableRetest",
                table: "OosRetestPlans");

            migrationBuilder.DropIndex(
                name: "IX_OosRetestPlans_ReportableSampleId",
                table: "OosRetestPlans");

            migrationBuilder.DropColumn(
                name: "ReportableRetestNumber",
                table: "OosRetestPlans");

            migrationBuilder.DropColumn(
                name: "ReportableSampleId",
                table: "OosRetestPlans");
        }
    }
}
