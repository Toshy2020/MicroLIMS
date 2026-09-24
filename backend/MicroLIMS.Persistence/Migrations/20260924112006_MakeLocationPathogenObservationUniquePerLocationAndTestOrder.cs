using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeLocationPathogenObservationUniquePerLocationAndTestOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse to proceed if any (location, test order) already has more
            // than one primary observation. These are GMP records, and some
            // carry confirmatory plate readings, so choosing which row to keep
            // is a documented data correction - not something a migration may
            // do silently. The migration runs in a transaction, so failing
            // here leaves the schema unchanged.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    duplicate_pairs integer;
                BEGIN
                    SELECT count(*) INTO duplicate_pairs
                    FROM (
                        SELECT 1
                        FROM "LocationPathogenObservations"
                        GROUP BY "SampleLocationId", "TestOrderId"
                        HAVING count(*) > 1
                    ) AS duplicates;

                    IF duplicate_pairs > 0 THEN
                        RAISE EXCEPTION
                            'Cannot make LocationPathogenObservations unique per (SampleLocationId, TestOrderId): % pair(s) have more than one row. Resolve them through a documented data correction, then re-run the migration. Find them with: SELECT "SampleLocationId", "TestOrderId", count(*) FROM "LocationPathogenObservations" GROUP BY 1, 2 HAVING count(*) > 1;',
                            duplicate_pairs;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_LocationPathogenObservations_SampleLocationId_TestOrderId",
                table: "LocationPathogenObservations");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPathogenObservations_SampleLocationId_TestOrderId",
                table: "LocationPathogenObservations",
                columns: new[] { "SampleLocationId", "TestOrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocationPathogenObservations_SampleLocationId_TestOrderId",
                table: "LocationPathogenObservations");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPathogenObservations_SampleLocationId_TestOrderId",
                table: "LocationPathogenObservations",
                columns: new[] { "SampleLocationId", "TestOrderId" });
        }
    }
}
