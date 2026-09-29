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
            // Pathogen-session quantitative results once stored the literal
            // "Conform" without comparing the count against any limit (fixed
            // in PathogenSessionService, but the rows it wrote remain). No
            // limit judgement was ever made for them, so route them to a
            // reviewer rather than guess one, and record the change in the
            // audit trail as a system action.
            migrationBuilder.Sql("""
                WITH corrected AS (
                    UPDATE "SampleLocations" SET "Status" = 'RequiresReview'
                    WHERE "Status" = 'Conform'
                    RETURNING "Id", "SampleId", "TestOrderId"
                )
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "SampleId", "TestOrderId", "SampleReferenceNumber",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                SELECT 'SampleLocation', c."Id"::text, 'Update', '{"Status":"Conform"}', '{"Status":"RequiresReview"}', 0, now(),
                    c."SampleId", c."TestOrderId", s."ReferenceNumber",
                    2, 'Migration 20260928212117_ValidateResultStatusColumns', 'ResultStatusCorrected', 9,
                    'Legacy status "Conform" was stored without comparing the result against its limits and is not a recognised result status. Set to RequiresReview so a reviewer judges the result.'
                FROM corrected c
                LEFT JOIN "Samples" s ON s."Id" = c."SampleId";
                """);

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
