using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Segregation of duties: whatever the migrations and the seeder do, the built-in System Administrator
// ends up with the system permissions only. A later migration that grants a laboratory code to
// RoleType.SystemAdministrator (as the KPI and plan migrations once did) fails here.
public class SystemAdministratorGrantsPostgresTests
{
    private const string InitialMigration = "20260729201340_InitialCreate";

    private static Task<List<string>> HeldByBuiltInAdministratorAsync(MicroLimsDbContext db) =>
        db.RolePermissions
            .Where(rp => rp.Role!.Type == RoleType.SystemAdministrator && rp.Role.IsSystemRole)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();

    // The first-start path: every migration, then the seeder.
    [PostgresFact]
    public async Task FreshDatabase_AfterAllMigrationsAndSeed_AdministratorHoldsExactlyTheSystemGrants()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            await db.Database.MigrateAsync();
            DbSeeder.Seed(db);

            var held = await HeldByBuiltInAdministratorAsync(db);

            Assert.Equal(
                PermissionConstants.SystemAdministratorGrants.OrderBy(c => c, StringComparer.Ordinal),
                held.OrderBy(c => c, StringComparer.Ordinal));
        });
    }

    // The upgrade path: an administrator role that existed from the first schema, then every later migration
    // (each of which may grant or revoke codes). Codes the seeder would have granted on day one are absent here,
    // so this checks that nothing outside the system grants survives, not that everything is present.
    [PostgresFact]
    public async Task UpgradedDatabase_AfterAllMigrations_AdministratorHoldsNoCodeOutsideTheSystemGrants()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            await db.Database.MigrateAsync(InitialMigration);
            await db.Database.ExecuteSqlRawAsync(@"INSERT INTO ""Roles"" (""Type"", ""Name"") VALUES (0, 'System Administrator');");
            await db.Database.MigrateAsync();

            var held = await HeldByBuiltInAdministratorAsync(db);

            var excess = held.Except(PermissionConstants.SystemAdministratorGrants).OrderBy(c => c, StringComparer.Ordinal).ToList();
            Assert.True(excess.Count == 0, $"System Administrator holds codes outside SystemAdministratorGrants: {string.Join(", ", excess)}");
            Assert.Contains(PermissionConstants.SectionsViewAll, held);
        });
    }
}
