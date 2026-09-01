using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOrphanedXldTsiIncubations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Closes Incubation rows left with CompletedAt = NULL even
            // though their TestOrder has already been Approved (found: 3
            // "XLD,TSI" selective-plating windows superseded by the next
            // chained step without ever being explicitly closed). No
            // CompletedByUserId is recorded anywhere for these - the
            // analyst who advanced the chain was never captured, so it's
            // deliberately left NULL here rather than guessed. CompletedAt
            // is set to the StartedAt of the next incubation step on the
            // same TestOrder, since that's the moment this window was
            // physically superseded.
            migrationBuilder.Sql(@"
                UPDATE ""Incubations"" i
                SET ""CompletedAt"" = (
                    SELECT i2.""StartedAt""
                    FROM ""Incubations"" i2
                    WHERE i2.""TestOrderId"" = i.""TestOrderId""
                      AND i2.""StartedAt"" > i.""StartedAt""
                    ORDER BY i2.""StartedAt"" ASC
                    LIMIT 1
                )
                WHERE i.""CompletedAt"" IS NULL
                  AND i.""StepName"" != 'MediaEvaluation'
                  AND EXISTS (
                      SELECT 1 FROM ""TestOrders"" t
                      WHERE t.""Id"" = i.""TestOrderId"" AND t.""Status"" = 4
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill only - not reversible (original null state is not recoverable).
        }
    }
}
