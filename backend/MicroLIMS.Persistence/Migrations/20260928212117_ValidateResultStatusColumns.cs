using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    // The four status columns are now read into enums. The column type does
    // not change (the enum is stored as its name), but a row holding any
    // other text would throw every time it is loaded. Refuse to migrate
    // until such rows are corrected, and name them.
    public partial class ValidateResultStatusColumns : Migration
    {
        private const string ResultStatuses =
            "'WithinLimits','AlertLimitExceeded','ActionLimitExceeded','OutOfSpecification'," +
            "'LimitsNotConfigured','RequiresReview','Absent','Detected','PendingConfirmation'," +
            "'NotDetected','NextStageRequired','Inconclusive'";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DO $$
                DECLARE bad text;
                BEGIN
                    SELECT string_agg(format('%s.%s = %L (%s rows)', tbl, col, val, n), E'\n' ORDER BY tbl, col, val)
                    INTO bad
                    FROM (
                        SELECT 'CountTestReadings' AS tbl, 'Status' AS col, "Status" AS val, count(*) AS n
                        FROM "CountTestReadings" WHERE "Status" NOT IN ({ResultStatuses}) GROUP BY "Status"
                        UNION ALL
                        SELECT 'ParameterResults', 'ComparisonStatus', "ComparisonStatus", count(*)
                        FROM "ParameterResults" WHERE "ComparisonStatus" NOT IN ({ResultStatuses}) GROUP BY "ComparisonStatus"
                        UNION ALL
                        SELECT 'SampleLocations', 'Status', "Status", count(*)
                        FROM "SampleLocations" WHERE "Status" NOT IN ({ResultStatuses}) GROUP BY "Status"
                        UNION ALL
                        SELECT 'RevisionChangeItems', 'Status', "Status", count(*)
                        FROM "RevisionChangeItems" WHERE "Status" NOT IN ('Draft','Addressed','Deferred') GROUP BY "Status"
                    ) invalid;

                    IF bad IS NOT NULL THEN
                        RAISE EXCEPTION E'Status columns hold values the application no longer accepts. Correct these rows, then migrate again:\n%', bad;
                    END IF;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
