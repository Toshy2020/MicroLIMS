using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class LocationChemistryWorkflowTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task PrepareAsync_LinksConfiguration_ForNonCountTest()
    {
        await using var db = NewDb();
        var dept = new WaterDepartment { Name = "Chemistry Lab" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();

        var point = new WaterSamplingPoint
        {
            Code = "W-01",
            Location = "Loop A Feed",
            WaterDepartmentId = dept.Id,
            AssignedTestCodes = new() { "PH" }
        };
        db.WaterSamplingPoints.Add(point);

        var testDef = new TestDefinition
        {
            Code = "PH",
            DisplayName = "pH Determination",
            WorkflowType = WorkflowType.Measurement
        };
        db.TestDefinitions.Add(testDef);

        var config = new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id,
            TestCode = "PH",
            LimitType = LimitType.Range,
            LowerLimit = 6.5m,
            UpperLimit = 8.5m,
            Unit = "pH"
        };
        db.SamplingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var refGen = new MicroLIMS.Application.Services.ReferenceNumberGenerator(db);
        var engine = new WaterWorkflowEngine(db, refGen);

        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(dept.Id, 0, "500ml", "Analyst", "CTRL-10", 1));
        var prepared = await engine.PrepareAsync(sample.Id, new List<int> { point.Id }, 1, "RoomTemperature");

        var order = Assert.Single(prepared.TestOrders);
        Assert.Equal("PH", order.TestCode);
        var location = Assert.Single(prepared.Locations);
        Assert.Equal(config.Id, location.SamplingConfigurationId);
    }

    [Fact]
    public async Task LocationResultRecorder_Range_PassAndFail()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "PH", LimitType.Range, 6.5m, 8.5m, null, "pH");
        var engine = TestServiceFactory.TestWorkflow(db);

        // loc1: 7.2 (WithinLimits), loc2: 6.0 (OutOfSpecification - below lower)
        var inputs = new List<LocationResultInput>
        {
            new(loc1.Id, 7.2m, "7.2"),
            new(loc2.Id, 6.0m, "6.0")
        };

        var result = await engine.RecordLocationResultsAsync(order.Id, inputs, 1);

        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
        Assert.Contains("1 conform, 1 alert/action/spec", result.OutcomeSummary);

        var reloadedLoc1 = await db.SampleLocations.FindAsync(loc1.Id);
        Assert.Equal(ResultStatus.WithinLimits, reloadedLoc1!.Status);
        Assert.Equal(7.2m, reloadedLoc1.CalculatedResult);
        Assert.Equal("6.5-8.5", reloadedLoc1.SpecLimit);
        Assert.Equal("pH", reloadedLoc1.Unit);

        var reloadedLoc2 = await db.SampleLocations.FindAsync(loc2.Id);
        Assert.Equal(ResultStatus.OutOfSpecification, reloadedLoc2!.Status);
        Assert.Equal(6.0m, reloadedLoc2.CalculatedResult);
    }

    [Fact]
    public async Task LocationResultRecorder_NotMoreThan_PassAndFail()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "COND", LimitType.NotMoreThan, null, 1.3m, null, "µS/cm");
        var engine = TestServiceFactory.TestWorkflow(db);

        var inputs = new List<LocationResultInput>
        {
            new(loc1.Id, 1.1m, null),
            new(loc2.Id, 1.5m, null)
        };

        var result = await engine.RecordLocationResultsAsync(order.Id, inputs, 1);

        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);

        var reloadedLoc1 = await db.SampleLocations.FindAsync(loc1.Id);
        Assert.Equal(ResultStatus.WithinLimits, reloadedLoc1!.Status);
        Assert.Equal("NMT 1.3", reloadedLoc1.SpecLimit);

        var reloadedLoc2 = await db.SampleLocations.FindAsync(loc2.Id);
        Assert.Equal(ResultStatus.OutOfSpecification, reloadedLoc2!.Status);
    }

    [Fact]
    public async Task LocationResultRecorder_NotLessThan_PassAndFail()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "ASSAY", LimitType.NotLessThan, 10m, null, null, "ppm");
        var engine = TestServiceFactory.TestWorkflow(db);

        var inputs = new List<LocationResultInput>
        {
            new(loc1.Id, 12m, null),
            new(loc2.Id, 8m, null)
        };

        var result = await engine.RecordLocationResultsAsync(order.Id, inputs, 1);

        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);

        var reloadedLoc1 = await db.SampleLocations.FindAsync(loc1.Id);
        Assert.Equal(ResultStatus.WithinLimits, reloadedLoc1!.Status);
        Assert.Equal("NLT 10", reloadedLoc1.SpecLimit);

        var reloadedLoc2 = await db.SampleLocations.FindAsync(loc2.Id);
        Assert.Equal(ResultStatus.OutOfSpecification, reloadedLoc2!.Status);
    }

    [Fact]
    public async Task LocationResultRecorder_Qualitative_PassAndFail()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "APPEARANCE", LimitType.Qualitative, null, null, "Clear colourless liquid", "");
        var engine = TestServiceFactory.TestWorkflow(db);

        var inputs = new List<LocationResultInput>
        {
            new(loc1.Id, null, "  clear colourless liquid  "), // case-insensitive trimmed
            new(loc2.Id, null, "Slightly turbid")
        };

        var result = await engine.RecordLocationResultsAsync(order.Id, inputs, 1);

        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);

        var reloadedLoc1 = await db.SampleLocations.FindAsync(loc1.Id);
        Assert.Equal(ResultStatus.WithinLimits, reloadedLoc1!.Status);
        Assert.Equal("clear colourless liquid", reloadedLoc1.ReportedResult);
        Assert.Equal("Clear colourless liquid", reloadedLoc1.SpecLimit);

        var reloadedLoc2 = await db.SampleLocations.FindAsync(loc2.Id);
        Assert.Equal(ResultStatus.OutOfSpecification, reloadedLoc2!.Status);
        Assert.Equal("Slightly turbid", reloadedLoc2.ReportedResult);
    }

    [Fact]
    public async Task LocationResultRecorder_MissingInput_Refused()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "PH", LimitType.Range, 6.5m, 8.5m, null, "pH");
        var engine = TestServiceFactory.TestWorkflow(db);

        var inputs = new List<LocationResultInput>
        {
            new(loc1.Id, 7.0m, null)
            // missing loc2!
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordLocationResultsAsync(order.Id, inputs, 1));
        Assert.Contains("Results are missing for", ex.Message);
    }

    [Fact]
    public async Task LocationResultRecorder_UnconfiguredLimit_Refused()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "PH", LimitType.Range, 6.5m, 8.5m, null, "pH");

        // Clear limit config for loc2
        var config2 = await db.SamplingConfigurations.FindAsync(loc2.SamplingConfigurationId);
        config2!.LimitType = null;
        await db.SaveChangesAsync();

        var engine = TestServiceFactory.TestWorkflow(db);
        var inputs = new List<LocationResultInput>
        {
            new(loc1.Id, 7.0m, null),
            new(loc2.Id, 7.0m, null)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordLocationResultsAsync(order.Id, inputs, 1));
        Assert.Contains("Limits for PH at W-02 are not configured", ex.Message);
    }

    [Fact]
    public async Task LocationResultRecorder_TransitionsOrderToReady_And_SubmitsSampleToReview()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "PH", LimitType.Range, 6.5m, 8.5m, null, "pH");
        var engine = TestServiceFactory.TestWorkflow(db);

        var inputs = new List<LocationResultInput>
        {
            new(loc1.Id, 7.0m, null),
            new(loc2.Id, 7.5m, null)
        };

        var result = await engine.RecordLocationResultsAsync(order.Id, inputs, 1);

        Assert.Equal(ResultStatus.WithinLimits, result.Status);

        var reloadedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Ready, reloadedOrder!.CurrentStep);

        var summaryResult = await db.Results.FirstOrDefaultAsync(r => r.TestOrderId == order.Id);
        Assert.NotNull(summaryResult);
        Assert.Contains("2 locations: 2 conform, 0 alert/action/spec", summaryResult.RawValue);

        var reloadedSample = await db.Samples.FindAsync(order.SampleId);
        Assert.Equal(SampleStatus.UnderReview, reloadedSample!.Status);
    }

    [Fact]
    public async Task CurrentStepViewService_ReturnsLocationEntryAndLocations()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "PH", LimitType.Range, 6.5m, 8.5m, null, "pH");
        var engine = TestServiceFactory.TestWorkflow(db);
        var view = new CurrentStepViewService(db, engine);

        var result = await view.GetAsync(order.Id);

        var locEntryProp = result.GetType().GetProperty("locationEntry");
        Assert.NotNull(locEntryProp);
        Assert.True((bool)locEntryProp!.GetValue(result)!);

        var locationsProp = result.GetType().GetProperty("locations");
        Assert.NotNull(locationsProp);
        var list = (System.Collections.IEnumerable)locationsProp!.GetValue(result)!;
        var count = 0;
        foreach (var item in list)
        {
            count++;
            var nameProp = item.GetType().GetProperty("name");
            Assert.NotNull(nameProp);
            var limitTypeProp = item.GetType().GetProperty("limitType");
            Assert.Equal("Range", limitTypeProp!.GetValue(item));
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Controller_RecordLocationResults_ReturnsOk()
    {
        await using var db = NewDb();
        var (order, loc1, loc2) = await SeedWaterOrderAsync(db, "PH", LimitType.Range, 6.5m, 8.5m, null, "pH");
        var engine = TestServiceFactory.TestWorkflow(db);
        var queries = new TestWorkflowQueryService(db, new IncubatorEligibilityService(db), TestServiceFactory.AppearanceSnapshot(db));
        var scope = new UserSectionScopeService(db);
        var groupAction = new GroupedTestActionService(db, engine, new IncubatorEligibilityService(db));
        var view = new CurrentStepViewService(db, engine);

        var controller = new TestWorkflowController(engine, queries, scope, groupAction, view);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, "Analyst")
                }, "test"))
            }
        };

        var request = new LocationResultsRequest(
            Locations: new List<LocationResultItemRequest>
            {
                new(loc1.Id, 7.0m, "7.0"),
                new(loc2.Id, 7.5m, "7.5")
            });

        var actionResult = await controller.RecordLocationResults(order.Id, request);
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(ok.Value);
    }

    private static async Task<(TestOrder order, SampleLocation loc1, SampleLocation loc2)> SeedWaterOrderAsync(
        MicroLimsDbContext db, string testCode, LimitType limitType, decimal? lower, decimal? upper, string? expectedText, string unit)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        TestServiceFactory.AssignUserToMicroSection(db, 1);

        var cause = new CauseOfTesting { Name = "Routine Monitoring", IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var dept = new WaterDepartment { Name = "Chemistry" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();

        var point1 = new WaterSamplingPoint { Code = "W-01", Location = "Feed", WaterDepartmentId = dept.Id };
        var point2 = new WaterSamplingPoint { Code = "W-02", Location = "Return", WaterDepartmentId = dept.Id };
        db.WaterSamplingPoints.AddRange(point1, point2);

        var testDef = new TestDefinition { Code = testCode, DisplayName = testCode, WorkflowType = WorkflowType.Measurement };
        db.TestDefinitions.Add(testDef);

        var config1 = new SamplingConfiguration
        {
            WaterSamplingPointId = point1.Id,
            TestCode = testCode,
            LimitType = limitType,
            LowerLimit = lower,
            UpperLimit = upper,
            ExpectedResultText = expectedText,
            Unit = unit
        };
        var config2 = new SamplingConfiguration
        {
            WaterSamplingPointId = point2.Id,
            TestCode = testCode,
            LimitType = limitType,
            LowerLimit = lower,
            UpperLimit = upper,
            ExpectedResultText = expectedText,
            Unit = unit
        };
        db.SamplingConfigurations.AddRange(config1, config2);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "W-SAMPLE-01",
            Category = SampleCategory.Water,
            WaterDepartmentId = dept.Id,
            LabSectionId = section.Id,
            CauseOfTestingId = cause.Id,
            Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testCode,
            SectionId = section.Id,
            CurrentStep = WorkflowStep.Waiting,
            Status = ApprovalStatus.InProgress,
            AssignedAnalystId = 1
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var loc1 = new SampleLocation
        {
            SampleId = sample.Id,
            TestOrderId = order.Id,
            LocationType = LocationType.WaterSamplingPoint,
            WaterSamplingPointId = point1.Id,
            SamplingConfigurationId = config1.Id
        };
        var loc2 = new SampleLocation
        {
            SampleId = sample.Id,
            TestOrderId = order.Id,
            LocationType = LocationType.WaterSamplingPoint,
            WaterSamplingPointId = point2.Id,
            SamplingConfigurationId = config2.Id
        };
        db.SampleLocations.AddRange(loc1, loc2);
        await db.SaveChangesAsync();

        return (order, loc1, loc2);
    }
}
