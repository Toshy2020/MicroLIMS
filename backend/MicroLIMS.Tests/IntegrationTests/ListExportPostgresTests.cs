using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.ArchitectureTests;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ListExportPostgresTests
{
    private const string Table = "inventory.equipment";
    private static readonly DateTimeOffset FixedNow = new(2031, 5, 6, 7, 8, 9, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public ListExportPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<(int UserId, string FullName)> CreateUserAsync(params string[] codes)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        await using var arrangeDb = _fixture.CreateDbContext();
        var role = new Role { Type = RoleType.Reviewer, Name = "Export test " + tag, IsSystemRole = false, IsActive = true };
        arrangeDb.Roles.Add(role);
        await arrangeDb.SaveChangesAsync();

        var permissionIds = await arrangeDb.Permissions
            .Where(p => codes.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync();
        Assert.Equal(codes.Length, permissionIds.Count);
        foreach (var pid in permissionIds)
            arrangeDb.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });

        var fullName = "Export User " + tag;
        var user = new User
        {
            FullName = fullName,
            Username = "exp_" + tag,
            PasswordHash = "hashed_dummy",
            RoleId = role.Id,
            IsActive = true
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();
        return (user.Id, fullName);
    }

    private static string BuildFilterJson(int recordCount, string? status = "Active")
    {
        var ids = Enumerable.Range(1, recordCount).Select(i => $"EQ-{i:000}").ToArray();
        return JsonSerializer.Serialize(new
        {
            filters = new { status },
            columns = new[] { "code", "status" },
            recordIds = ids
        });
    }

    [PostgresFact]
    public async Task Log_WithPermission_WritesDataExportLog_WithUserFiltersRowCount()
    {
        var (userId, fullName) = await CreateUserAsync(PermissionConstants.DataExport);
        var filterJson = BuildFilterJson(3);
        var clock = new FixedTimeProvider(FixedNow);

        int exportId;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = new DataExportAuditService(actDb, clock);
            var log = await svc.LogListExportAsync(userId, Table, filterJson, 3);
            exportId = log.Id;
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var persisted = await assertDb.DataExportLogs.AsNoTracking()
                .SingleOrDefaultAsync(l => l.Id == exportId);
            Assert.NotNull(persisted);
            Assert.Equal(userId, persisted.UserId);
            Assert.Equal(fullName, persisted.UserName);
            Assert.Equal(FixedNow.UtcDateTime, persisted.ExportedAt);
            Assert.Equal($"ListCsv:{Table}", persisted.ExportType);
            Assert.Equal(3, persisted.RowCount);
            Assert.Equal(filterJson, persisted.FilterJson);
        }
    }

    [PostgresFact]
    public async Task Log_WithoutPermission_403()
    {
        var method = typeof(ExportsController).GetMethod(nameof(ExportsController.Log));
        Assert.NotNull(method);
        var authAttr = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .FirstOrDefault(a => a.Policy == PermissionConstants.DataExport);
        Assert.NotNull(authAttr);

        var allowed = AuthorizationMatrixTests.AllowedRoles<ExportsController>(nameof(ExportsController.Log));
        Assert.NotNull(allowed);
        Assert.DoesNotContain(RoleType.Analyst, allowed);
        Assert.DoesNotContain(RoleType.Qa, allowed);
        Assert.DoesNotContain(RoleType.SystemAdministrator, allowed);
        Assert.Contains(RoleType.SectionHead, allowed);
        Assert.Contains(RoleType.Reviewer, allowed);
        Assert.Contains(RoleType.QcManager, allowed);

        await using var db = _fixture.CreateDbContext();
        var perm = await db.Permissions.AsNoTracking().SingleAsync(p => p.Code == PermissionConstants.DataExport);
        Assert.True(perm.IsEnforced);
    }

    [PostgresFact]
    public async Task Log_Over5000_Rejected()
    {
        var (userId, _) = await CreateUserAsync(PermissionConstants.DataExport);
        var filterJson = BuildFilterJson(5001);

        int beforeCount;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            beforeCount = await arrangeDb.DataExportLogs.AsNoTracking().CountAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = new DataExportAuditService(actDb, new FixedTimeProvider(FixedNow));
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => svc.LogListExportAsync(userId, Table, filterJson, 5001));
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(beforeCount, await assertDb.DataExportLogs.AsNoTracking().CountAsync());
        }
    }

    [PostgresFact]
    public async Task Log_UnknownTableKey_Rejected()
    {
        var (userId, _) = await CreateUserAsync(PermissionConstants.DataExport);
        var filterJson = BuildFilterJson(1);

        int beforeCount;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            beforeCount = await arrangeDb.DataExportLogs.AsNoTracking().CountAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = new DataExportAuditService(actDb, new FixedTimeProvider(FixedNow));
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => svc.LogListExportAsync(userId, "nonexistent.table", filterJson, 1));
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(beforeCount, await assertDb.DataExportLogs.AsNoTracking().CountAsync());
        }
    }

    [PostgresFact]
    public async Task Log_RowCountMismatch_Rejected()
    {
        var (userId, _) = await CreateUserAsync(PermissionConstants.DataExport);
        var filterJson = BuildFilterJson(2);

        int beforeCount;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            beforeCount = await arrangeDb.DataExportLogs.AsNoTracking().CountAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = new DataExportAuditService(actDb, new FixedTimeProvider(FixedNow));
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => svc.LogListExportAsync(userId, Table, filterJson, 3));
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(beforeCount, await assertDb.DataExportLogs.AsNoTracking().CountAsync());
        }
    }
}
