using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Configuration;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Helpers;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ChangeReasonPostgresTests
{
    private const string JwtSigningKey = "TestJwtKey_ChangeReason_0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ!";
    private const string Password = "Password123!";
    private static readonly DateTimeOffset ServerNow = new(2031, 4, 5, 6, 7, 8, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public ChangeReasonPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private async Task<(WebApplication App, HttpClient Client)> StartHostAsync()
    {
        var cs = _fixture.TestConnectionString;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtSigningKey,
            ["Jwt:Issuer"] = "MicroLIMS",
            ["Jwt:Audience"] = "MicroLIMS.Client",
            ["ConnectionStrings:DefaultConnection"] = cs
        });

        var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(jwtSettings);
        builder.Services.AddMicroLimsDbContext(opt => opt.UseNpgsql(cs));
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(ServerNow));
        builder.Services.AddMicroLimsJwtAuthentication(jwtSettings);
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddMicroLimsCors(new[] { "http://localhost:5173" });
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.Filters.Add<MustChangePasswordFilter>();
            })
            .AddApplicationPart(typeof(SpecificationMasterDataController).Assembly)
            .AddJsonOptions(o => ApiJsonOptions.Configure(o.JsonSerializerOptions));

        var app = builder.Build();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseRouting();
        app.UseCors("Frontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMicroLimsAuditPipeline();
        app.UseRateLimiter();
        app.MapControllers();

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private async Task<(int UserId, string Username)> SeedAdminAsync()
    {
        var username = "cr_admin_" + Guid.NewGuid().ToString("N")[..12];
        await using var arrangeDb = _fixture.CreateDbContext();
        var roleId = await arrangeDb.Roles.AsNoTracking()
            .Where(r => r.Type == RoleType.SystemAdministrator)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync();
        var user = new User
        {
            FullName = "Change Reason Admin " + username[^6..],
            Username = username,
            Email = username + "@example.test",
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        arrangeDb.Users.Add(user);
        await arrangeDb.SaveChangesAsync();
        return (user.Id, username);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    private async Task<(int ItemId, int SpecId, string TestCode, int CauseId)> SeedSpecWithOptionalOpenOrderAsync(bool withOpenOrder)
    {
        var tag = Tag();
        var testCode = "TST-" + tag;
        await using var arrangeDb = _fixture.CreateDbContext();

        var section = TestServiceFactory.EnsureMicroSection(arrangeDb);
        var td = new TestDefinition
        {
            Code = testCode,
            DisplayName = "Test " + tag,
            SectionId = section.Id,
            WorkflowType = WorkflowType.Measurement
        };
        arrangeDb.TestDefinitions.Add(td);

        var item = new Item
        {
            Code = "ITM-" + tag,
            Name = "Item " + tag,
            Category = SampleCategory.RawMaterial,
            AssignedTests = new List<SampleTest>
            {
                new() { TestCode = testCode, DisplayName = td.DisplayName }
            }
        };
        arrangeDb.Items.Add(item);

        var cause = new CauseOfTesting { Name = "Routine-" + tag };
        arrangeDb.CausesOfTesting.Add(cause);
        await arrangeDb.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testCode,
            ParameterName = "Appearance",
            LimitType = LimitType.Range,
            LowerLimit = 1m,
            UpperLimit = 10m,
            LowerInclusive = true,
            UpperInclusive = true,
            Unit = "mg"
        };
        arrangeDb.Specifications.Add(spec);

        if (withOpenOrder)
        {
            var sample = new Sample
            {
                Category = SampleCategory.RawMaterial,
                ItemId = item.Id,
                ControlNumber = "CTL-" + tag,
                ReferenceNumber = "REF-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                Status = SampleStatus.Received
            };
            sample.TestOrders.Add(new TestOrder
            {
                TestCode = testCode,
                Status = ApprovalStatus.Pending,
                CurrentStep = WorkflowStep.Waiting,
                SectionId = section.Id
            });
            arrangeDb.Samples.Add(sample);
        }

        await arrangeDb.SaveChangesAsync();
        return (item.Id, spec.Id, testCode, cause.Id);
    }

    private static SpecificationMasterDataService BuildSpecService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db));

    private static TestDefinitionMasterDataService BuildTestDefService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), new AuditEventService(db, new DatabaseSequenceHelper(db)));

    private static EquipmentMasterDataService BuildEquipmentService(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db));

    [PostgresFact]
    public async Task EditSpec_WithOpenOrders_NoReason_Refused()
    {
        var (_, specId, testCode, _) = await SeedSpecWithOptionalOpenOrderAsync(withOpenOrder: true);

        await using var actDb = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            BuildSpecService(actDb).UpdateSpecificationAsync(
                _fixture.SeededUserId,
                specId,
                new UpdateSpecificationRequest(
                    TestCode: testCode,
                    ParameterName: "Appearance Updated",
                    LimitType: LimitType.Range,
                    LowerLimit: 2m,
                    UpperLimit: 12m,
                    LowerInclusive: true,
                    UpperInclusive: true,
                    Unit: "mg"),
                changeReason: null));

        Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
    }

    [PostgresFact]
    public async Task EditSpec_WithOpenOrders_WithReason_Saved_ReasonOnAuditRow()
    {
        const string expectedReason = "Correction approved under CAPA-2026-01";
        var (_, specId, testCode, _) = await SeedSpecWithOptionalOpenOrderAsync(withOpenOrder: true);

        await using (var actDb = _fixture.CreateDbContext())
        {
            await BuildSpecService(actDb).UpdateSpecificationAsync(
                _fixture.SeededUserId,
                specId,
                new UpdateSpecificationRequest(
                    TestCode: testCode,
                    ParameterName: "Appearance",
                    LimitType: LimitType.Range,
                    LowerLimit: 2.5m,
                    UpperLimit: 11.5m,
                    LowerInclusive: true,
                    UpperInclusive: true,
                    Unit: "mg"),
                changeReason: expectedReason);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var spec = await assertDb.Specifications.AsNoTracking().SingleAsync(s => s.Id == specId);
        Assert.Equal(2.5m, spec.LowerLimit);
        Assert.Equal(11.5m, spec.UpperLimit);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Specification) && a.EntityId == specId.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstAsync();
        Assert.Equal(expectedReason, audit.Reason);
    }

    [PostgresFact]
    public async Task EditSpec_NoOpenOrders_NoReason_Saved()
    {
        var (_, specId, testCode, _) = await SeedSpecWithOptionalOpenOrderAsync(withOpenOrder: false);

        await using (var actDb = _fixture.CreateDbContext())
        {
            await BuildSpecService(actDb).UpdateSpecificationAsync(
                _fixture.SeededUserId,
                specId,
                new UpdateSpecificationRequest(
                    TestCode: testCode,
                    ParameterName: "Appearance",
                    LimitType: LimitType.Range,
                    LowerLimit: 3m,
                    UpperLimit: 9m,
                    LowerInclusive: true,
                    UpperInclusive: true,
                    Unit: "mg"),
                changeReason: null);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var spec = await assertDb.Specifications.AsNoTracking().SingleAsync(s => s.Id == specId);
        Assert.Equal(3m, spec.LowerLimit);
        Assert.Equal(9m, spec.UpperLimit);
    }

    [PostgresFact]
    public async Task EditSpec_ClientClaimsNoOpenWork_ServerStillRequiresReason()
    {
        var (_, specId, testCode, _) = await SeedSpecWithOptionalOpenOrderAsync(withOpenOrder: true);
        var (_, username) = await SeedAdminAsync();
        var (app, client) = await StartHostAsync();
        await using (app)
        {
            var token = await LoginAsync(client, username);
            using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/masterdata/specifications/{specId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // Client tries to spoof an impact/no-open-work flag and omits X-Change-Reason.
            req.Content = JsonContent.Create(new
            {
                testCode,
                parameterName = "Appearance",
                limitType = LimitType.Range,
                lowerLimit = 2m,
                upperLimit = 8m,
                lowerInclusive = true,
                upperInclusive = true,
                unit = "mg",
                openWorkCount = 0,
                skipReason = true
            });

            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var fieldErrors = json.RootElement.GetProperty("fieldErrors");
            Assert.Contains(fieldErrors.EnumerateArray(), e => e.GetProperty("field").GetString() == "reason");
        }
    }

    [PostgresFact]
    public async Task Reason9Chars_Refused()
    {
        var (_, specId, testCode, _) = await SeedSpecWithOptionalOpenOrderAsync(withOpenOrder: true);

        await using var actDb = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            BuildSpecService(actDb).UpdateSpecificationAsync(
                _fixture.SeededUserId,
                specId,
                new UpdateSpecificationRequest(
                    TestCode: testCode,
                    ParameterName: "Appearance",
                    LimitType: LimitType.Range,
                    LowerLimit: 2m,
                    UpperLimit: 10m,
                    LowerInclusive: true,
                    UpperInclusive: true,
                    Unit: "mg"),
                changeReason: "123456789"));

        Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
    }

    [PostgresFact]
    public async Task ReasonWhitespacePadded_TrimmedAndCounted()
    {
        var (_, specId, testCode, _) = await SeedSpecWithOptionalOpenOrderAsync(withOpenOrder: true);

        // 5 spaces + 9 chars + 5 spaces -> 9 chars after trim -> refused.
        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                BuildSpecService(actDb).UpdateSpecificationAsync(
                    _fixture.SeededUserId,
                    specId,
                    new UpdateSpecificationRequest(
                        TestCode: testCode,
                        ParameterName: "Appearance",
                        LimitType: LimitType.Range,
                        LowerLimit: 2m,
                        UpperLimit: 10m,
                        LowerInclusive: true,
                        UpperInclusive: true,
                        Unit: "mg"),
                    changeReason: "     123456789     "));
            Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
        }

        // Leading/trailing spaces around 10+ chars -> trimmed on audit row.
        await using (var actDb = _fixture.CreateDbContext())
        {
            await BuildSpecService(actDb).UpdateSpecificationAsync(
                _fixture.SeededUserId,
                specId,
                new UpdateSpecificationRequest(
                    TestCode: testCode,
                    ParameterName: "Appearance",
                    LimitType: LimitType.Range,
                    LowerLimit: 2m,
                    UpperLimit: 10m,
                    LowerInclusive: true,
                    UpperInclusive: true,
                    Unit: "mg"),
                changeReason: "   1234567890   ");
        }

        await using var assertDb = _fixture.CreateDbContext();
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Specification) && a.EntityId == specId.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstAsync();
        Assert.Equal("1234567890", audit.Reason);
    }

    [PostgresFact]
    public async Task FreezeTest_NoReason_Refused()
    {
        var tag = Tag();
        int tdId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var td = new TestDefinition
            {
                Code = "FZ-" + tag,
                DisplayName = "Freeze test " + tag,
                SectionId = section.Id,
                WorkflowType = WorkflowType.Measurement
            };
            arrangeDb.TestDefinitions.Add(td);
            await arrangeDb.SaveChangesAsync();
            tdId = td.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            BuildTestDefService(actDb).FreezeTestDefinitionAsync(_fixture.SeededUserId, tdId, changeReason: null));
        Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
    }

    [PostgresFact]
    public async Task UnfreezeTest_NoReason_Refused()
    {
        var tag = Tag();
        int tdId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var td = new TestDefinition
            {
                Code = "UF-" + tag,
                DisplayName = "Unfreeze test " + tag,
                SectionId = section.Id,
                WorkflowType = WorkflowType.Measurement,
                IsActive = false
            };
            arrangeDb.TestDefinitions.Add(td);
            await arrangeDb.SaveChangesAsync();
            tdId = td.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            BuildTestDefService(actDb).UnfreezeTestDefinitionAsync(_fixture.SeededUserId, tdId, changeReason: "  short  "));
        Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
    }

    [PostgresFact]
    public async Task FreezeItem_NoReason_Refused()
    {
        var tag = Tag();
        int itemId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var item = new Item
            {
                Code = "FI-" + tag,
                Name = "Freeze Item " + tag,
                Category = SampleCategory.RawMaterial
            };
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
            new ItemService(actDb).SetActiveAsync(itemId, isActive: false, changeReason: null, currentUserId: _fixture.SeededUserId));
        Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
    }

    [PostgresFact]
    public async Task EquipmentEdit_WithIncubationInProgress_RequiresReason()
    {
        var tag = Tag();
        int equipmentId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var section = TestServiceFactory.EnsureMicroSection(arrangeDb);
            var equipment = new Equipment
            {
                Code = "EQ-" + tag,
                Name = "Incubator " + tag,
                Type = EquipmentType.Incubator,
                SectionId = section.Id
            };
            arrangeDb.Equipment.Add(equipment);

            var cause = new CauseOfTesting { Name = "Routine-" + tag };
            arrangeDb.CausesOfTesting.Add(cause);
            await arrangeDb.SaveChangesAsync();

            var sample = new Sample
            {
                Category = SampleCategory.Water,
                ControlNumber = "CTL-" + tag,
                ReferenceNumber = "REF-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                Status = SampleStatus.Received
            };
            var order = new TestOrder
            {
                TestCode = "TAMC-" + tag,
                Status = ApprovalStatus.Pending,
                CurrentStep = WorkflowStep.Incubating,
                SectionId = section.Id
            };
            sample.TestOrders.Add(order);
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.Incubations.Add(new Incubation
            {
                TestOrderId = order.Id,
                IncubatorEquipmentId = equipment.Id,
                StepNumber = 1,
                StepName = "Incubation",
                StartedAt = DateTime.UtcNow,
                CompletedAt = null
            });
            await arrangeDb.SaveChangesAsync();
            equipmentId = equipment.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                BuildEquipmentService(actDb).UpdateEquipmentAsync(
                    _fixture.SeededUserId,
                    equipmentId,
                    new UpdateEquipmentRequest("Incubator Renamed " + tag, "EQ-" + tag, EquipmentType.Incubator, "Room 2", 32.5m, null),
                    changeReason: null));
            Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
        }

        const string eqReason = "Calibrated probe offset updated under CR-42";
        await using (var actDb = _fixture.CreateDbContext())
        {
            await BuildEquipmentService(actDb).UpdateEquipmentAsync(
                _fixture.SeededUserId,
                equipmentId,
                new UpdateEquipmentRequest("Incubator Renamed " + tag, "EQ-" + tag, EquipmentType.Incubator, "Room 2", 32.5m, null),
                changeReason: eqReason);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var eq = await assertDb.Equipment.AsNoTracking().SingleAsync(e => e.Id == equipmentId);
        Assert.Equal("Incubator Renamed " + tag, eq.Name);
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Equipment) && a.EntityId == equipmentId.ToString() && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstAsync();
        Assert.Equal(eqReason, audit.Reason);
    }

    [PostgresFact]
    public async Task Validate_ReturnsFieldErrors_SavesNothing()
    {
        var tag = Tag();
        int itemId;
        int sectionId;
        int auditCountBefore;
        int specCountBefore;
        int testDefCountBefore;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            sectionId = TestServiceFactory.EnsureMicroSection(arrangeDb).Id;
            var item = new Item { Code = "VL-" + tag, Name = "Validate Item " + tag, Category = SampleCategory.RawMaterial };
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();
            itemId = item.Id;

            auditCountBefore = await arrangeDb.AuditLogs.CountAsync();
            specCountBefore = await arrangeDb.Specifications.CountAsync();
            testDefCountBefore = await arrangeDb.TestDefinitions.CountAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            // Range spec with LowerLimit > UpperLimit must fail validation and save nothing.
            var specErrors = await BuildSpecService(actDb).ValidateSpecificationAsync(
                _fixture.SeededUserId,
                new ValidateSpecificationRequest
                {
                    ItemId = itemId,
                    TestCode = "UNKNOWN-" + tag,
                    LimitType = LimitType.Range,
                    LowerLimit = 50m,
                    UpperLimit = 10m
                },
                id: null);
            Assert.NotEmpty(specErrors);

            var tdErrors = await BuildTestDefService(actDb).ValidateTestDefinitionAsync(
                _fixture.SeededUserId,
                new ValidateTestDefinitionRequest
                {
                    Code = "",
                    DisplayName = "",
                    SectionId = sectionId,
                    WorkflowType = WorkflowType.Measurement
                },
                id: null);
            Assert.Contains(tdErrors, e => e.Field == "code");

            var tdNameErrors = await BuildTestDefService(actDb).ValidateTestDefinitionAsync(
                _fixture.SeededUserId,
                new ValidateTestDefinitionRequest
                {
                    Code = "VLD-" + tag,
                    DisplayName = "",
                    SectionId = sectionId,
                    WorkflowType = WorkflowType.Measurement
                },
                id: null);
            Assert.Contains(tdNameErrors, e => e.Field == "displayName");
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(auditCountBefore, await assertDb.AuditLogs.CountAsync());
        Assert.Equal(specCountBefore, await assertDb.Specifications.CountAsync());
        Assert.Equal(testDefCountBefore, await assertDb.TestDefinitions.CountAsync());
    }

    [PostgresFact]
    public async Task ExistingClientsWithoutHeader_EditWithNoOpenWork_StillWork()
    {
        var (_, specId, testCode, _) = await SeedSpecWithOptionalOpenOrderAsync(withOpenOrder: false);
        var (_, username) = await SeedAdminAsync();
        var (app, client) = await StartHostAsync();
        await using (app)
        {
            var token = await LoginAsync(client, username);
            using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/masterdata/specifications/{specId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // No X-Change-Reason header sent.
            req.Content = JsonContent.Create(new
            {
                testCode,
                parameterName = "Appearance",
                limitType = LimitType.Range,
                lowerLimit = 1.5m,
                upperLimit = 9.5m,
                lowerInclusive = true,
                upperInclusive = true,
                unit = "mg"
            });

            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var spec = await assertDb.Specifications.AsNoTracking().SingleAsync(s => s.Id == specId);
        Assert.Equal(1.5m, spec.LowerLimit);
        Assert.Equal(9.5m, spec.UpperLimit);
    }

    [PostgresFact]
    public async Task CorsPreflight_AllowsChangeReasonHeader()
    {
        var (app, client) = await StartHostAsync();
        await using (app)
        {
            using var req = new HttpRequestMessage(HttpMethod.Options, "/api/masterdata/specifications/1");
            req.Headers.Add("Origin", "http://localhost:5173");
            req.Headers.Add("Access-Control-Request-Method", "PUT");
            req.Headers.Add("Access-Control-Request-Headers", ChangeReasonHeader.HeaderName);

            var res = await client.SendAsync(req);
            Assert.True(res.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK);
            Assert.True(res.Headers.TryGetValues("Access-Control-Allow-Headers", out var allowed));
            var headerValue = string.Join(",", allowed);
            Assert.Contains(ChangeReasonHeader.HeaderName, headerValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
