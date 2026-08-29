using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOosGroupCodes : Migration
    {
        // Data-only fixup for the previous migration: any OOS retest chain
        // (Sample.OriginSampleId links) created before OosGroupCode existed
        // has every row's OosGroupCode still NULL, so it's invisible to the
        // grouped OOS tracking page even though the retest samples
        // themselves are real. Walks every OriginSampleId chain back to its
        // root, mints one OOS{MM}{yy}{seq:D3} code per root that actually
        // has descendants (mirrors ReferenceNumberGenerator.GenerateOosCodeAsync's
        // format, sequenced by the root's own ReceivedAt month), and stamps
        // it onto the root and every descendant. Only touches rows that are
        // still NULL, so it can never clobber a code assigned by real usage
        // after this deploys.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                WITH RECURSIVE chain AS (
                    SELECT ""Id"" AS root_id, ""Id"" AS member_id
                    FROM ""Samples""
                    WHERE ""OriginSampleId"" IS NULL
                    UNION ALL
                    SELECT c.root_id, s.""Id""
                    FROM ""Samples"" s
                    JOIN chain c ON s.""OriginSampleId"" = c.member_id
                ),
                roots_with_descendants AS (
                    SELECT root_id
                    FROM chain
                    GROUP BY root_id
                    HAVING COUNT(*) > 1
                ),
                root_codes AS (
                    SELECT rwd.root_id,
                           'OOS' || to_char(s.""ReceivedAt"", 'MM') || to_char(s.""ReceivedAt"", 'YY') ||
                           lpad(
                               (ROW_NUMBER() OVER (
                                   PARTITION BY to_char(s.""ReceivedAt"", 'MM'), to_char(s.""ReceivedAt"", 'YY')
                                   ORDER BY s.""ReceivedAt"", s.""Id""
                               ))::text, 3, '0'
                           ) AS code
                    FROM roots_with_descendants rwd
                    JOIN ""Samples"" s ON s.""Id"" = rwd.root_id
                )
                UPDATE ""Samples"" tgt
                SET ""OosGroupCode"" = rc.code
                FROM chain c
                JOIN root_codes rc ON rc.root_id = c.root_id
                WHERE tgt.""Id"" = c.member_id
                  AND tgt.""OosGroupCode"" IS NULL;
            ");
        }

        // Not reversible: there's no way to tell a backfilled code apart
        // from one a real OOS decision assigned after this deployed.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
