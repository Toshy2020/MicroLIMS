using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMediaEvaluationIncubationCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfills Incubations rows created by MediaEvaluationEngine
            // before it started stamping CompletedAt/CompletedByUserId.
            // Each MediaEvaluationChallenge owns exactly one Incubation
            // (Challenge.IncubationId), and ReadAt/ReadByUserId already
            // record when/by whom its result was entered - copy that onto
            // the Incubation row so already-evaluated challenges stop
            // showing as perpetually "Active" in equipment traceability.
            migrationBuilder.Sql(@"
                UPDATE ""Incubations"" i
                SET ""CompletedAt"" = c.""ReadAt"",
                    ""CompletedByUserId"" = c.""ReadByUserId""
                FROM ""MediaEvaluationChallenges"" c
                WHERE c.""IncubationId"" = i.""Id""
                  AND c.""ReadAt"" IS NOT NULL
                  AND i.""CompletedAt"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill only - not reversible (original null state is not recoverable).
        }
    }
}
