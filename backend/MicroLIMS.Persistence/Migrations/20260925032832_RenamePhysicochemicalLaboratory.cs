using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePhysicochemicalLaboratory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FP is now the Physicochemical Laboratory; the section Code stays "FP".
            migrationBuilder.Sql(@"UPDATE ""DocumentSections"" SET ""Name"" = 'Physicochemical Laboratory' WHERE ""Code"" = 'FP';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""DocumentSections"" SET ""Name"" = 'Finished Product Laboratory' WHERE ""Code"" = 'FP';");
        }
    }
}
