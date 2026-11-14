using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDataExportPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Permissions"" SET ""IsEnforced"" = TRUE WHERE ""Code"" = 'Data.Export';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Permissions"" SET ""IsEnforced"" = FALSE WHERE ""Code"" = 'Data.Export';");
        }
    }
}
