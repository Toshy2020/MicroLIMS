using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowStepResultUniqueStepName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStepResults_TestOrderId",
                table: "WorkflowStepResults");

            // Clears the duplicates the pre-index race already left behind,
            // or the unique index below cannot be built. Keeps the earliest
            // row of each (TestOrderId, StepName) - that is the one the
            // workflow went on to use - and only ever deletes a later copy
            // that carries no confirmatory media selection or plate
            // observation of its own, so no recorded result is removed. A
            // duplicate that does carry one is left in place deliberately:
            // the index creation then fails loudly rather than this migration
            // quietly discarding laboratory data.
            migrationBuilder.Sql(@"
                DELETE FROM ""WorkflowStepResults"" w
                WHERE w.""Id"" > (
                        SELECT MIN(k.""Id"")
                        FROM ""WorkflowStepResults"" k
                        WHERE k.""TestOrderId"" = w.""TestOrderId""
                          AND k.""StepName"" = w.""StepName""
                    )
                  AND NOT EXISTS (
                        SELECT 1 FROM ""ConfirmatoryMediaSelections"" s
                        WHERE s.""WorkflowStepResultId"" = w.""Id""
                    )
                  AND NOT EXISTS (
                        SELECT 1 FROM ""ConfirmatoryPlateObservations"" o
                        WHERE o.""WorkflowStepResultId"" = w.""Id""
                    );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepResults_TestOrderId_StepName",
                table: "WorkflowStepResults",
                columns: new[] { "TestOrderId", "StepName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStepResults_TestOrderId_StepName",
                table: "WorkflowStepResults");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepResults_TestOrderId",
                table: "WorkflowStepResults",
                column: "TestOrderId");
        }
    }
}
