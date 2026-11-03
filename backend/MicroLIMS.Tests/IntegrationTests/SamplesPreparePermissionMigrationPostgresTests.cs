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

// Every role that prepared samples before keeps doing so: system roles and custom roles built on
// one of the four working base types. Only QC Manager (approval-only) is left out.
[Collection("PostgresDatabaseCollection")]
public class SamplesPreparePermissionMigrationPostgresTests
{
    private const string Code = PermissionConstants.SamplesPrepare;
    private readonly PostgresTestFixture _fixture;

    public SamplesPreparePermissionMigrationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task RunAsync(MicroLimsDbContext db, IEnumerable<MigrationOperation> operations)
    {
        foreach (var op in operations.Cast<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
    }

    [PostgresFact]
    public async Task Up_GrantsEveryWorkingRoleIncludingCustom_NotQcManager_IsIdempotent_AndDownRemovesIt()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = {0});
              DELETE FROM ""Permissions"" WHERE ""Code"" = {0};", new object[] { Code });
        var customAnalyst = new Role { Name = "QC Trainee (custom)", Type = RoleType.Analyst, IsSystemRole = false, IsActive = true };
        var customReviewer = new Role { Name = "QA Checker (custom)", Type = RoleType.Reviewer, IsSystemRole = false, IsActive = true };
        var customQcManager = new Role { Name = "QC Deputy (custom)", Type = RoleType.QcManager, IsSystemRole = false, IsActive = true };
        db.Roles.AddRange(customAnalyst, customReviewer, customQcManager);
        await db.SaveChangesAsync();

        var up = new AddSamplesPreparePermission().UpOperations;
        await RunAsync(db, up);

        var permission = await db.Permissions.SingleAsync(p => p.Code == Code);
        Assert.True(permission.IsEnforced);
        Assert.False(string.IsNullOrWhiteSpace(permission.Description));

        var holders = await db.RolePermissions.Where(rp => rp.Permission!.Code == Code).Select(rp => rp.RoleId).ToListAsync();
        Assert.Equal(
            await db.Roles.Where(r => r.Type != RoleType.QcManager).Select(r => r.Id).OrderBy(i => i).ToListAsync(),
            holders.OrderBy(i => i).ToList());
        Assert.Contains(customAnalyst.Id, holders);
        Assert.Contains(customReviewer.Id, holders);
        Assert.DoesNotContain(customQcManager.Id, holders);

        var grants = await db.RolePermissions.CountAsync();
        var permissions = await db.Permissions.CountAsync();
        await RunAsync(db, up);
        Assert.Equal(grants, await db.RolePermissions.CountAsync());
        Assert.Equal(permissions, await db.Permissions.CountAsync());

        await RunAsync(db, new AddSamplesPreparePermission().DownOperations);
        Assert.False(await db.Permissions.AnyAsync(p => p.Code == Code));
        Assert.False(await db.RolePermissions.AnyAsync(rp => rp.Permission!.Code == Code));

        await tx.RollbackAsync();
    }
}
