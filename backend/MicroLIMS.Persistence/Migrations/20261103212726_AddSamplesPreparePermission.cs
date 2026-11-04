using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplesPreparePermission : Migration
    {
        private const string Process = "Migration 20261103212726_AddSamplesPreparePermission";

        // Data only. Sample preparation writes used to need just a login; they now need samples.prepare.
        // DbSeeder covers a fresh database.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'samples.prepare', 'Prepare a sample for testing: record, confirm or batch-confirm a sample preparation.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'samples.prepare');");
            migrationBuilder.Sql(@"UPDATE ""Permissions"" SET ""IsEnforced"" = TRUE WHERE ""Code"" = 'samples.prepare';");

            // Preparation was open to every signed-in user, so every role keeps it except QC Manager roles
            // (approval-only), the built-in System Administrator and any role that manages users or roles (segregation of duties), which excludes
            // the built-in System Administrator.
            migrationBuilder.Sql(RoleGrantSql.Grant(@"ro.""Type"" <> 4 AND NOT (ro.""Type"" = 0 AND ro.""IsSystemRole"")",
                new[] { "samples.prepare" }, Process, "Sample preparation needs its own permission"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(new[] { "samples.prepare" }, Process + " (Down)", "Sample preparation permission rolled back (migration Down)"));
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'samples.prepare';");
        }
    }
}
