using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public class OosPermissionsMigrationPostgresTests
{
    private const string PreviousMigration = "20261120123423_AddAnalystCompetency";
    private const string View = "Oos.View";
    private const string Contribute = "Oos.Contribute";
    private const string RequestDeviation = "Oos.RequestDeviation";
    private static readonly string[] Codes = [Contribute, RequestDeviation, View];

    [PostgresFact]
    public Task Upgrade_grants_and_audits_exactly_the_expected_permissions() =>
        VerifyUpgradeAsync(includeQa: true);

    [PostgresFact]
    public Task Upgrade_without_Qa_grants_no_deviation_request_permission() =>
        VerifyUpgradeAsync(includeQa: false);

    [PostgresFact]
    public async Task Fresh_seed_and_upgrade_have_the_same_Oos_permissions()
    {
        await ScratchPostgresDatabase.RunAsync(async freshConnection =>
        {
            await using (var seedDb = ScratchPostgresDatabase.NewContext(freshConnection))
            {
                await seedDb.Database.MigrateAsync();
                DbSeeder.Seed(seedDb);
            }

            await ScratchPostgresDatabase.RunAsync(async upgradeConnection =>
            {
                await ArrangeUpgradeAsync(upgradeConnection, includeQa: true);
                await using (var migrateDb = ScratchPostgresDatabase.NewContext(upgradeConnection))
                    await migrateDb.Database.MigrateAsync();

                await using var fresh = ScratchPostgresDatabase.NewContext(freshConnection);
                await using var upgraded = ScratchPostgresDatabase.NewContext(upgradeConnection);
                var migratedCatalog = await CatalogAsync(upgraded);
                Assert.Equal(Codes, migratedCatalog.Select(permission => permission.Code));
                Assert.Equal(migratedCatalog, await CatalogAsync(fresh));
                var freshRoles = (await RolesAsync(fresh)).Where(role => role.IsSystemRole).ToArray();
                var upgradedRoles = (await RolesAsync(upgraded)).Where(role => role.IsSystemRole).ToArray();
                Assert.Equal(upgradedRoles.Select(role => role.Type), freshRoles.Select(role => role.Type));
                foreach (var (freshRole, upgradedRole) in freshRoles.Zip(upgradedRoles))
                    Assert.Equal(upgradedRole.Codes.Intersect(Codes), freshRole.Codes.Intersect(Codes));
            });
        });
    }

    private static Task VerifyUpgradeAsync(bool includeQa) =>
        ScratchPostgresDatabase.RunAsync(async connection =>
        {
            await ArrangeUpgradeAsync(connection, includeQa);

            await using var beforeDb = ScratchPostgresDatabase.NewContext(connection);
            var before = await RolesAsync(beforeDb);
            var lastAuditId = await beforeDb.AuditLogs.AsNoTracking().MaxAsync(log => (int?)log.Id) ?? 0;

            await using (var migrateDb = ScratchPostgresDatabase.NewContext(connection))
                await migrateDb.Database.MigrateAsync("20261120131800_AddOosInvestigationPermissions");

            await using var afterDb = ScratchPostgresDatabase.NewContext(connection);
            var catalog = await CatalogAsync(afterDb);
            Assert.Equal(Codes, catalog.Select(permission => permission.Code));
            Assert.All(catalog, permission => Assert.True(permission.IsEnforced));

            var after = await RolesAsync(afterDb);
            Assert.Equal(before.Select(role => role.Id), after.Select(role => role.Id));
            var audits = await afterDb.AuditLogs.AsNoTracking()
                .Where(log => log.Id > lastAuditId && log.EntityName == "Role").ToListAsync();
            Assert.Equal(before.Count(role => ExpectedGrants(role).Length > 0), audits.Count);
            foreach (var (original, updated) in before.Zip(after))
            {
                var added = ExpectedGrants(original);
                var expected = Sort(original.Codes.Concat(added));
                Assert.Equal(expected, updated.Codes);
                Assert.Equal(original.PermissionVersion + (added.Length == 0 ? 0 : 1), updated.PermissionVersion);

                if (added.Length == 0)
                    continue;

                var audit = Assert.Single(audits, log => log.EntityId == original.Id.ToString());
                Assert.Equal("ROLE_PERMISSIONS_UPDATED", audit.Action);
                Assert.Equal("RolePermissionsUpdated", audit.ActionCode);
                Assert.Equal(AuditActionCategory.Security, audit.ActionCategory);
                Assert.Equal(ActorType.System, audit.ActorType);
                Assert.Equal(0, audit.UserId);
                Assert.Contains("AddOosInvestigationPermissions", audit.SystemProcessName);
                Assert.False(string.IsNullOrWhiteSpace(audit.Reason));
                Assert.Equal(DateTimeKind.Utc, audit.Timestamp.Kind);
                using var previous = JsonDocument.Parse(Assert.IsType<string>(audit.PreviousValue));
                using var current = JsonDocument.Parse(Assert.IsType<string>(audit.NewValue));
                Assert.Equal(original.Codes, ReadCodes(previous, "PermissionCodes"));
                Assert.Equal(expected, ReadCodes(current, "PermissionCodes"));
                Assert.Equal(added, ReadCodes(current, "Granted"));
                Assert.Empty(ReadCodes(current, "Revoked"));
            }
        });

    private static async Task ArrangeUpgradeAsync(string connection, bool includeQa)
    {
        await using var db = ScratchPostgresDatabase.NewContext(connection);
        await db.Database.MigrateAsync(PreviousMigration);
        db.Roles.AddRange(Enum.GetValues<RoleType>()
            .Where(type => includeQa || type != RoleType.Qa)
            .Select(type => new Role { Type = type, Name = type.ToString(), IsSystemRole = true }));
        db.Roles.Add(new Role { Type = RoleType.Analyst, Name = "Custom analyst" });
        await db.SaveChangesAsync();

        var analyst = await db.Roles.SingleAsync(role => role.Type == RoleType.Analyst && role.IsSystemRole);
        var existingPermission = await db.Permissions.SingleAsync(permission => permission.Code == "Competency.View");
        db.RolePermissions.Add(new RolePermission { RoleId = analyst.Id, PermissionId = existingPermission.Id });
        await db.SaveChangesAsync();
    }

    private static string[] ExpectedGrants(RoleState role) => (role.IsSystemRole, role.Type) switch
    {
        (true, RoleType.SectionHead or RoleType.Analyst) => [Contribute, View],
        (true, RoleType.Reviewer or RoleType.QcManager) => [View],
        (true, RoleType.Qa) => [RequestDeviation, View],
        _ => []
    };

    private static async Task<RoleState[]> RolesAsync(MicroLimsDbContext db)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(role => role.Type).ThenBy(role => role.Id)
            .Select(role => new { role.Id, role.Type, role.IsSystemRole, role.PermissionVersion }).ToListAsync();
        var grants = await db.RolePermissions.AsNoTracking()
            .Select(grant => new { grant.RoleId, Code = grant.Permission!.Code }).ToListAsync();
        var byRole = grants.ToLookup(grant => grant.RoleId, grant => grant.Code);
        return roles.Select(role => new RoleState(role.Id, role.Type, role.IsSystemRole,
            role.PermissionVersion, Sort(byRole[role.Id]))).ToArray();
    }

    private static async Task<PermissionState[]> CatalogAsync(MicroLimsDbContext db) =>
        (await db.Permissions.AsNoTracking().Where(permission => Codes.Contains(permission.Code))
            .Select(permission => new PermissionState(permission.Code, permission.Description, permission.IsEnforced))
            .ToListAsync()).OrderBy(permission => permission.Code, StringComparer.Ordinal).ToArray();

    private static string[] ReadCodes(JsonDocument json, string property) =>
        Sort(json.RootElement.GetProperty(property).EnumerateArray().Select(element => Assert.IsType<string>(element.GetString())));

    private static string[] Sort(IEnumerable<string> codes) => codes.Order(StringComparer.Ordinal).ToArray();

    private sealed record RoleState(int Id, RoleType Type, bool IsSystemRole, int PermissionVersion, string[] Codes);
    private sealed record PermissionState(string Code, string? Description, bool IsEnforced);
}
