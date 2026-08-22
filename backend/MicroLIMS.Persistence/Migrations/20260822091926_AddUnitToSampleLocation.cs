using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitToSampleLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "SampleLocations",
                type: "text",
                nullable: true);

            // Backfill: existing quantitative SampleLocation rows (CalculatedResult
            // already set) were all written before Unit existed, and
            // ResultProjectionService previously hardcoded "CFU/Plate" for every one
            // of them regardless of sampling method - wrong for Water (CFU/mL),
            // SurfaceAirSample/Swab (CFU/25 sq.cm), and Rinse (CFU/mL). Correct both
            // SampleLocations and the ResultRecords projected from them, using the
            // same mapping as TestWorkflowEngine.DeriveBatchLocationUnit (QC
            // Microbiology Supervisor sign-off, 2026-08-22). Qualitative
            // (Detected/Absent) locations have CalculatedResult = null and are left
            // with Unit = null, unchanged.
            migrationBuilder.Sql(@"
                UPDATE ""SampleLocations"" sl
                SET ""Unit"" = CASE
                    WHEN rtc.""TestType"" = 'PassiveAirSample' THEN 'CFU/plate/4 hours'
                    WHEN rtc.""TestType"" = 'SurfaceAirSample' THEN 'CFU/25 sq.cm'
                    WHEN mpc.""TestType"" = 'Swab' THEN 'CFU/25 sq.cm'
                    WHEN mpc.""TestType"" = 'Rinse' THEN 'CFU/mL'
                    WHEN sl.""WaterSamplingPointId"" IS NOT NULL THEN 'CFU/mL'
                    ELSE NULL
                END
                FROM ""SampleLocations"" base
                LEFT JOIN ""RoomTestConfigurations"" rtc ON base.""RoomTestConfigurationId"" = rtc.""Id""
                LEFT JOIN ""MachinePartConfigurations"" mpc ON base.""MachinePartConfigurationId"" = mpc.""Id""
                WHERE sl.""Id"" = base.""Id"" AND sl.""CalculatedResult"" IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""ResultRecords"" rr
                SET ""Unit"" = sl.""Unit""
                FROM ""SampleLocations"" sl
                WHERE rr.""SourceTable"" = 'SampleLocation' AND rr.""SourceId"" = sl.""Id"" AND sl.""CalculatedResult"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "SampleLocations");
        }
    }
}
