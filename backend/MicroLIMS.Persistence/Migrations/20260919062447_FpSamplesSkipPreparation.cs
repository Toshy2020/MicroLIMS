using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FpSamplesSkipPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Finished Product (FP section) tests have no preparation stage.
            // Open samples received before this rule whose active tests are
            // all FP tests, and that were never prepared, become Ready
            // (PreparationStatus 0 = NeedsPreparation, 1 = Ready).
            migrationBuilder.Sql(@"
UPDATE ""Samples"" s
SET ""PreparationStatus"" = 1
WHERE s.""PreparationStatus"" = 0
  AND NOT EXISTS (SELECT 1 FROM ""SamplePreparations"" p WHERE p.""SampleId"" = s.""Id"")
  AND EXISTS (SELECT 1 FROM ""TestOrders"" t WHERE t.""SampleId"" = s.""Id"" AND NOT t.""IsSuperseded"")
  AND NOT EXISTS (
      SELECT 1 FROM ""TestOrders"" t
      JOIN ""DocumentSections"" ds ON ds.""Id"" = t.""SectionId""
      WHERE t.""SampleId"" = s.""Id"" AND NOT t.""IsSuperseded"" AND ds.""Code"" <> 'FP');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only: samples moved to Ready are not put back in the queue.
        }
    }
}
