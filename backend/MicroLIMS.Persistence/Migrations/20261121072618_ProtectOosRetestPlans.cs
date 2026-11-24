using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectOosRetestPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OosRetestPlanSamples_SampleId",
                table: "OosRetestPlanSamples");

            migrationBuilder.CreateIndex(
                name: "IX_OosRetestPlanSamples_SampleId",
                table: "OosRetestPlanSamples",
                column: "SampleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosRetestPlans_InvestigationId",
                table: "OosRetestPlans",
                column: "InvestigationId",
                unique: true,
                filter: "\"SignedAtUtc\" IS NULL");

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_oosretestplansamples_immutable
                BEFORE UPDATE OR DELETE ON "OosRetestPlanSamples"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_oosretestplansamples_no_truncate
                BEFORE TRUNCATE ON "OosRetestPlanSamples"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_oosretestplansamples_no_truncate ON "OosRetestPlanSamples";
                DROP TRIGGER IF EXISTS trg_oosretestplansamples_immutable ON "OosRetestPlanSamples";
                """);

            migrationBuilder.DropIndex(
                name: "IX_OosRetestPlanSamples_SampleId",
                table: "OosRetestPlanSamples");

            migrationBuilder.DropIndex(
                name: "IX_OosRetestPlans_InvestigationId",
                table: "OosRetestPlans");

            migrationBuilder.CreateIndex(
                name: "IX_OosRetestPlanSamples_SampleId",
                table: "OosRetestPlanSamples",
                column: "SampleId");
        }
    }
}
