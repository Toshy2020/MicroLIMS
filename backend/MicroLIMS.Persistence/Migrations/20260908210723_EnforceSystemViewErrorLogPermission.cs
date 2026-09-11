using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSystemViewErrorLogPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ErrorMonitoringController now checks this code via
            // [Authorize(Policy=...)], so the Roles screen should stop
            // showing it as declared-but-unenforced.
            migrationBuilder.Sql(@"
                UPDATE ""Permissions"" SET ""IsEnforced"" = TRUE
                WHERE ""Code"" = 'System.ViewErrorLog';");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Permissions"" SET ""IsEnforced"" = FALSE
                WHERE ""Code"" = 'System.ViewErrorLog';");


        }
    }
}
