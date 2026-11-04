using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSecurityAuditPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The security-events and login-history endpoints now check System.ViewSecurityAudit
            // (they used Audit.View), so the Roles screen should stop showing it as legacy-only.
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'System.ViewSecurityAudit';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = FALSE WHERE "Code" = 'System.ViewSecurityAudit';""");
        }
    }
}
