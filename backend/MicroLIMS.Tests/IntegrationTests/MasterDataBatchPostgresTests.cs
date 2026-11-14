using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.ArchitectureTests;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class MasterDataBatchPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public MasterDataBatchPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static WaterMasterDataService BuildWaterService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), new MasterDataBatchService(db));

    private async Task<(int HeadUserId, int WaterDeptId, string MicroTestCode, string OtherTestCode)> SeedWaterScopeAsync(string suffix)
    {
        await using var db = _fixture.CreateDbContext();

        var micro = await db.DocumentSections.FirstAsync(s => s.Code == LaboratoryCodes.Microbiology);
        var other = await db.DocumentSections.FirstOrDefaultAsync(s => s.Id != micro.Id && s.IsActive);
        if (other is null)
        {
            other = new DocumentSection { Code = $"OTH-{suffix}", Name = $"Other Lab {suffix}", IsActive = true };
            db.DocumentSections.Add(other);
            await db.SaveChangesAsync();
        }

        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.SectionHead);
        var user = new User
        {
            Username = $"md_batch_head_{suffix}",
            PasswordHash = "x",
            FullName = $"MD Batch Head {suffix}",
            Email = $"md_batch_{suffix}@test.local",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = micro.DepartmentId,
            SectionId = micro.Id
        });

        var microTestCode = $"MD-MICRO-{suffix}";
        var otherTestCode = $"MD-OTHER-{suffix}";
        db.TestDefinitions.AddRange(
            new TestDefinition { Code = microTestCode, DisplayName = $"Micro Test {suffix}", SectionId = micro.Id, IsActive = true },
            new TestDefinition { Code = otherTestCode, DisplayName = $"Other Test {suffix}", SectionId = other.Id, IsActive = true });

        var waterDept = new WaterDepartment { Name = $"PW-{suffix}", IsActive = true };
        db.WaterDepartments.Add(waterDept);

        await db.SaveChangesAsync();
        return (user.Id, waterDept.Id, microTestCode, otherTestCode);
    }

    [PostgresFact]
    public async Task AllValid_CreatesAll_AuditedLikeSingleCreate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, waterDeptId, microTestCode, _) = await SeedWaterScopeAsync(suffix);
        var fixedNow = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        var freq = new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Monday);

        WaterSamplingPointResponse singleResult;
        await using (var singleDb = _fixture.CreateDbContext(new FixedClock(fixedNow)))
        {
            singleDb.CurrentUserId = userId;
            var svc = BuildWaterService(singleDb);
            singleResult = await svc.CreateWaterSamplingPointAsync(
                userId,
                new CreateWaterSamplingPointRequest($"SP-S-{suffix}", "Single Room", "Weekly", new List<string> { microTestCode }, waterDeptId, freq));
        }

        IReadOnlyList<WaterSamplingPointResponse> batchResults;
        await using (var actDb = _fixture.CreateDbContext(new FixedClock(fixedNow)))
        {
            actDb.CurrentUserId = userId;
            var svc = BuildWaterService(actDb);
            batchResults = await svc.CreateWaterSamplingPointsBatchAsync(
                userId,
                new[]
                {
                    new CreateWaterSamplingPointRequest($"SP-B1-{suffix}", "Batch Room 1", "Weekly", new List<string> { microTestCode }, waterDeptId, freq),
                    new CreateWaterSamplingPointRequest($"SP-B2-{suffix}", "Batch Room 2", "Weekly", new List<string> { microTestCode }, waterDeptId, freq)
                });
        }

        Assert.Equal(2, batchResults.Count);

        await using var assertDb = _fixture.CreateDbContext();
        var batchIds = batchResults.Select(r => r.Id).ToList();
        var persisted = await assertDb.WaterSamplingPoints
            .AsNoTracking()
            .Where(p => batchIds.Contains(p.Id))
            .OrderBy(p => p.Id)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal($"SP-B1-{suffix}", persisted[0].Code);
        Assert.Equal($"SP-B2-{suffix}", persisted[1].Code);
        Assert.Equal(waterDeptId, persisted[0].WaterDepartmentId);
        Assert.Equal(FrequencyType.Weekly, persisted[0].FrequencyType);
        Assert.Equal(DayOfWeek.Monday, persisted[0].SamplingWeekday);

        var singleIdStr = singleResult.Id.ToString();
        var batchIdStrs = batchIds.Select(id => id.ToString()).ToList();

        var singleAudit = await assertDb.AuditLogs
            .AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(WaterSamplingPoint) && a.Action == "Create" && a.EntityId == singleIdStr);

        var batchAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(WaterSamplingPoint) && a.Action == "Create" && batchIdStrs.Contains(a.EntityId))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, batchAudits.Count);

        var singlePayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(singleAudit.NewValue!)!;
        for (var i = 0; i < batchAudits.Count; i++)
        {
            var batchAudit = batchAudits[i];
            var batchPoint = persisted[i];
            Assert.Equal(singleAudit.UserId, batchAudit.UserId);
            Assert.Equal(singleAudit.Action, batchAudit.Action);
            Assert.Equal(singleAudit.EntityName, batchAudit.EntityName);
            Assert.Equal(singleAudit.PreviousValue, batchAudit.PreviousValue);
            Assert.Equal(singleAudit.Timestamp, batchAudit.Timestamp);

            var batchPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(batchAudit.NewValue!)!;
            Assert.Equal(singlePayload.Keys.OrderBy(k => k), batchPayload.Keys.OrderBy(k => k));
            Assert.Equal(batchPoint.Id, batchPayload["Id"].GetInt32());
            Assert.Equal(batchPoint.Code, batchPayload["Code"].GetString());
            Assert.Equal(batchPoint.Location, batchPayload["Location"].GetString());
            Assert.Equal(batchPoint.WaterDepartmentId, batchPayload["WaterDepartmentId"].GetInt32());
            Assert.Equal(batchPoint.IsActive, batchPayload["IsActive"].GetBoolean());
        }
    }

    [PostgresFact]
    public async Task OneInvalid_NoneCreated_RowAndFieldReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, waterDeptId, microTestCode, _) = await SeedWaterScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildWaterService(actDb);
            var rows = new[]
            {
                new CreateWaterSamplingPointRequest($"SP-OK-{suffix}", "Valid Room", "Daily", new List<string> { microTestCode }, waterDeptId),
                new CreateWaterSamplingPointRequest("   ", "Missing Code", "Daily", new List<string> { microTestCode }, waterDeptId),
                new CreateWaterSamplingPointRequest(
                    $"SP-FREQ-{suffix}",
                    "Bad Frequency",
                    "Weekly",
                    new List<string> { microTestCode },
                    waterDeptId,
                    new FrequencyRuleDto(FrequencyType.Weekly, 1, SamplingWeekday: null))
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateWaterSamplingPointsBatchAsync(userId, rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 1 && e.Field == "code");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "frequency");
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.WaterSamplingPoints.AsNoTracking().AnyAsync(p => p.Code == $"SP-OK-{suffix}" || p.Code == $"SP-FREQ-{suffix}"));
    }

    [PostgresFact]
    public async Task DuplicateCodeInRequest_BothRowsReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, waterDeptId, microTestCode, _) = await SeedWaterScopeAsync(suffix);
        var dupCode = $"SP-DUP-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildWaterService(actDb);
            var rows = new[]
            {
                new CreateWaterSamplingPointRequest(dupCode, "Room 1", "Daily", new List<string> { microTestCode }, waterDeptId),
                new CreateWaterSamplingPointRequest($"SP-UNIQ-{suffix}", "Room 2", "Daily", new List<string> { microTestCode }, waterDeptId),
                new CreateWaterSamplingPointRequest(dupCode.ToLowerInvariant(), "Room 3", "Daily", new List<string> { microTestCode }, waterDeptId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateWaterSamplingPointsBatchAsync(userId, rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 0 && e.Field == "code");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "code");
            Assert.DoesNotContain(ex.FieldErrors, e => e.Row == 1);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.WaterSamplingPoints.AsNoTracking().AnyAsync(p => p.Code == dupCode || p.Code == $"SP-UNIQ-{suffix}"));
    }

    [PostgresFact]
    public async Task ExistingCode_Reported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, waterDeptId, microTestCode, _) = await SeedWaterScopeAsync(suffix);
        var existingCode = $"SP-EX-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.WaterSamplingPoints.Add(new WaterSamplingPoint
            {
                Code = existingCode,
                Location = "Already Here",
                TestingFrequency = "Daily",
                AssignedTestCodes = new List<string> { microTestCode },
                WaterDepartmentId = waterDeptId
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildWaterService(actDb);
            var rows = new[]
            {
                new CreateWaterSamplingPointRequest($"SP-FRESH-{suffix}", "Fresh Room", "Daily", new List<string> { microTestCode }, waterDeptId),
                new CreateWaterSamplingPointRequest(existingCode.ToLowerInvariant(), "Duplicate Existing", "Daily", new List<string> { microTestCode }, waterDeptId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateWaterSamplingPointsBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("code", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.WaterSamplingPoints.AsNoTracking().AnyAsync(p => p.Code == $"SP-FRESH-{suffix}"));
        Assert.Equal(1, await assertDb.WaterSamplingPoints.AsNoTracking().CountAsync(p => p.Code == existingCode));
    }

    [PostgresFact]
    public async Task _201Rows_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, waterDeptId, microTestCode, _) = await SeedWaterScopeAsync(suffix);
        var rows = Enumerable.Range(0, 201)
            .Select(i => new CreateWaterSamplingPointRequest($"SP-201-{i}-{suffix}", $"Loc {i}", "Daily", new List<string> { microTestCode }, waterDeptId))
            .ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildWaterService(actDb);
            await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateWaterSamplingPointsBatchAsync(userId, rows));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.WaterSamplingPoints.AsNoTracking().AnyAsync(p => p.Code.StartsWith("SP-201-") && p.Code.EndsWith(suffix)));
    }

    [PostgresFact]
    public async Task WithoutPermission_403()
    {
        var method = typeof(WaterMasterDataController).GetMethod(nameof(WaterMasterDataController.CreateWaterSamplingPointsBatch));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("water-sampling-points/batch", post!.Template);

        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.MasterDataManage, auth!.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());

        var allowed = AuthorizationMatrixTests.AllowedRoles<WaterMasterDataController>(nameof(WaterMasterDataController.CreateWaterSamplingPointsBatch));
        var singleAllowed = AuthorizationMatrixTests.AllowedRoles<WaterMasterDataController>(nameof(WaterMasterDataController.CreateWaterSamplingPoint));
        Assert.NotNull(allowed);
        Assert.Equal(singleAllowed, allowed);
        Assert.Contains(RoleType.SystemAdministrator, allowed);
        Assert.Contains(RoleType.SectionHead, allowed);
        Assert.DoesNotContain(RoleType.Analyst, allowed);
        Assert.DoesNotContain(RoleType.Reviewer, allowed);
        Assert.DoesNotContain(RoleType.Qa, allowed);

        await using var assertDb = _fixture.CreateDbContext();
        var perm = await assertDb.Permissions.AsNoTracking().SingleAsync(p => p.Code == PermissionConstants.MasterDataManage);
        Assert.True(perm.IsEnforced);
    }

    [PostgresFact]
    public async Task SectionICannotConfigure_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, waterDeptId, microTestCode, otherTestCode) = await SeedWaterScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildWaterService(actDb);
            var rows = new[]
            {
                new CreateWaterSamplingPointRequest($"SP-OWN-{suffix}", "My Lab", "Daily", new List<string> { microTestCode }, waterDeptId),
                new CreateWaterSamplingPointRequest($"SP-OTH-{suffix}", "Other Lab", "Daily", new List<string> { otherTestCode }, waterDeptId)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateWaterSamplingPointsBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("assignedTestCodes", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.WaterSamplingPoints.AsNoTracking().AnyAsync(p => p.Code == $"SP-OWN-{suffix}" || p.Code == $"SP-OTH-{suffix}"));
    }
}
