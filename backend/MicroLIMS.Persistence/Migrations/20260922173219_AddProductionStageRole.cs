using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <summary>
    /// FP Standard-Comparison Assay foundation (Slice 1, 2026-09-22): gives the existing
    /// Section-Head-managed ProductionStage lookup a fixed, code-meaningful Role
    /// (ProductionStageRole: Other=0, Bulk=1, InProcess=2, Finished=3, Stability=4), and links
    /// Sample to it via a nullable FK alongside the existing free-text ProductionStage string
    /// (which is left exactly as-is - the historical snapshot, never removed).
    ///
    /// Role backfill for the six rows seeded by AddSamplersAndProductionStages (by exact name):
    ///   'B' -> Bulk, 'IP' -> InProcess, 'F.P' -> Finished, 'S.F' -> Finished (user, 2026-09-22: semi-finished is tested like finished product),
    ///   'Coating' -> InProcess, 'Compressed Tab' -> InProcess.
    /// Any other existing row (a lab-added/renamed stage this migration does not recognize)
    /// keeps the column default, Other - never guessed.
    ///
    /// A new 'Stability' row (Role = Stability) is inserted only if no row with that exact
    /// name already exists, so re-running this logic is idempotent.
    ///
    /// Sample.ProductionStageId backfill: matches each sample's stored ProductionStage string
    /// against ProductionStages.Name, trimmed and case-insensitive. A sample whose stored name
    /// does not match any current row (a stage since renamed/deleted, or no stage recorded)
    /// is left ProductionStageId = null rather than guessed.
    /// </summary>
    public partial class AddProductionStageRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductionStageId",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "ProductionStages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_ProductionStageId",
                table: "Samples",
                column: "ProductionStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_ProductionStages_ProductionStageId",
                table: "Samples",
                column: "ProductionStageId",
                principalTable: "ProductionStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Role backfill for the seeded rows, by exact name. Everything else keeps
            // the column default (Other, 0) - no guessing for an unrecognized name.
            migrationBuilder.Sql(@"
                UPDATE ""ProductionStages"" SET ""Role"" = 1 WHERE ""Name"" = 'B';
                UPDATE ""ProductionStages"" SET ""Role"" = 2 WHERE ""Name"" = 'IP';
                UPDATE ""ProductionStages"" SET ""Role"" = 3 WHERE ""Name"" = 'F.P';
                UPDATE ""ProductionStages"" SET ""Role"" = 3 WHERE ""Name"" = 'S.F';
                UPDATE ""ProductionStages"" SET ""Role"" = 2 WHERE ""Name"" = 'Coating';
                UPDATE ""ProductionStages"" SET ""Role"" = 2 WHERE ""Name"" = 'Compressed Tab';

                -- The lab renames these rows: LIMSV2 had 'Bulk' (was 'B') and
                -- 'Coated' (was 'Coating') by 2026-09-22. Map both spellings so a
                -- database that renamed them still gets the right role.
                UPDATE ""ProductionStages"" SET ""Role"" = 1 WHERE ""Name"" = 'Bulk';
                UPDATE ""ProductionStages"" SET ""Role"" = 2 WHERE ""Name"" = 'Coated';
            ");

            // New Stability row, Role = Stability (4) - only if no row with that exact
            // name already exists (idempotent; recon confirmed no such row exists today).
            migrationBuilder.Sql(@"
                INSERT INTO ""ProductionStages"" (""Name"", ""IsActive"", ""Role"")
                SELECT 'Stability', true, 4
                WHERE NOT EXISTS (SELECT 1 FROM ""ProductionStages"" WHERE ""Name"" = 'Stability');
            ");

            // Sample.ProductionStageId backfill: match the stored ProductionStage string
            // (trimmed, case-insensitive) against the current ProductionStages.Name rows.
            // A sample whose stored name matches nothing current is left null.
            migrationBuilder.Sql(@"
                UPDATE ""Samples"" s
                SET ""ProductionStageId"" = ps.""Id""
                FROM ""ProductionStages"" ps
                WHERE s.""ProductionStage"" IS NOT NULL
                  AND LOWER(TRIM(s.""ProductionStage"")) = LOWER(ps.""Name"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Samples_ProductionStages_ProductionStageId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_ProductionStageId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ProductionStageId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ProductionStages");
        }
    }
}
