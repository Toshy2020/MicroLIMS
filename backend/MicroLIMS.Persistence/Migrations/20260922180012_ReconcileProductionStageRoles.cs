using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <summary>
    /// Reconciles production stage roles with the names the lab actually uses.
    /// AddProductionStageRole mapped the names seeded in 2026-09, but LIMSV2 had
    /// since been renamed: 'B' -> 'Bulk' and 'Coating' -> 'Coated', so both rows
    /// were left with Role = Other. Samples received before the rename still
    /// record the old name 'B' as their historical snapshot, so they never
    /// matched a lookup row and kept a null ProductionStageId.
    /// Roles are only ever set here where the row is still Other, so a role a
    /// section head has chosen by hand is never overwritten.
    /// </summary>
    public partial class ReconcileProductionStageRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""ProductionStages"" SET ""Role"" = 1
                WHERE ""Name"" = 'Bulk' AND ""Role"" = 0;

                UPDATE ""ProductionStages"" SET ""Role"" = 2
                WHERE ""Name"" = 'Coated' AND ""Role"" = 0;

                UPDATE ""Samples"" s SET ""ProductionStageId"" = p.""Id""
                FROM ""ProductionStages"" p
                WHERE s.""ProductionStageId"" IS NULL
                  AND btrim(s.""ProductionStage"") = 'B'
                  AND p.""Name"" = 'Bulk';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only reconciliation; nothing to reverse (matches the
            // convention of the other data migrations in this project).
        }
    }
}
