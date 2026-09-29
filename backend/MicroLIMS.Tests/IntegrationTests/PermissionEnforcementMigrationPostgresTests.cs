using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// EnforcePermissionBasedAuthorization on a database as it was before the
// switch: the ten new codes absent, and a custom role holding no grants -
// which until now still passed every role check for its base type.
[Collection("PostgresDatabaseCollection")]
public class PermissionEnforcementMigrationPostgresTests
{
    private static readonly string[] NewCodes =
    {
        PermissionConstants.DashboardsLabOverview, PermissionConstants.DashboardsReview, PermissionConstants.KpiView,
        PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease, PermissionConstants.OosManage,
        PermissionConstants.TestWorkflowSupervise, PermissionConstants.SamplesReceiveOwnLab,
        PermissionConstants.SamplesCorrect, PermissionConstants.SamplesAssignAnalyst
    };

    // Enforced codes that later migrations add and enforce themselves;
    // this migration never sees them.
    private static readonly string[] AddedByLaterMigrations =
    {
        PermissionConstants.SolutionsPrepare
    };

    private readonly PostgresTestFixture _fixture;

    public PermissionEnforcementMigrationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task RunUpAsync(MicroLIMS.Persistence.DbContext.MicroLimsDbContext db)
    {
        foreach (var op in new EnforcePermissionBasedAuthorization().UpOperations.Cast<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
    }

    [PostgresFact]
    public async Task Up_KeepsEveryRolesAccess_AndIsIdempotent()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // The state before the switch.
        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = ANY({0}));
              DELETE FROM ""Permissions"" WHERE ""Code"" = ANY({0});
              UPDATE ""Permissions"" SET ""IsEnforced"" = FALSE;", new object[] { NewCodes });
        var customAnalyst = new Role { Name = "QC Trainee", Type = RoleType.Analyst, IsSystemRole = false, IsActive = true };
        db.Roles.Add(customAnalyst);
        await db.SaveChangesAsync();

        await RunUpAsync(db);

        var granted = await db.RolePermissions.Where(rp => rp.RoleId == customAnalyst.Id)
            .Select(rp => rp.Permission!.Code).OrderBy(c => c).ToListAsync();
        // What an Analyst passed by role type and now needs as a grant...
        Assert.Equal(new[]
        {
            PermissionConstants.CryovialsManage, PermissionConstants.MaterialsManage,
            PermissionConstants.MediaPrepare, PermissionConstants.SamplesReceiveOwnLab
        }.OrderBy(c => c), granted);
        // ...but nothing that was already a real permission check, which the
        // administrator had been managing for this role all along.
        Assert.DoesNotContain(PermissionConstants.TestWorkflowExecute, granted);

        var sectionHead = await db.Roles.SingleAsync(r => r.IsSystemRole && r.Type == RoleType.SectionHead);
        var sectionHeadCodes = await db.RolePermissions.Where(rp => rp.RoleId == sectionHead.Id).Select(rp => rp.Permission!.Code).ToListAsync();
        Assert.Contains(PermissionConstants.OosManage, sectionHeadCodes);
        Assert.Contains(PermissionConstants.TestWorkflowSupervise, sectionHeadCodes);

        var enforced = await db.Permissions.Where(p => p.IsEnforced).Select(p => p.Code).ToListAsync();
        Assert.Equal(PermissionConstants.Enforced.Except(AddedByLaterMigrations).OrderBy(c => c), enforced.OrderBy(c => c));

        var grantsBefore = await db.RolePermissions.CountAsync();
        var permissionsBefore = await db.Permissions.CountAsync();
        await RunUpAsync(db);
        Assert.Equal(grantsBefore, await db.RolePermissions.CountAsync());
        Assert.Equal(permissionsBefore, await db.Permissions.CountAsync());

        await tx.RollbackAsync();
    }
}
