using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBiochemicalOrganismDetected : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BiochemicalOrganismDetected",
                table: "WorkflowStepResults",
                type: "boolean",
                nullable: true);

            // Backfill: every historical biochemical submission finalized
            // through the old hardcoded-Detected path in SubmitBiochemicalAsync
            // (a real submission, not a skip) genuinely meant Detected under
            // that logic - this preserves what those already-finalized rows
            // actually recorded, it does not reinterpret them.
            //
            // One live row is a documented exception, not a mechanical case:
            // TestOrderId 211's "Identification Kit" result text reads "The
            // biochemical result confirmed the absence of E. coli" - the
            // exact incident that motivated this migration. The old hardcode
            // reported it as Detected; the text says the opposite. Excluded
            // from the generic backfill and corrected explicitly rather than
            // silently perpetuating the false positive into the new field.
            // Generic backfill only - a real historical biochemical
            // submission that went through the old hardcoded-Detected path
            // genuinely meant Detected under that logic, so this preserves
            // what those rows already recorded rather than reinterpreting
            // them. Order-specific business-data corrections (e.g. the
            // TestOrderId 211 incident that motivated this migration) do
            // NOT belong in a replayable schema migration - a migration
            // with a hardcoded TestOrderId would corrupt an unrelated
            // order with that same ID on any other database it ever runs
            // against, and any such correction needs its own reviewed,
            // properly-attributed audit trail entry, not one fabricated
            // by an unattended migration. That correction is handled
            // separately, out of band, with an accurate audit record.
            migrationBuilder.Sql(
                "UPDATE \"WorkflowStepResults\" SET \"BiochemicalOrganismDetected\" = true " +
                "WHERE \"BiochemicalResultText\" IS NOT NULL AND \"SkippedBiochemical\" = false " +
                "AND \"BiochemicalOrganismDetected\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BiochemicalOrganismDetected",
                table: "WorkflowStepResults");
        }
    }
}
