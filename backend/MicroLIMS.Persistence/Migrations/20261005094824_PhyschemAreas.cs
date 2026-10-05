using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhyschemAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhyschemArea",
                table: "UserOrgMemberships",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhyschemArea",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            // Backfill from existing specifications: RM/PM item categories (1, 2) only -> 1, both kinds -> 2, otherwise FP (0).
            migrationBuilder.Sql(@"
UPDATE ""TestDefinitions"" td SET ""PhyschemArea"" = CASE
  WHEN EXISTS (SELECT 1 FROM ""Specifications"" s JOIN ""Items"" i ON i.""Id"" = s.""ItemId"" WHERE s.""TestCode"" = td.""Code"" AND i.""Category"" IN (1,2))
   AND EXISTS (SELECT 1 FROM ""Specifications"" s JOIN ""Items"" i ON i.""Id"" = s.""ItemId"" WHERE s.""TestCode"" = td.""Code"" AND i.""Category"" NOT IN (1,2)) THEN 2
  WHEN EXISTS (SELECT 1 FROM ""Specifications"" s JOIN ""Items"" i ON i.""Id"" = s.""ItemId"" WHERE s.""TestCode"" = td.""Code"" AND i.""Category"" IN (1,2)) THEN 1
  ELSE 0 END
WHERE td.""SectionId"" IN (SELECT ""Id"" FROM ""DocumentSections"" WHERE ""Code"" = 'FP');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhyschemArea",
                table: "UserOrgMemberships");

            migrationBuilder.DropColumn(
                name: "PhyschemArea",
                table: "TestDefinitions");
        }
    }
}
