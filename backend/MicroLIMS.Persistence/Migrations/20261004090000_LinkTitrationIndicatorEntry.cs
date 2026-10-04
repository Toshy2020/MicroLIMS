using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkTitrationIndicatorEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TitrationIndicatorEntryId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_TitrationIndicatorEntryId",
                table: "TestDefinitions",
                column: "TitrationIndicatorEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_MaterialMasterEntries_TitrationIndicatorEnt~",
                table: "TestDefinitions",
                column: "TitrationIndicatorEntryId",
                principalTable: "MaterialMasterEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Link existing free-text indicators to the same-section Indicator
            // entry (Category 1) of the same name. Unmatched tests keep their
            // text and must pick an indicator entry on their next edit.
            migrationBuilder.Sql(@"
UPDATE ""TestDefinitions"" t
SET ""TitrationIndicatorEntryId"" = (
    SELECT m.""Id"" FROM ""MaterialMasterEntries"" m
    WHERE m.""Category"" = 1 AND m.""SectionId"" = t.""SectionId""
      AND lower(trim(m.""Name"")) = lower(trim(t.""TitrationIndicator""))
    ORDER BY m.""IsActive"" DESC, m.""Id""
    LIMIT 1)
WHERE t.""TitrationIndicator"" IS NOT NULL AND t.""TitrationIndicatorEntryId"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_MaterialMasterEntries_TitrationIndicatorEnt~",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_TitrationIndicatorEntryId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "TitrationIndicatorEntryId",
                table: "TestDefinitions");
        }
    }
}
