using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The seed grants nothing on an established database, so this migration is what gives existing
// Section Heads and administrators the permission.
[Collection("PostgresDatabaseCollection")]
public class KpiViewTeamPermissionMigrationPostgresTests
{
    private const string Code = PermissionConstants.KpiViewTeam;
    private readonly PostgresTestFixture _fixture;

    public KpiViewTeamPermissionMigrationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task RunAsync(MicroLimsDbContext db, IEnumerable<MigrationOperation> operations)
    {
        foreach (var op in operations.Cast<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
    }

    private static Task UpAsync(MicroLimsDbContext db) => RunAsync(db, new SeedKpiViewTeamPermission().UpOperations);
    private static Task DownAsync(MicroLimsDbContext db) => RunAsync(db, new SeedKpiViewTeamPermission().DownOperations);

    [PostgresFact]
    public async Task Up_GrantsAdminsAndSectionHeads_IsIdempotent_AndDownRemovesIt()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // The state of an existing database before this migration.
        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = {0});
              DELETE FROM ""Permissions"" WHERE ""Code"" = {0};", new object[] { Code });
        var customSectionHead = new Role { Name = "QC Lead (custom)", Type = RoleType.SectionHead, IsSystemRole = false, IsActive = true };
        var customAnalyst = new Role { Name = "QC Trainee (custom)", Type = RoleType.Analyst, IsSystemRole = false, IsActive = true };
        db.Roles.AddRange(customSectionHead, customAnalyst);
        await db.SaveChangesAsync();

        await UpAsync(db);

        var permission = await db.Permissions.SingleAsync(p => p.Code == Code);
        Assert.True(permission.IsEnforced);
        Assert.False(string.IsNullOrWhiteSpace(permission.Description));

        var roleIds = await db.RolePermissions.Where(rp => rp.Permission!.Code == Code).Select(rp => rp.RoleId).ToListAsync();
        var types = await db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Type).Distinct().OrderBy(t => t).ToListAsync();
        Assert.Equal(new[] { RoleType.SystemAdministrator, RoleType.SectionHead }, types);   // not Reviewer, not Analyst
        Assert.Contains(customSectionHead.Id, roleIds);
        Assert.DoesNotContain(customAnalyst.Id, roleIds);

        var grants = await db.RolePermissions.CountAsync();
        var permissions = await db.Permissions.CountAsync();
        await UpAsync(db);
        Assert.Equal(grants, await db.RolePermissions.CountAsync());
        Assert.Equal(permissions, await db.Permissions.CountAsync());

        await DownAsync(db);
        Assert.False(await db.Permissions.AnyAsync(p => p.Code == Code));
        Assert.False(await db.RolePermissions.AnyAsync(rp => rp.Permission!.Code == Code));

        await tx.RollbackAsync();
    }

    [PostgresFact]
    public async Task Up_EnforcesARowThatAlreadyExistedUnenforced()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // DbSeeder may have inserted the row first with the code's own flag; either way it ends up enforced.
        await db.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Permissions"" SET ""IsEnforced"" = FALSE WHERE ""Code"" = {0};", new object[] { Code });
        await UpAsync(db);

        Assert.True((await db.Permissions.SingleAsync(p => p.Code == Code)).IsEnforced);
        await tx.RollbackAsync();
    }
}
