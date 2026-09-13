using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <summary>
    /// Status-only data backfill decided 2026-09-13:
    /// Synchronizes TestOrder Status and CurrentStep with sample-level review/approval state.
    /// Deliberately, no WorkflowHistory or audit rows are synthesized for backfilled rows.
    /// 
    /// Backfill rules:
    /// a) Non-superseded TestOrders with CurrentStep = Ready (3) and Status IN (Pending (0), ResultEntered (2))
    ///    whose parent Sample.Status = UnderApproval (3) are backfilled to Status = Reviewed (3) and CurrentStep = Reviewed (4).
    /// b) Non-superseded TestOrders with CurrentStep = Ready (3) and Status = Pending (0)
    ///    whose parent Sample.Status = UnderReview (2) are backfilled to Status = ResultEntered (2) (pathogen-session samples).
    /// 
    /// Exclusion:
    /// TestOrders whose CurrentStep is not Ready (e.g. tests returned to incubation or still running on an UnderApproval sample)
    /// are strictly excluded and left untouched for manual review.
    /// </summary>
    public partial class SyncTestOrderStatusWithSampleReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // a) Non-superseded TestOrders in Ready step on UnderApproval samples:
            //    Update Status -> Reviewed (3), CurrentStep -> Reviewed (4).
            migrationBuilder.Sql(@"
UPDATE ""TestOrders""
SET ""Status"" = 3, ""CurrentStep"" = 4
WHERE ""IsSuperseded"" = FALSE
  AND ""CurrentStep"" = 3
  AND ""Status"" IN (0, 2)
  AND ""SampleId"" IN (SELECT ""Id"" FROM ""Samples"" WHERE ""Status"" = 3);
");

            // b) Non-superseded TestOrders in Ready step on UnderReview samples with Status = Pending (pathogen-session samples):
            //    Update Status -> ResultEntered (2). CurrentStep remains Ready (3).
            migrationBuilder.Sql(@"
UPDATE ""TestOrders""
SET ""Status"" = 2
WHERE ""IsSuperseded"" = FALSE
  AND ""CurrentStep"" = 3
  AND ""Status"" = 0
  AND ""SampleId"" IN (SELECT ""Id"" FROM ""Samples"" WHERE ""Status"" = 2);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op: This migration is a data-only corrective fix.
            // Rows updated by this migration cannot be definitively distinguished from rows
            // legitimately updated by live application workflows, so the backfill cannot be reversed.
        }
    }
}
