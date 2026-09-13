using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <summary>
    /// Data-only conversion decided 2026-09-13 ("rejected means rejected not voided").
    /// Before the Voided status existed, voiding a sample stored it as a rejection:
    /// Samples.Status = Rejected (5), ApprovalDecision = Reject (1), ApprovedByUserId /
    /// ApprovedAt = the voiding user and time, CertificateRemarks = "Voided: {reason}",
    /// and every TestOrder Status = Rejected (5). Those samples are identified by that
    /// exact combination and converted to what a void records today:
    ///
    /// a) Non-superseded TestOrders -> Status Voided (7). Superseded orders are left as
    ///    they are - their earlier status was overwritten and cannot be recovered, and
    ///    a superseded test reads as superseded regardless.
    /// b) ResultRecords -> SampleStatus Voided (8), so reports stop counting them as rejected.
    /// c) Samples -> Status Voided (8); ApprovalDecision / ApprovedByUserId / ApprovedAt
    ///    cleared - a void is not an approval decision. The voiding user and time remain
    ///    in the AuditLog row captured when the sample was voided.
    ///
    /// Deliberately, no audit rows, timeline events or signatures are synthesized.
    /// Samples and their dependents are matched on the sample state, so the sample
    /// update runs last.
    /// </summary>
    public partial class ConvertLegacyVoidedSamples : Migration
    {
        private const string LegacyVoidedSampleIds = @"
SELECT ""Id"" FROM ""Samples""
WHERE ""Status"" = 5
  AND ""ApprovalDecision"" = 1
  AND ""CertificateRemarks"" LIKE 'Voided: %'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // a) Their current tests: Rejected (5) -> Voided (7).
            migrationBuilder.Sql($@"
UPDATE ""TestOrders""
SET ""Status"" = 7
WHERE ""IsSuperseded"" = FALSE
  AND ""Status"" = 5
  AND ""SampleId"" IN ({LegacyVoidedSampleIds});
");

            // b) Their result projection rows: SampleStatus -> Voided (8).
            migrationBuilder.Sql($@"
UPDATE ""ResultRecords""
SET ""SampleStatus"" = 8
WHERE ""SampleId"" IN ({LegacyVoidedSampleIds});
");

            // c) The samples themselves: Rejected (5) -> Voided (8), no approval decision.
            migrationBuilder.Sql(@"
UPDATE ""Samples""
SET ""Status"" = 8,
    ""ApprovalDecision"" = NULL,
    ""ApprovedByUserId"" = NULL,
    ""ApprovedAt"" = NULL
WHERE ""Status"" = 5
  AND ""ApprovalDecision"" = 1
  AND ""CertificateRemarks"" LIKE 'Voided: %';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op: a data-only corrective conversion. The cleared approver
            // and approval time are not stored anywhere the migration could restore them
            // from, and converted rows cannot be told apart from samples voided later by
            // the application, so the conversion cannot be reversed.
        }
    }
}
