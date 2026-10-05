using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrimaryStandardType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only: both enums are stored as int. 3 = MaterialMasterCategory.PrimaryStandard,
            // 13 = MaterialType.PrimaryStandard, 0 = Reagent, 6 = Chemical, SolutionMasters.Type 2 = Titrant.
            migrationBuilder.Sql(@"
UPDATE ""MaterialMasterEntries"" e SET ""Category"" = 3
WHERE e.""Category"" = 0 AND EXISTS (SELECT 1 FROM ""SolutionMasters"" sm WHERE sm.""Type"" = 2 AND sm.""StandardEntryId"" = e.""Id"");
UPDATE ""Materials"" m SET ""MaterialType"" = 13
WHERE m.""MaterialType"" = 6 AND m.""MaterialMasterEntryId"" IN (SELECT ""Id"" FROM ""MaterialMasterEntries"" WHERE ""Category"" = 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""Materials"" SET ""MaterialType"" = 6, ""Purity"" = NULL WHERE ""MaterialType"" = 13;
UPDATE ""MaterialMasterEntries"" SET ""Category"" = 0 WHERE ""Category"" = 3;");
        }
    }
}
