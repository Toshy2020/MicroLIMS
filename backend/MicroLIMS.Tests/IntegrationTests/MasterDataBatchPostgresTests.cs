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

    private static EnvironmentalMonitoringMasterDataService BuildEmService(MicroLimsDbContext db) =>
        new(db, new MasterDataBatchService(db));

    private async Task<(int HeadUserId, int DeptId, int SecondDeptId, int ExistingRoomId)> SeedEmScopeAsync(string suffix)
    {
        await using var db = _fixture.CreateDbContext();

        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.SectionHead);
        var user = new User
        {
            Username = $"em_batch_head_{suffix}",
            PasswordHash = "x",
            FullName = $"EM Batch Head {suffix}",
            Email = $"em_batch_{suffix}@test.local",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);

        var dept = new Department { Name = $"EM-Dept-{suffix}", Class = "Grade B", TestingFrequency = "Daily", IsActive = true };
        var secondDept = new Department { Name = $"EM-Dept2-{suffix}", Class = "Grade C", TestingFrequency = "Weekly", IsActive = true };
        db.Departments.AddRange(dept, secondDept);
        await db.SaveChangesAsync();

        var existingRoom = new Room
        {
            Name = $"EM-BaseRoom-{suffix}",
            DepartmentId = dept.Id,
            GradeClassification = "B",
            Kind = RoomKind.Room,
            IsActive = true
        };
        db.Rooms.Add(existingRoom);
        await db.SaveChangesAsync();

        return (user.Id, dept.Id, secondDept.Id, existingRoom.Id);
    }

    [PostgresFact]
    public async Task Rooms_AllValid_CreatesAll_AuditedLikeSingleCreate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, deptId, _, existingRoomId) = await SeedEmScopeAsync(suffix);
        var fixedNow = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        var freq = new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Tuesday);

        RoomResponse singleResult;
        await using (var singleDb = _fixture.CreateDbContext(new FixedClock(fixedNow)))
        {
            singleDb.CurrentUserId = userId;
            var svc = BuildEmService(singleDb);
            singleResult = await svc.CreateRoomAsync(
                new CreateRoomRequest($"RM-S-{suffix}", deptId, "B", RoomKind.Room, null, freq));
        }

        IReadOnlyList<RoomResponse> batchResults;
        await using (var actDb = _fixture.CreateDbContext(new FixedClock(fixedNow)))
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            batchResults = await svc.CreateRoomsBatchAsync(
                new[]
                {
                    new CreateRoomRequest($"RM-B1-{suffix}", deptId, "B", RoomKind.Room, null, freq),
                    new CreateRoomRequest($"RM-B2-{suffix}", deptId, "A", RoomKind.LAF, existingRoomId, freq)
                });
        }

        Assert.Equal(2, batchResults.Count);

        await using var assertDb = _fixture.CreateDbContext();
        var batchIds = batchResults.Select(r => r.Id).ToList();
        var persisted = await assertDb.Rooms
            .AsNoTracking()
            .Where(r => batchIds.Contains(r.Id))
            .OrderBy(r => r.Id)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal($"RM-B1-{suffix}", persisted[0].Name);
        Assert.Equal(RoomKind.Room, persisted[0].Kind);
        Assert.Null(persisted[0].ParentRoomId);
        Assert.Equal($"RM-B2-{suffix}", persisted[1].Name);
        Assert.Equal(RoomKind.LAF, persisted[1].Kind);
        Assert.Equal(existingRoomId, persisted[1].ParentRoomId);
        Assert.Equal(FrequencyType.Weekly, persisted[0].FrequencyType);
        Assert.Equal(DayOfWeek.Tuesday, persisted[0].SamplingWeekday);

        var singleIdStr = singleResult.Id.ToString();
        var batchIdStrs = batchIds.Select(id => id.ToString()).ToList();

        var singleAudit = await assertDb.AuditLogs
            .AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(Room) && a.Action == "Create" && a.EntityId == singleIdStr);

        var batchAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(Room) && a.Action == "Create" && batchIdStrs.Contains(a.EntityId))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, batchAudits.Count);

        var singlePayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(singleAudit.NewValue!)!;
        for (var i = 0; i < batchAudits.Count; i++)
        {
            var batchAudit = batchAudits[i];
            var batchRoom = persisted[i];
            Assert.Equal(singleAudit.UserId, batchAudit.UserId);
            Assert.Equal(singleAudit.Action, batchAudit.Action);
            Assert.Equal(singleAudit.EntityName, batchAudit.EntityName);
            Assert.Equal(singleAudit.PreviousValue, batchAudit.PreviousValue);
            Assert.Equal(singleAudit.Timestamp, batchAudit.Timestamp);

            var batchPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(batchAudit.NewValue!)!;
            Assert.Equal(singlePayload.Keys.OrderBy(k => k), batchPayload.Keys.OrderBy(k => k));
            Assert.Equal(batchRoom.Id, batchPayload["Id"].GetInt32());
            Assert.Equal(batchRoom.Name, batchPayload["Name"].GetString());
            Assert.Equal(batchRoom.DepartmentId, batchPayload["DepartmentId"].GetInt32());
            Assert.Equal(batchRoom.GradeClassification, batchPayload["GradeClassification"].GetString());
            Assert.Equal(batchRoom.IsActive, batchPayload["IsActive"].GetBoolean());
        }
    }

    [PostgresFact]
    public async Task Rooms_OneInvalid_NoneCreated_RowAndFieldReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, deptId, _, _) = await SeedEmScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var rows = new[]
            {
                new CreateRoomRequest($"RM-OK-{suffix}", deptId, "B"),
                new CreateRoomRequest("   ", deptId, "B"),
                new CreateRoomRequest($"RM-FREQ-{suffix}", deptId, "C", Frequency: new FrequencyRuleDto(FrequencyType.Daily, Interval: 2)),
                new CreateRoomRequest($"RM-LAF-{suffix}", deptId, "A", RoomKind.LAF, ParentRoomId: null)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateRoomsBatchAsync(rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 1 && e.Field == "name");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "frequency");
            Assert.Contains(ex.FieldErrors, e => e.Row == 3 && e.Field == "parentRoomId");
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Rooms.AsNoTracking().AnyAsync(r =>
            r.Name == $"RM-OK-{suffix}" || r.Name == $"RM-FREQ-{suffix}" || r.Name == $"RM-LAF-{suffix}"));
    }

    [PostgresFact]
    public async Task Rooms_DuplicateInRequest_BothRowsReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, deptId, secondDeptId, _) = await SeedEmScopeAsync(suffix);
        var dupName = $"RM-DUP-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var rows = new[]
            {
                new CreateRoomRequest(dupName, deptId, "B"),
                new CreateRoomRequest($"RM-UNIQ-{suffix}", deptId, "C"),
                new CreateRoomRequest($"  {dupName.ToLowerInvariant()}  ", deptId, "B"),
                new CreateRoomRequest(dupName, secondDeptId, "C")
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateRoomsBatchAsync(rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 0 && e.Field == "name");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "name");
            Assert.DoesNotContain(ex.FieldErrors, e => e.Row == 1 || e.Row == 3);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Rooms.AsNoTracking().AnyAsync(r => r.Name == dupName || r.Name == $"RM-UNIQ-{suffix}"));
    }

    [PostgresFact]
    public async Task Rooms_Existing_Reported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, deptId, _, _) = await SeedEmScopeAsync(suffix);
        var existingName = $"RM-EX-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.Rooms.Add(new Room
            {
                Name = existingName,
                DepartmentId = deptId,
                GradeClassification = "B",
                Kind = RoomKind.Room
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var rows = new[]
            {
                new CreateRoomRequest($"RM-FRESH-{suffix}", deptId, "C"),
                new CreateRoomRequest(existingName.ToLowerInvariant(), deptId, "B")
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateRoomsBatchAsync(rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("name", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Rooms.AsNoTracking().AnyAsync(r => r.Name == $"RM-FRESH-{suffix}"));
        Assert.Equal(1, await assertDb.Rooms.AsNoTracking().CountAsync(r => r.DepartmentId == deptId && r.Name == existingName));
    }

    [PostgresFact]
    public async Task Rooms_201Rows_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, deptId, _, _) = await SeedEmScopeAsync(suffix);
        var rows = Enumerable.Range(0, 201)
            .Select(i => new CreateRoomRequest($"RM-201-{i}-{suffix}", deptId, "C"))
            .ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateRoomsBatchAsync(rows));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Rooms.AsNoTracking().AnyAsync(r => r.Name.StartsWith("RM-201-") && r.Name.EndsWith(suffix)));
    }

    [PostgresFact]
    public async Task Rooms_WithoutPermission_403()
    {
        var method = typeof(EnvironmentalMonitoringMasterDataController).GetMethod(nameof(EnvironmentalMonitoringMasterDataController.CreateRoomsBatch));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("rooms/batch", post!.Template);

        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.MasterDataManage, auth!.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());

        var allowed = AuthorizationMatrixTests.AllowedRoles<EnvironmentalMonitoringMasterDataController>(
            nameof(EnvironmentalMonitoringMasterDataController.CreateRoomsBatch));
        var singleAllowed = AuthorizationMatrixTests.AllowedRoles<EnvironmentalMonitoringMasterDataController>(
            nameof(EnvironmentalMonitoringMasterDataController.CreateRoom));
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
    public async Task RoomTestConfigs_AllValid_CreatesAll_AuditedLikeSingleCreate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);
        var fixedNow = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

        RoomTestConfigurationResponse singleResult;
        await using (var singleDb = _fixture.CreateDbContext(new FixedClock(fixedNow)))
        {
            singleDb.CurrentUserId = userId;
            var svc = BuildEmService(singleDb);
            singleResult = await svc.CreateRoomTestConfigurationAsync(
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, $"TC-S-{suffix}", "5", "10", "20"));
        }

        IReadOnlyList<RoomTestConfigurationResponse> batchResults;
        await using (var actDb = _fixture.CreateDbContext(new FixedClock(fixedNow)))
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            batchResults = await svc.CreateRoomTestConfigurationsBatchAsync(
                new[]
                {
                    new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, $"TC-B-{suffix}", "5", "10", "20"),
                    new CreateRoomTestConfigRequest(roomId, RoomTestTypes.ActiveAirSample, $"TC-B-{suffix}", "1", "3", "5", " CFU/m3 ")
                });
        }

        Assert.Equal(2, batchResults.Count);

        await using var assertDb = _fixture.CreateDbContext();
        var batchIds = batchResults.Select(r => r.Id).ToList();
        var persisted = await assertDb.RoomTestConfigurations
            .AsNoTracking()
            .Where(c => batchIds.Contains(c.Id))
            .OrderBy(c => c.Id)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal(RoomTestTypes.PassiveAirSample, persisted[0].TestType);
        Assert.Equal($"TC-B-{suffix}", persisted[0].TestCode);
        Assert.Equal(string.Empty, persisted[0].Unit);
        Assert.Equal(RoomTestTypes.ActiveAirSample, persisted[1].TestType);
        Assert.Equal($"TC-B-{suffix}", persisted[1].TestCode);
        Assert.Equal("CFU/m3", persisted[1].Unit);

        var singleIdStr = singleResult.Id.ToString();
        var batchIdStrs = batchIds.Select(id => id.ToString()).ToList();

        var singleAudit = await assertDb.AuditLogs
            .AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(RoomTestConfiguration) && a.Action == "Create" && a.EntityId == singleIdStr);

        var batchAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(RoomTestConfiguration) && a.Action == "Create" && batchIdStrs.Contains(a.EntityId))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, batchAudits.Count);

        var singlePayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(singleAudit.NewValue!)!;
        for (var i = 0; i < batchAudits.Count; i++)
        {
            var batchAudit = batchAudits[i];
            var batchConfig = persisted[i];
            Assert.Equal(singleAudit.UserId, batchAudit.UserId);
            Assert.Equal(singleAudit.Action, batchAudit.Action);
            Assert.Equal(singleAudit.EntityName, batchAudit.EntityName);
            Assert.Equal(singleAudit.PreviousValue, batchAudit.PreviousValue);
            Assert.Equal(singleAudit.Timestamp, batchAudit.Timestamp);

            var batchPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(batchAudit.NewValue!)!;
            Assert.Equal(singlePayload.Keys.OrderBy(k => k), batchPayload.Keys.OrderBy(k => k));
            Assert.Equal(batchConfig.Id, batchPayload["Id"].GetInt32());
            Assert.Equal(batchConfig.RoomId, batchPayload["RoomId"].GetInt32());
            Assert.Equal(batchConfig.TestType, batchPayload["TestType"].GetString());
            Assert.Equal(batchConfig.TestCode, batchPayload["TestCode"].GetString());
            Assert.Equal(batchConfig.Unit, batchPayload["Unit"].GetString());
            Assert.Equal(batchConfig.IsActive, batchPayload["IsActive"].GetBoolean());
        }
    }

    [PostgresFact]
    public async Task RoomTestConfigs_OneInvalid_NoneCreated_RowAndFieldReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var rows = new[]
            {
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, $"TC-OK-{suffix}", "5", "10", "20"),
                new CreateRoomTestConfigRequest(roomId, "Swab", $"TC-BADTYPE-{suffix}", "1", "2", "5", "CFU/swab"),
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.ActiveAirSample, $"TC-NOUNIT-{suffix}", "1", "2", "5", "   "),
                new CreateRoomTestConfigRequest(-1, RoomTestTypes.PassiveAirSample, $"TC-BADROOM-{suffix}", "1", "2", "5")
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateRoomTestConfigurationsBatchAsync(rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 1 && e.Field == "testType");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "unit");
            Assert.Contains(ex.FieldErrors, e => e.Row == 3 && e.Field == "roomId");
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RoomTestConfigurations.AsNoTracking().AnyAsync(c => c.TestCode.EndsWith(suffix)));
    }

    [PostgresFact]
    public async Task RoomTestConfigs_DuplicateInRequest_BothRowsReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);
        var dupCode = $"TC-DUP-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var rows = new[]
            {
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, dupCode, "5", "10", "20"),
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.ActiveAirSample, dupCode, "1", "3", "5", "CFU/m3"),
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, $"  {dupCode.ToLowerInvariant()}  ", "5", "10", "20")
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateRoomTestConfigurationsBatchAsync(rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 0 && e.Field == "testCode");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "testCode");
            Assert.DoesNotContain(ex.FieldErrors, e => e.Row == 1);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RoomTestConfigurations.AsNoTracking().AnyAsync(c => c.RoomId == roomId));
    }

    [PostgresFact]
    public async Task RoomTestConfigs_Existing_Reported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);
        var existingCode = $"TC-EX-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.RoomTestConfigurations.Add(new RoomTestConfiguration
            {
                RoomId = roomId,
                TestType = RoomTestTypes.PassiveAirSample,
                TestCode = existingCode,
                AlertLimit = "5",
                ActionLimit = "10",
                SpecLimit = "20",
                Unit = string.Empty
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var rows = new[]
            {
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, $"TC-FRESH-{suffix}", "5", "10", "20"),
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, existingCode.ToLowerInvariant(), "5", "10", "20")
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateRoomTestConfigurationsBatchAsync(rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("testCode", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RoomTestConfigurations.AsNoTracking().AnyAsync(c => c.TestCode == $"TC-FRESH-{suffix}"));
        Assert.Equal(1, await assertDb.RoomTestConfigurations.AsNoTracking().CountAsync(c => c.RoomId == roomId && c.TestCode == existingCode));
    }

    [PostgresFact]
    public async Task RoomTestConfigs_InactiveExisting_DoesNotBlockANewActiveRow()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);
        var code = $"TC-OLD-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.RoomTestConfigurations.Add(new RoomTestConfiguration
            {
                RoomId = roomId,
                TestType = RoomTestTypes.PassiveAirSample,
                TestCode = code,
                AlertLimit = "5",
                ActionLimit = "10",
                SpecLimit = "20",
                Unit = string.Empty,
                IsActive = false
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            await svc.CreateRoomTestConfigurationsBatchAsync(new[]
            {
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, code, "6", "12", "20")
            });
        }

        await using var assertDb = _fixture.CreateDbContext();
        var rows = await assertDb.RoomTestConfigurations.AsNoTracking()
            .Where(c => c.RoomId == roomId && c.TestCode == code)
            .OrderBy(c => c.Id)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.False(rows[0].IsActive);
        Assert.True(rows[1].IsActive);
        Assert.Equal("6", rows[1].AlertLimit);
    }

    [PostgresFact]
    public async Task RoomTestConfigs_201Rows_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);
        var rows = Enumerable.Range(0, 201)
            .Select(i => new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, $"TC-201-{i}-{suffix}", "5", "10", "20"))
            .ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateRoomTestConfigurationsBatchAsync(rows));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.RoomTestConfigurations.AsNoTracking().AnyAsync(c => c.RoomId == roomId));
    }

    [PostgresFact]
    public async Task RoomTestConfigs_WithoutPermission_403()
    {
        var method = typeof(EnvironmentalMonitoringMasterDataController).GetMethod(
            nameof(EnvironmentalMonitoringMasterDataController.CreateRoomTestConfigurationsBatch));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("room-test-configurations/batch", post!.Template);

        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.MasterDataManage, auth!.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());

        var allowed = AuthorizationMatrixTests.AllowedRoles<EnvironmentalMonitoringMasterDataController>(
            nameof(EnvironmentalMonitoringMasterDataController.CreateRoomTestConfigurationsBatch));
        var singleAllowed = AuthorizationMatrixTests.AllowedRoles<EnvironmentalMonitoringMasterDataController>(
            nameof(EnvironmentalMonitoringMasterDataController.CreateRoomTestConfiguration));
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
    public async Task SingleRoom_DuplicateNameInDepartment_Refused()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, deptId, _, _) = await SeedEmScopeAsync(suffix);
        var existingName = $"RM-S-EX-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.Rooms.Add(new Room
            {
                Name = existingName,
                DepartmentId = deptId,
                GradeClassification = "B",
                Kind = RoomKind.Room
            });
            await arrangeDb.SaveChangesAsync();
        }

        var duplicateInput = $"  {existingName.ToLowerInvariant()}  ";
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.CreateRoomAsync(new CreateRoomRequest(duplicateInput, deptId, "B")));
            Assert.Equal($"Room '{existingName.ToLowerInvariant()}' already exists.", ex.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(1, await assertDb.Rooms.AsNoTracking().CountAsync(r => r.DepartmentId == deptId && r.Name.ToLower() == existingName.ToLower()));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Room) && a.Action == "Create" && a.UserId == userId));
    }

    [PostgresFact]
    public async Task SingleRoom_SameNameOtherDepartment_Allowed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, secondDeptId, _) = await SeedEmScopeAsync(suffix);
        var sharedName = $"EM-BaseRoom-{suffix}";

        RoomResponse created;
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            created = await svc.CreateRoomAsync(new CreateRoomRequest(sharedName, secondDeptId, "C"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.Rooms.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        Assert.Equal(secondDeptId, persisted.DepartmentId);
        Assert.Equal(sharedName, persisted.Name);
        Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Room) && a.Action == "Create" && a.EntityId == created.Id.ToString() && a.UserId == userId));
    }

    [PostgresFact]
    public async Task SingleConfig_DuplicateActive_Refused()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);
        var existingCode = $"TC-S-EX-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.RoomTestConfigurations.Add(new RoomTestConfiguration
            {
                RoomId = roomId,
                TestType = RoomTestTypes.PassiveAirSample,
                TestCode = existingCode,
                AlertLimit = "5",
                ActionLimit = "10",
                SpecLimit = "20",
                Unit = string.Empty,
                IsActive = true
            });
            await arrangeDb.SaveChangesAsync();
        }

        var duplicateCode = $"  {existingCode.ToLowerInvariant()}  ";
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.CreateRoomTestConfigurationAsync(
                    new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, duplicateCode, "5", "10", "20")));
            Assert.Equal(
                $"Test configuration '{existingCode.ToLowerInvariant()}' ({RoomTestTypes.PassiveAirSample}) already exists for room {roomId}.",
                ex.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(1, await assertDb.RoomTestConfigurations.AsNoTracking().CountAsync(c => c.RoomId == roomId));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(RoomTestConfiguration) && a.Action == "Create" && a.UserId == userId));
    }

    [PostgresFact]
    public async Task SingleConfig_InactiveExisting_Allowed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);
        var code = $"TC-S-OLD-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.RoomTestConfigurations.Add(new RoomTestConfiguration
            {
                RoomId = roomId,
                TestType = RoomTestTypes.PassiveAirSample,
                TestCode = code,
                AlertLimit = "5",
                ActionLimit = "10",
                SpecLimit = "20",
                Unit = string.Empty,
                IsActive = false
            });
            await arrangeDb.SaveChangesAsync();
        }

        RoomTestConfigurationResponse created;
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            created = await svc.CreateRoomTestConfigurationAsync(
                new CreateRoomTestConfigRequest(roomId, RoomTestTypes.PassiveAirSample, code, "6", "12", "20"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var rows = await assertDb.RoomTestConfigurations.AsNoTracking()
            .Where(c => c.RoomId == roomId && c.TestCode == code)
            .OrderBy(c => c.Id)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.False(rows[0].IsActive);
        Assert.True(rows[1].IsActive);
        Assert.Equal(created.Id, rows[1].Id);
        Assert.True(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(RoomTestConfiguration) && a.Action == "Create" && a.EntityId == created.Id.ToString() && a.UserId == userId));
    }

    [PostgresFact]
    public async Task UpdateRoom_DuplicateNameInDepartment_Refused_AndSelfUpdateAllowed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, deptId, _, existingRoomId) = await SeedEmScopeAsync(suffix);
        var existingName = $"EM-BaseRoom-{suffix}";
        var otherName = $"RM-UPD-{suffix}";
        int otherRoomId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var other = new Room
            {
                Name = otherName,
                DepartmentId = deptId,
                GradeClassification = "B",
                Kind = RoomKind.Room
            };
            arrangeDb.Rooms.Add(other);
            await arrangeDb.SaveChangesAsync();
            otherRoomId = other.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.UpdateRoomAsync(otherRoomId, new UpdateRoomRequest($"  {existingName.ToLowerInvariant()}  ", deptId, "B")));
            Assert.Equal($"Room '{existingName.ToLowerInvariant()}' already exists.", ex.Message);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var persisted = await assertDb.Rooms.AsNoTracking().SingleAsync(r => r.Id == otherRoomId);
            Assert.Equal(otherName, persisted.Name);
            Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityName == nameof(Room) && a.Action == "Update" && a.EntityId == otherRoomId.ToString()));
        }

        await using (var selfActDb = _fixture.CreateDbContext())
        {
            selfActDb.CurrentUserId = userId;
            var svc = BuildEmService(selfActDb);
            await svc.UpdateRoomAsync(existingRoomId, new UpdateRoomRequest(existingName, deptId, "A"));
        }

        await using var finalAssertDb = _fixture.CreateDbContext();
        var updatedSelf = await finalAssertDb.Rooms.AsNoTracking().SingleAsync(r => r.Id == existingRoomId);
        Assert.Equal("A", updatedSelf.GradeClassification);
    }

    [PostgresFact]
    public async Task UpdateRoomTestConfig_DuplicateActive_Refused_AndSelfUpdateAllowed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, _, _, roomId) = await SeedEmScopeAsync(suffix);
        var activeCode = $"TC-ACT-{suffix}";
        var secondCode = $"TC-SEC-{suffix}";
        int activeId;
        int inactiveSameId;
        int secondActiveId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var active = new RoomTestConfiguration
            {
                RoomId = roomId,
                TestType = RoomTestTypes.PassiveAirSample,
                TestCode = activeCode,
                AlertLimit = "5",
                ActionLimit = "10",
                SpecLimit = "20",
                Unit = string.Empty,
                IsActive = true
            };
            var inactiveSame = new RoomTestConfiguration
            {
                RoomId = roomId,
                TestType = RoomTestTypes.PassiveAirSample,
                TestCode = activeCode,
                AlertLimit = "4",
                ActionLimit = "8",
                SpecLimit = "16",
                Unit = string.Empty,
                IsActive = false
            };
            var secondActive = new RoomTestConfiguration
            {
                RoomId = roomId,
                TestType = RoomTestTypes.PassiveAirSample,
                TestCode = secondCode,
                AlertLimit = "5",
                ActionLimit = "10",
                SpecLimit = "20",
                Unit = string.Empty,
                IsActive = true
            };
            arrangeDb.RoomTestConfigurations.AddRange(active, inactiveSame, secondActive);
            await arrangeDb.SaveChangesAsync();
            activeId = active.Id;
            inactiveSameId = inactiveSame.Id;
            secondActiveId = secondActive.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildEmService(actDb);
            var reactivateEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.UpdateRoomTestConfigurationAsync(
                    inactiveSameId,
                    new UpdateRoomTestConfigRequest(RoomTestTypes.PassiveAirSample, activeCode, "4", "8", "16", IsActive: true)));
            Assert.Equal(
                $"Test configuration '{activeCode}' ({RoomTestTypes.PassiveAirSample}) already exists for room {roomId}.",
                reactivateEx.Message);

            var renameEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.UpdateRoomTestConfigurationAsync(
                    secondActiveId,
                    new UpdateRoomTestConfigRequest(RoomTestTypes.PassiveAirSample, $" {activeCode.ToLowerInvariant()} ", "5", "10", "20")));
            Assert.Equal(
                $"Test configuration '{activeCode.ToLowerInvariant()}' ({RoomTestTypes.PassiveAirSample}) already exists for room {roomId}.",
                renameEx.Message);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var inactiveRow = await assertDb.RoomTestConfigurations.AsNoTracking().SingleAsync(c => c.Id == inactiveSameId);
            Assert.False(inactiveRow.IsActive);
            var secondRow = await assertDb.RoomTestConfigurations.AsNoTracking().SingleAsync(c => c.Id == secondActiveId);
            Assert.Equal(secondCode, secondRow.TestCode);
            Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityName == nameof(RoomTestConfiguration)
                && a.Action == "Update"
                && (a.EntityId == inactiveSameId.ToString() || a.EntityId == secondActiveId.ToString())));
        }

        await using (var selfActDb = _fixture.CreateDbContext())
        {
            selfActDb.CurrentUserId = userId;
            var svc = BuildEmService(selfActDb);
            await svc.UpdateRoomTestConfigurationAsync(
                activeId,
                new UpdateRoomTestConfigRequest(RoomTestTypes.PassiveAirSample, activeCode, "6", "12", "24", IsActive: true));
        }

        await using var finalAssertDb = _fixture.CreateDbContext();
        var updatedSelf = await finalAssertDb.RoomTestConfigurations.AsNoTracking().SingleAsync(c => c.Id == activeId);
        Assert.Equal("6", updatedSelf.AlertLimit);
    }

    private static SpecificationMasterDataService BuildSpecService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), new SpecificationService(db), new MasterDataBatchService(db));

    private async Task<(int HeadUserId, int ItemId, string MicroTestCode, string OtherTestCode, string TitrationCode)> SeedSpecScopeAsync(string suffix)
    {
        await using var db = _fixture.CreateDbContext();

        var micro = await db.DocumentSections.FirstAsync(s => s.Code == LaboratoryCodes.Microbiology);
        var other = await db.DocumentSections.FirstOrDefaultAsync(s => s.Id != micro.Id && s.IsActive);
        if (other is null)
        {
            other = new DocumentSection { Code = $"SP-OTH-{suffix}", Name = $"Other Lab {suffix}", IsActive = true };
            db.DocumentSections.Add(other);
            await db.SaveChangesAsync();
        }

        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.SectionHead);
        var user = new User
        {
            Username = $"sp_batch_head_{suffix}",
            PasswordHash = "x",
            FullName = $"Spec Batch Head {suffix}",
            Email = $"sp_batch_{suffix}@test.local",
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

        var microCode = $"SP-MIC-{suffix}";
        var otherCode = $"SP-OTH-{suffix}";
        var titrCode = $"SP-TITR-{suffix}";
        var lodCode = $"SP-LOD-{suffix}";
        var kfCode = $"SP-KF-{suffix}";

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = microCode, DisplayName = $"Micro Spec {suffix}", SectionId = micro.Id, IsActive = true },
            new TestDefinition { Code = otherCode, DisplayName = $"Other Spec {suffix}", SectionId = other.Id, IsActive = true },
            new TestDefinition
            {
                Code = titrCode,
                DisplayName = $"Titration Spec {suffix}",
                SectionId = micro.Id,
                WorkflowType = WorkflowType.Titration,
                EquationType = EquationType.Titration,
                PhyschemArea = PhyschemArea.Both,
                IsActive = true
            },
            new TestDefinition
            {
                Code = lodCode,
                DisplayName = $"LoD {suffix}",
                SectionId = micro.Id,
                WorkflowType = WorkflowType.Gravimetric,
                MoistureRole = MoistureRole.LossOnDrying,
                IsActive = true
            },
            new TestDefinition
            {
                Code = kfCode,
                DisplayName = $"KF {suffix}",
                SectionId = micro.Id,
                WorkflowType = WorkflowType.Titration,
                MoistureRole = MoistureRole.Water,
                IsActive = true
            });

        var item = new Item
        {
            Code = $"SP-ITEM-{suffix}",
            Name = $"Spec Item {suffix}",
            Category = SampleCategory.FinishedProduct,
            AssignedTests =
            {
                new SampleTest { TestCode = microCode, DisplayName = $"Micro Spec {suffix}" },
                new SampleTest { TestCode = otherCode, DisplayName = $"Other Spec {suffix}" },
                new SampleTest { TestCode = titrCode, DisplayName = $"Titration Spec {suffix}" },
                new SampleTest { TestCode = lodCode, DisplayName = $"LoD {suffix}" },
                new SampleTest { TestCode = kfCode, DisplayName = $"KF {suffix}" }
            }
        };
        db.Items.Add(item);

        await db.SaveChangesAsync();
        return (user.Id, item.Id, microCode, otherCode, titrCode);
    }

    [PostgresFact]
    public async Task Specs_AllValid_CreatesAll_AuditedLikeSingleCreate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, itemId, microCode, _, _) = await SeedSpecScopeAsync(suffix);
        var fixedNow = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

        SpecificationResponse singleResult;
        await using (var singleDb = _fixture.CreateDbContext(new FixedClock(fixedNow)))
        {
            singleDb.CurrentUserId = userId;
            var svc = BuildSpecService(singleDb);
            singleResult = await svc.CreateSpecificationAsync(
                userId,
                new CreateSpecificationRequest(
                    ItemId: itemId,
                    TestCode: microCode,
                    ParameterName: $"Param-S-{suffix}",
                    LimitType: LimitType.Range,
                    LowerLimit: 10m,
                    UpperLimit: 20m,
                    Unit: "mg",
                    Grade: "bp"));
        }

        IReadOnlyList<SpecificationResponse> batchResults;
        await using (var actDb = _fixture.CreateDbContext(new FixedClock(fixedNow)))
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSpecService(actDb);
            batchResults = await svc.CreateSpecificationsBatchAsync(
                userId,
                new[]
                {
                    new CreateSpecificationRequest(
                        ItemId: itemId,
                        TestCode: microCode,
                        ParameterName: $"Param-B1-{suffix}",
                        LimitType: LimitType.Range,
                        LowerLimit: 1m,
                        UpperLimit: 5m,
                        Unit: "mg",
                        Grade: "bp"),
                    new CreateSpecificationRequest(
                        ItemId: itemId,
                        TestCode: microCode,
                        ParameterName: $"Param-B2-{suffix}",
                        LimitType: LimitType.NotMoreThan,
                        UpperLimit: 100m,
                        Unit: "CFU/g",
                        ProductionStageRole: ProductionStageRole.Finished)
                });
        }

        Assert.Equal(2, batchResults.Count);

        await using var assertDb = _fixture.CreateDbContext();
        var batchIds = batchResults.Select(r => r.Id).ToList();
        var persisted = await assertDb.Specifications
            .AsNoTracking()
            .Where(s => batchIds.Contains(s.Id))
            .OrderBy(s => s.Id)
            .ToListAsync();

        Assert.Equal(2, persisted.Count);
        Assert.Equal($"Param-B1-{suffix}", persisted[0].ParameterName);
        Assert.Equal(LimitType.Range, persisted[0].LimitType);
        Assert.Equal("1-5", persisted[0].SpecLimit);
        Assert.Equal("BP", persisted[0].Grade);
        Assert.Null(persisted[0].ProductionStageRole);

        Assert.Equal($"Param-B2-{suffix}", persisted[1].ParameterName);
        Assert.Equal(LimitType.NotMoreThan, persisted[1].LimitType);
        Assert.Equal("NMT 100", persisted[1].SpecLimit);
        Assert.Equal(ProductionStageRole.Finished, persisted[1].ProductionStageRole);

        var singleIdStr = singleResult.Id.ToString();
        var batchIdStrs = batchIds.Select(id => id.ToString()).ToList();

        var singleAudit = await assertDb.AuditLogs
            .AsNoTracking()
            .SingleAsync(a => a.EntityName == nameof(Specification) && a.Action == "Create" && a.EntityId == singleIdStr);

        var batchAudits = await assertDb.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == nameof(Specification) && a.Action == "Create" && batchIdStrs.Contains(a.EntityId))
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, batchAudits.Count);

        var singlePayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(singleAudit.NewValue!)!;
        for (var i = 0; i < batchAudits.Count; i++)
        {
            var batchAudit = batchAudits[i];
            var batchSpec = persisted[i];
            Assert.Equal(singleAudit.UserId, batchAudit.UserId);
            Assert.Equal(singleAudit.Action, batchAudit.Action);
            Assert.Equal(singleAudit.EntityName, batchAudit.EntityName);
            Assert.Equal(singleAudit.PreviousValue, batchAudit.PreviousValue);
            Assert.Equal(singleAudit.Timestamp, batchAudit.Timestamp);

            var batchPayload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(batchAudit.NewValue!)!;
            Assert.Equal(singlePayload.Keys.OrderBy(k => k), batchPayload.Keys.OrderBy(k => k));
            Assert.Equal(batchSpec.Id, batchPayload["Id"].GetInt32());
            Assert.Equal(batchSpec.ItemId, batchPayload["ItemId"].GetInt32());
            Assert.Equal(batchSpec.TestCode, batchPayload["TestCode"].GetString());
            Assert.Equal(batchSpec.ParameterName, batchPayload["ParameterName"].GetString());
            Assert.Equal(batchSpec.SpecLimit, batchPayload["SpecLimit"].GetString());
            Assert.Equal(batchSpec.Unit, batchPayload["Unit"].GetString());
        }
    }

    [PostgresFact]
    public async Task Specs_OneInvalid_NoneCreated_RowAndMessageReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, itemId, microCode, _, _) = await SeedSpecScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSpecService(actDb);
            var rows = new[]
            {
                new CreateSpecificationRequest(itemId, microCode, ParameterName: $"Param-OK-{suffix}", LimitType: LimitType.NotMoreThan, UpperLimit: 10m, Unit: "mg"),
                new CreateSpecificationRequest(itemId, microCode, ParameterName: $"Param-NoMax-{suffix}", LimitType: LimitType.NotMoreThan, UpperLimit: null, Unit: "mg")
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateSpecificationsBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("Upper limit is required for Not More Than specifications.", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<Specification>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == itemId));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Specification) && a.Action == "Create" && a.NewValue != null && a.NewValue.Contains($"Param-OK-{suffix}")));
    }

    [PostgresFact]
    public async Task Specs_TwoRowsWithUnknownItem_BothReportedBeforeAnyWrite()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, itemId, microCode, _, _) = await SeedSpecScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSpecService(actDb);
            var rows = new[]
            {
                new CreateSpecificationRequest(int.MaxValue - 1, microCode, ParameterName: $"Param-Miss1-{suffix}", LimitType: LimitType.NotMoreThan, UpperLimit: 10m),
                new CreateSpecificationRequest(itemId, microCode, ParameterName: $"Param-Valid-{suffix}", LimitType: LimitType.NotMoreThan, UpperLimit: 10m),
                new CreateSpecificationRequest(int.MaxValue - 2, microCode, ParameterName: $"Param-Miss2-{suffix}", LimitType: LimitType.NotMoreThan, UpperLimit: 10m)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateSpecificationsBatchAsync(userId, rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 0 && e.Field == "itemId");
            Assert.Contains(ex.FieldErrors, e => e.Row == 2 && e.Field == "itemId");
            Assert.DoesNotContain(ex.FieldErrors, e => e.Row == 1);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == itemId));
    }

    [PostgresFact]
    public async Task Specs_DuplicateInRequest_LaterRowReported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, itemId, microCode, _, _) = await SeedSpecScopeAsync(suffix);
        var dupParam = $"Param-Dup-{suffix}";

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSpecService(actDb);
            var rows = new[]
            {
                new CreateSpecificationRequest(itemId, microCode, ParameterName: dupParam, LimitType: LimitType.NotMoreThan, UpperLimit: 10m, Grade: "bp"),
                new CreateSpecificationRequest(itemId, microCode, ParameterName: dupParam, LimitType: LimitType.NotMoreThan, UpperLimit: 15m, Grade: "USP"),
                new CreateSpecificationRequest(itemId, microCode, ParameterName: dupParam, LimitType: LimitType.NotMoreThan, UpperLimit: 15m, Grade: "BP")
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateSpecificationsBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(2, err.Row);
            Assert.Equal("A specification for this parameter and grade already exists.", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<Specification>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == itemId));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Specification) && a.Action == "Create" && a.NewValue != null && a.NewValue.Contains(dupParam)));
    }

    [PostgresFact]
    public async Task Specs_Existing_Reported()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, itemId, microCode, _, _) = await SeedSpecScopeAsync(suffix);
        var existingParam = $"Param-Ex-{suffix}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.Specifications.Add(new Specification
            {
                ItemId = itemId,
                TestCode = microCode,
                ParameterName = existingParam,
                LimitType = LimitType.NotMoreThan,
                UpperLimit = 10m,
                SpecLimit = "NMT 10",
                Grade = "In-house"
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSpecService(actDb);
            var rows = new[]
            {
                new CreateSpecificationRequest(itemId, microCode, ParameterName: $"Param-Fresh-{suffix}", LimitType: LimitType.NotMoreThan, UpperLimit: 25m),
                new CreateSpecificationRequest(itemId, microCode, ParameterName: existingParam, LimitType: LimitType.NotMoreThan, UpperLimit: 15m, Grade: "in-house")
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateSpecificationsBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("A specification for this parameter and grade already exists.", err.Message);
            Assert.Empty(actDb.ChangeTracker.Entries<Specification>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == itemId && s.ParameterName == $"Param-Fresh-{suffix}"));
        Assert.Equal(1, await assertDb.Specifications.AsNoTracking().CountAsync(s => s.ItemId == itemId));
    }

    [PostgresFact]
    public async Task Specs_201Rows_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, itemId, microCode, _, _) = await SeedSpecScopeAsync(suffix);
        var rows = Enumerable.Range(0, 201)
            .Select(i => new CreateSpecificationRequest(
                itemId,
                microCode,
                ParameterName: $"Param-201-{i}-{suffix}",
                LimitType: LimitType.NotMoreThan,
                UpperLimit: 100m))
            .ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSpecService(actDb);
            await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateSpecificationsBatchAsync(userId, rows));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == itemId));
    }

    [PostgresFact]
    public async Task Specs_SectionICannotConfigure_Rejected()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, itemId, microCode, otherCode, _) = await SeedSpecScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSpecService(actDb);
            var rows = new[]
            {
                new CreateSpecificationRequest(itemId, microCode, ParameterName: $"Param-Own-{suffix}", LimitType: LimitType.NotMoreThan, UpperLimit: 10m),
                new CreateSpecificationRequest(itemId, otherCode, ParameterName: $"Param-Other-{suffix}", LimitType: LimitType.NotMoreThan, UpperLimit: 10m)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateSpecificationsBatchAsync(userId, rows));
            var err = Assert.Single(ex.FieldErrors);
            Assert.Equal(1, err.Row);
            Assert.Equal("testCode", err.Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == itemId));
    }

    [PostgresFact]
    public async Task Specs_FailureInsideTransaction_RollsBackEarlierRows()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, itemId, _, _, titrationCode) = await SeedSpecScopeAsync(suffix);

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = BuildSpecService(actDb);
            // Both rows pass per-row pre-validation and uniqueness checks; row 1 fails inside
            // the transaction because row 0 already persisted PercentDriedBasis for this titration.
            var rows = new[]
            {
                new CreateSpecificationRequest(
                    ItemId: itemId,
                    TestCode: titrationCode,
                    ParameterName: $"Titr-Dried-{suffix}",
                    LimitType: LimitType.Range,
                    LowerLimit: 98m,
                    UpperLimit: 102m,
                    Unit: "%",
                    ResultBasis: ResultBasis.PercentDriedBasis),
                new CreateSpecificationRequest(
                    ItemId: itemId,
                    TestCode: titrationCode,
                    ParameterName: $"Titr-Anhyd-{suffix}",
                    LimitType: LimitType.Range,
                    LowerLimit: 98m,
                    UpperLimit: 102m,
                    Unit: "%",
                    ResultBasis: ResultBasis.PercentAnhydrousBasis)
            };

            var ex = await Assert.ThrowsAsync<FieldValidationException>(() => svc.CreateSpecificationsBatchAsync(userId, rows));
            Assert.Contains(ex.FieldErrors, e => e.Row == 1);
            Assert.Empty(actDb.ChangeTracker.Entries<Specification>());
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.Specifications.AsNoTracking().AnyAsync(s => s.ItemId == itemId));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(Specification)
            && a.Action == "Create"
            && a.NewValue != null
            && (a.NewValue.Contains($"Titr-Dried-{suffix}") || a.NewValue.Contains($"Titr-Anhyd-{suffix}"))));
    }

    [PostgresFact]
    public async Task Specs_WithoutPermission_403()
    {
        var method = typeof(SpecificationMasterDataController).GetMethod(
            nameof(SpecificationMasterDataController.CreateSpecificationsBatch));
        Assert.NotNull(method);

        var post = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("specifications/batch", post!.Template);

        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.MasterDataManage, auth!.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());

        var allowed = AuthorizationMatrixTests.AllowedRoles<SpecificationMasterDataController>(
            nameof(SpecificationMasterDataController.CreateSpecificationsBatch));
        var singleAllowed = AuthorizationMatrixTests.AllowedRoles<SpecificationMasterDataController>(
            nameof(SpecificationMasterDataController.CreateSpecification));
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
}
