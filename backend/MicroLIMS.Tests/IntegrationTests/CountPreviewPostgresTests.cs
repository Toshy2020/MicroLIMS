using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// POST api/test-workflow/{testOrderId}/preview-count-result: the count a save
// would report, from the same calculator, with nothing written. Driven through
// TestWorkflowController so the section-scope check and the response shape are
// the real ones.
[Collection("PostgresDatabaseCollection")]
public class CountPreviewPostgresTests
{
    private const string StepName = "CountIncubation";
    private readonly PostgresTestFixture _fixture;

    public CountPreviewPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string UniqueCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // A water sample with alert 10 / action 50 / spec 100 whose single count step is incubating.
    private async Task<int> ArrangeIncubatingWaterOrderAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var sectionId = _fixture.SeededSectionId;
        var testCode = UniqueCode("TAMCPV");

        var definition = new TestDefinition { Code = testCode, DisplayName = "Count test (preview)", WorkflowType = WorkflowType.CountTest, SectionId = sectionId };
        db.TestDefinitions.Add(definition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = definition.Id, StepOrder = 1, StepName = StepName,
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(step);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = UniqueCode("LOT-"), ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = UniqueCode("TSA"),
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram, SectionId = sectionId
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = step.Id, MediaProductId = MediaProductTestData.ProductIdFor(db, material.Id), TempMin = 30, TempMax = 35, IncubationMinHours = 72, IncubationMaxHours = 120 });
        var media = new Media { MaterialId = material.Id, LotNumber = UniqueCode("TSA/"), IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);
        var incubator = new Equipment { Name = "Incubator", Code = UniqueCode("INC-"), Type = EquipmentType.Incubator, SetPointTemperature = 32, SectionId = sectionId };
        db.Equipment.Add(incubator);
        var cause = new CauseOfTesting { Name = UniqueCode("Routine ") };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var point = new WaterSamplingPoint { Code = UniqueCode("WP-"), Location = "Utility Room", AssignedTestCodes = new() { testCode } };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();
        db.SamplingConfigurations.Add(new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id, TestCode = testCode, AlertLimit = "10", ActionLimit = "50", SpecLimit = "100", Unit = ""
        });

        var sample = new Sample
        {
            Category = SampleCategory.Water, ControlNumber = UniqueCode("CTRL-"), ReferenceNumber = UniqueCode("REF-"),
            CauseOfTestingId = cause.Id, ReceivedByUserId = _fixture.SeededUserId, Status = SampleStatus.Received,
            WaterSamplingPointId = point.Id
        };
        var order = new TestOrder { TestCode = testCode, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, SectionId = sectionId };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        // Media selection opens the incubation the save would close; not under test.
        var engine = TestServiceFactory.TestWorkflow(db);
        await engine.SelectMediaAsync(order.Id, StepName, media.Id, incubator.Id, _fixture.SeededUserId);
        return order.Id;
    }

    // An Analyst who belongs to a laboratory section other than the order's.
    private async Task<int> ArrangeOtherSectionAnalystAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var uid = UniqueCode("");
        var section = new DocumentSection
        {
            Name = $"Preview Other Section {uid}", Code = $"POS-{uid}", DepartmentId = _fixture.SeededDepartmentId, IsActive = true
        };
        db.DocumentSections.Add(section);
        var role = await db.Roles.OrderBy(r => r.Id).FirstAsync(r => r.Type == RoleType.Analyst);
        var user = new User { FullName = $"Other Section Analyst {uid}", Username = $"other_{uid}", PasswordHash = "hashed_dummy", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = _fixture.SeededDepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static TestWorkflowController CreateController(MicroLimsDbContext db, int userId, RoleType role)
    {
        var engine = TestServiceFactory.TestWorkflow(db);
        var eligibility = TestServiceFactory.IncubatorEligibility(db);
        var snapshot = TestServiceFactory.AppearanceSnapshot(db);
        return new TestWorkflowController(engine, new TestWorkflowQueryService(db, eligibility, snapshot), new UserSectionScopeService(db),
            new GroupedTestActionService(db, engine, eligibility), new CurrentStepViewService(db, engine))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, role.ToString())
                    }, "TestAuth"))
                }
            }
        };
    }

    private sealed record Previewed(string Reported, decimal? Average, decimal? Calculated, ResultStatus Status, IReadOnlyList<string> Messages);

    private static T Prop<T>(object data, string name) =>
        (T)data.GetType().GetProperty(name)!.GetValue(data)!;

    private static Previewed ReadPreview(IActionResult actionResult)
    {
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        var data = response.Data!;
        return new Previewed(
            Prop<string>(data, "reported"),
            data.GetType().GetProperty("average")!.GetValue(data) as decimal?,
            data.GetType().GetProperty("calculated")!.GetValue(data) as decimal?,
            Prop<ResultStatus>(data, "status"),
            Prop<IReadOnlyList<string>>(data, "messages"));
    }

    private async Task<long> MaxAuditIdAsync()
    {
        await using var db = _fixture.CreateDbContext();
        return await db.AuditLogs.AsNoTracking().Select(a => (long?)a.Id).MaxAsync() ?? 0;
    }

    // Preview and save of the same readings, through the same controller, must agree to the character.
    [PostgresFact]
    public async Task Preview_ReturnsSameReportedValueAsSubsequentSave()
    {
        await PostgresCases.RunAsync(new[]
        {
            new[] { "12", "13" },      // midpoint: away from zero, not banker's
            new[] { "100.4" },         // reported "100" but status on the unrounded value
            new[] { "0", "1" },        // below the limit
            new[] { "12", "TNTC", "14" },
            new[] { "5", "7", "8" },   // repeating average
        }, async readings =>
        {
            var orderId = await ArrangeIncubatingWaterOrderAsync();
            var request = new RecordTestResultRequest(StepName, null, 1m, readings.ToList());

            Previewed previewed;
            await using (var previewDb = _fixture.CreateDbContext())
            {
                previewed = ReadPreview(await CreateController(previewDb, _fixture.SeededUserId, RoleType.Analyst).PreviewCountResult(orderId, request));
            }

            await using (var saveDb = _fixture.CreateDbContext())
            {
                Assert.IsType<OkObjectResult>(await CreateController(saveDb, _fixture.SeededUserId, RoleType.Analyst).RecordResult(orderId, request));
            }

            await using var assertDb = _fixture.CreateDbContext();
            var saved = await assertDb.CountTestReadings.AsNoTracking().SingleAsync(r => r.TestOrderId == orderId);
            Assert.Equal(saved.ReportedResult, previewed.Reported);
            Assert.Equal(saved.Average, previewed.Average);
            Assert.Equal(saved.CalculatedResult, previewed.Calculated);
            Assert.Equal(saved.Status, previewed.Status);
            Assert.Empty(previewed.Messages);
        });
    }

    [PostgresFact]
    public async Task Preview_PersistsNothing()
    {
        var orderId = await ArrangeIncubatingWaterOrderAsync();
        var auditBefore = await MaxAuditIdAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var previewed = ReadPreview(await CreateController(actDb, _fixture.SeededUserId, RoleType.Analyst)
                .PreviewCountResult(orderId, new RecordTestResultRequest(StepName, null, 1m, new List<string> { "60", "62" })));
            Assert.Equal("61 CFU/mL", previewed.Reported);
            Assert.Equal(ResultStatus.ActionLimitExceeded, previewed.Status);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.CountTestReadings.AsNoTracking().AnyAsync(r => r.TestOrderId == orderId));
        Assert.False(await assertDb.Results.AsNoTracking().AnyAsync(r => r.TestOrderId == orderId));
        Assert.False(await assertDb.ResultRecords.AsNoTracking().AnyAsync(r => r.TestOrderId == orderId));
        Assert.Equal(0, await assertDb.AuditLogs.AsNoTracking().CountAsync(a => a.Id > auditBefore));
        Assert.Equal(WorkflowStep.Incubating, (await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId)).CurrentStep);
        Assert.Null((await assertDb.Incubations.AsNoTracking().SingleAsync(i => i.TestOrderId == orderId)).CompletedAt);
    }

    [PostgresFact]
    public async Task Preview_OtherSection_Forbidden()
    {
        var orderId = await ArrangeIncubatingWaterOrderAsync();
        var outsiderId = await ArrangeOtherSectionAnalystAsync();
        var auditBefore = await MaxAuditIdAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateController(actDb, outsiderId, RoleType.Analyst)
                    .PreviewCountResult(orderId, new RecordTestResultRequest(StepName, null, 1m, new List<string> { "12" })));
        }

        // Same request from a member of the order's section is accepted.
        await using (var contrastDb = _fixture.CreateDbContext())
        {
            var previewed = ReadPreview(await CreateController(contrastDb, _fixture.SeededUserId, RoleType.Analyst)
                .PreviewCountResult(orderId, new RecordTestResultRequest(StepName, null, 1m, new List<string> { "12" })));
            Assert.Equal("12 CFU/mL", previewed.Reported);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.CountTestReadings.AsNoTracking().AnyAsync(r => r.TestOrderId == orderId));
        Assert.Equal(0, await assertDb.AuditLogs.AsNoTracking().CountAsync(a => a.Id > auditBefore));
    }

    [PostgresFact]
    public async Task Preview_InvalidReadings_ReturnsFieldErrors()
    {
        var orderId = await ArrangeIncubatingWaterOrderAsync();
        var auditBefore = await MaxAuditIdAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var controller = CreateController(actDb, _fixture.SeededUserId, RoleType.Analyst);

            // Two bad readings among good ones: each is reported with its row (0-based position in the readings).
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                controller.PreviewCountResult(orderId, new RecordTestResultRequest(StepName, null, 1m, new List<string> { "12", "abc", "14", "N/A" })));
            Assert.Equal(new[] { 1, 3 }, ex.FieldErrors.Select(e => e.Row!.Value).ToArray());
            Assert.All(ex.FieldErrors, e => Assert.Equal("rawPlateReadings", e.Field));
            Assert.Contains("abc", ex.FieldErrors[0].Message);

            // No readings at all.
            var empty = await Assert.ThrowsAsync<FieldValidationException>(() =>
                controller.PreviewCountResult(orderId, new RecordTestResultRequest(StepName, null, 1m, new List<string>())));
            Assert.Equal("rawPlateReadings", Assert.Single(empty.FieldErrors).Field);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.CountTestReadings.AsNoTracking().AnyAsync(r => r.TestOrderId == orderId));
        Assert.Equal(0, await assertDb.AuditLogs.AsNoTracking().CountAsync(a => a.Id > auditBefore));
    }
}
