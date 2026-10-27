using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using MicroLIMS.Tests.UnitTests;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Segregation of duties: whatever the migrations and the seeder do, the built-in System Administrator
// ends up with the system permissions only. A later migration that grants a laboratory code to
// RoleType.SystemAdministrator (as the KPI and plan migrations once did) fails here.
public class SystemAdministratorGrantsPostgresTests
{
    private const string InitialMigration = "20260729201340_InitialCreate";
    private const string PreviousMigration = "20261026162508_AddRolePermissionVersionAndSignatureRoleName";

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

            // Tokens issued with the old grants must stop validating (AccessTokenRevalidator): PermissionVersion starts at 1.
            var role = await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SystemAdministrator && r.IsSystemRole);
            Assert.True(role.PermissionVersion > 1, $"PermissionVersion was not incremented (is {role.PermissionVersion})");

            var audits = await db.AuditLogs.Where(a => a.Action == "ROLE_PERMISSIONS_UPDATED" && a.EntityId == role.Id.ToString()).ToListAsync();
            var restriction = Assert.Single(audits, a => a.Reason!.Contains("Segregation of duties"));
            Assert.Equal(nameof(Role), restriction.EntityName);
            var configure = Assert.Single(audits, a => a.Reason!.Contains("configure master data"));
            Assert.Contains("Sections.ConfigureAll", configure.NewValue);
            Assert.Contains(PermissionConstants.SectionsConfigureAll, held);
            Assert.Contains(PermissionConstants.SystemManageJobs, held);
            Assert.True(await db.Permissions.AnyAsync(p => p.Code == PermissionConstants.SystemManageJobs && p.IsEnforced));
            Assert.Contains(audits, a => a.Reason!.Contains("background job") && a.NewValue!.Contains("System.ManageJobs"));
            Assert.True(await db.Permissions.AnyAsync(p => p.Code == PermissionConstants.SectionsConfigureAll && p.IsEnforced));

            // System.ConfigureSettings: granted with its own audit row, enforced.
            Assert.Contains(PermissionConstants.SystemConfigureSettings, held);
            Assert.True(await db.Permissions.AnyAsync(p => p.Code == PermissionConstants.SystemConfigureSettings && p.IsEnforced));
            var settings = Assert.Single(audits, a => a.NewValue!.Contains(PermissionConstants.SystemConfigureSettings));
            Assert.Contains("settings", settings.Reason!, StringComparison.OrdinalIgnoreCase);
        });
    }

    // Rolling the migration back gives the administrator every removed code again and drops Sections.ViewAll.
    [PostgresFact]
    public async Task Down_RestoresTheRemovedCodes_AndInvalidatesTokensAgain()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            await db.Database.MigrateAsync(InitialMigration);
            await db.Database.ExecuteSqlRawAsync(@"INSERT INTO ""Roles"" (""Type"", ""Name"") VALUES (0, 'System Administrator');");
            await db.Database.MigrateAsync();
            var upVersion = (await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SystemAdministrator)).PermissionVersion;

            await db.Database.MigrateAsync(PreviousMigration);
            Assert.False(await db.Permissions.AnyAsync(p => p.Code == PermissionConstants.SectionsConfigureAll));

            var held = await HeldByBuiltInAdministratorAsync(db);
            Assert.DoesNotContain(PermissionConstants.SectionsViewAll, held);
            Assert.False(await db.Permissions.AnyAsync(p => p.Code == PermissionConstants.SectionsViewAll));
            // Only codes whose Permission row exists here (this schema-only database has none the seeder adds).
            var existing = await db.Permissions.Select(p => p.Code).ToListAsync();
            var expected = SystemAdministratorGrantsTests.GxpExecutionCodes.Intersect(existing).ToList();
            Assert.NotEmpty(expected);
            Assert.Empty(expected.Except(held));
            var role = await db.Roles.AsNoTracking().SingleAsync(r => r.Type == RoleType.SystemAdministrator);
            Assert.True(role.PermissionVersion > upVersion);
        });
    }
}
