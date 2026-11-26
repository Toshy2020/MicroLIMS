using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordSopCaptureStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "SystemSettings" ("Key", "Value", "ValueType", "Category", "IsGxpRelevant", "Description", "CurrentVersion")
                SELECT 'sopCapture.startDateUtc',
                       to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
                       2,
                       'System',
                       FALSE,
                       'UTC instant when SOP revision capture on test orders was installed; used for pre-capture display.',
                       1
                WHERE NOT EXISTS (
                    SELECT 1 FROM "SystemSettings" WHERE "Key" = 'sopCapture.startDateUtc'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "SystemSettings" WHERE "Key" = 'sopCapture.startDateUtc';
                """);
        }
    }
}
