using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCaseInsensitiveDocumentCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    v_clashes text;
                BEGIN
                    SELECT string_agg(grp, '; ' ORDER BY grp)
                    INTO v_clashes
                    FROM (
                        SELECT string_agg("CompanyDocumentCode", ' / ' ORDER BY "Id") AS grp
                        FROM "DocumentMasters"
                        GROUP BY lower(trim("CompanyDocumentCode"))
                        HAVING count(*) > 1
                    ) sub;

                    IF v_clashes IS NOT NULL THEN
                        RAISE EXCEPTION 'Document codes must be unique ignoring case. Rename these before installing: %', v_clashes;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_DocumentMasters_CompanyDocumentCode",
                table: "DocumentMasters");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_DocumentMasters_CompanyDocumentCode_Lower\" ON \"DocumentMasters\" (lower(\"CompanyDocumentCode\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_DocumentMasters_CompanyDocumentCode_Lower\";");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_CompanyDocumentCode",
                table: "DocumentMasters",
                column: "CompanyDocumentCode",
                unique: true,
                filter: "\"RecordStatus\" = 1");
        }
    }
}
