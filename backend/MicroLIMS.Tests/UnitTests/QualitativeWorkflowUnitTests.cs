using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class QualitativeWorkflowUnitTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (TestOrder order, Specification spec, User user, TestDefinition testDef) SeedQualitativeTest(
        MicroLimsDbContext db,
        LimitType limitType = LimitType.Qualitative,
        string specLimit = "White to off-white, round biconvex effervescent tablets")
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var fpSec = db.DocumentSections.FirstOrDefault(s => s.Code == "FP");
        if (fpSec == null)
        {
            fpSec = new DocumentSection
            {
                Name = "Finished Product Laboratory",
                Code = "FP",
                DepartmentId = section.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(fpSec);
            db.SaveChanges();
        }

        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();

        var user = new User
        {
            Username = "analyst_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Test Analyst",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id
        });
        db.SaveChanges();

        var testDef = new TestDefinition
        {
            Code = "APPEARANCE_" + Guid.NewGuid().ToString("N")[..6],
            DisplayName = "Appearance Test",
            SectionId = fpSec.Id,
            WorkflowType = WorkflowType.Qualitative,
            EquationType = EquationType.Qualitative,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);
        db.SaveChanges();

        var item = new Item
        {
            Code = "ITEM_" + Guid.NewGuid().ToString("N")[..6],
            Name = "Effervescent Tablets",
            Category = SampleCategory.FinishedProduct,
            IsActive = true
        };
        db.Items.Add(item);
        db.SaveChanges();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Appearance",
            LimitType = limitType,
            SpecLimit = specLimit,
            DisplayOrder = 1
        };
        db.Specifications.Add(spec);

        var cause = new CauseOfTesting { Name = "Release", IsActive = true };
        db.CausesOfTesting.Add(cause);
        db.SaveChanges();

        var sample = new Sample
        {
            ReferenceNumber = "SMP_" + Guid.NewGuid().ToString("N")[..6],
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            ReceivedAt = DateTime.UtcNow,
            CauseOfTesting = cause,
            ReceivedByUserId = user.Id,
            Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = fpSec.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        return (order, spec, user, testDef);
    }

    [Fact]
    public async Task Conforms_GeneratesWithinLimitsAndComplies()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new QualitativePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: true, Observation: "White round tablets, citrus odour")
            },
            Password: "Password123!",
            Comment: "Appearance conforms");

        var result = await engine.RecordQualitativeResultAsync(order.Id, payload, user.Id);

        Assert.Equal(ResultStatus.WithinLimits, result.Status);
        Assert.Contains("Complies", result.OutcomeSummary);

        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);

        Assert.NotNull(analysis);
        Assert.Equal(WorkflowType.Qualitative, analysis.AnalysisType);
        Assert.Single(analysis.ParameterResults);

        var pr = analysis.ParameterResults[0];
        Assert.Null(pr.ReportedValue);
        Assert.Equal("Complies", pr.ReportedDisplay);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);
        Assert.Equal("White to off-white, round biconvex effervescent tablets", pr.SpecLimit);

        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        Assert.True(calc.RootElement.GetProperty("conforms").GetBoolean());
        Assert.Equal("White to off-white, round biconvex effervescent tablets", calc.RootElement.GetProperty("expected").GetString());

        Assert.Single(pr.Readings);
        var reading = pr.Readings[0];
        Assert.Equal(ReadingKind.Replicate, reading.Kind);
        Assert.Equal(1, reading.Index);
        Assert.Equal("White round tablets, citrus odour", reading.Text);
        Assert.True(reading.Passed);
    }

    [Fact]
    public async Task DoesNotConform_GeneratesOutOfSpecification()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new QualitativePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: false, Observation: "Yellow discolouration with brown spots")
            },
            Password: "Password123!",
            Comment: "Appearance failed");

        var result = await engine.RecordQualitativeResultAsync(order.Id, payload, user.Id);

        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
        Assert.Contains("Does not comply", result.OutcomeSummary);

        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);

        Assert.NotNull(analysis);
        var pr = analysis.ParameterResults[0];
        Assert.Equal("Does not comply", pr.ReportedDisplay);
        Assert.Equal(ResultStatus.OutOfSpecification, pr.ComparisonStatus);
        Assert.False(pr.Readings[0].Passed);
        Assert.Equal("Yellow discolouration with brown spots", pr.Readings[0].Text);
    }

    [Fact]
    public async Task DoesNotConform_WithoutObservationText_Refused()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Null observation
        var payload1 = new QualitativePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: false, Observation: null)
            },
            Password: "Password123!");

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordQualitativeResultAsync(order.Id, payload1, user.Id));
        Assert.Contains("An observation is required when result does not conform", ex1.Message);

        // Empty/whitespace observation
        var payload2 = new QualitativePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: false, Observation: "   ")
            },
            Password: "Password123!");

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordQualitativeResultAsync(order.Id, payload2, user.Id));
        Assert.Contains("An observation is required when result does not conform", ex2.Message);
    }

    [Fact]
    public async Task NonQualitativeSpec_Refused()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db, limitType: LimitType.Range, specLimit: "90-110");
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new QualitativePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: true, Observation: "Ok")
            },
            Password: "Password123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordQualitativeResultAsync(order.Id, payload, user.Id));
        Assert.Contains("must have limit type Qualitative or PresenceAbsence", ex.Message);
    }

    [Fact]
    public async Task ObservationExceeding500Chars_Refused()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var longText = new string('A', 501);
        var payload = new QualitativePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: false, Observation: longText)
            },
            Password: "Password123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordQualitativeResultAsync(order.Id, payload, user.Id));
        Assert.Contains("Observation cannot exceed 500 characters", ex.Message);
    }

    [Fact]
    public async Task SpecLimitSnapshot_UnchangedWhenSpecEditedLater()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db, specLimit: "Original Expected Description");
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new QualitativePayload(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: true, Observation: "Matched original")
            },
            Password: "Password123!");

        await engine.RecordQualitativeResultAsync(order.Id, payload, user.Id);

        // Now modify the specification
        spec.SpecLimit = "Modified Description Later";
        await db.SaveChangesAsync();

        var pr = await db.ParameterResults.FirstOrDefaultAsync(p => p.SpecificationId == spec.Id);
        Assert.NotNull(pr);
        // Stored snapshot should be the original
        Assert.Equal("Original Expected Description", pr.SpecLimit);
    }

    private static (TestOrder order, Specification spec, User user) SeedPerContainer(MicroLimsDbContext db)
    {
        var (order, spec, user, testDef) = SeedQualitativeTest(db);
        testDef.PerContainer = true;
        db.SaveChanges();
        return (order, spec, user);
    }

    private static QualitativePayload PerContainerPayload(
        Specification spec, int? count, List<QualitativeContainerInput>? containers, bool paramConforms = true) =>
        new(
            AnalysedAt: DateTime.UtcNow,
            EquipmentId: null,
            Parameters: new List<QualitativeParameterInput>
            {
                new(spec.Id, Conforms: paramConforms, Observation: null, Containers: containers)
            },
            Password: "Password123!",
            ContainerCount: count);

    private static List<QualitativeContainerInput> AllConform(int n) =>
        Enumerable.Range(1, n).Select(i => new QualitativeContainerInput(i, true, $"Drum {i} white powder")).ToList();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CurrentStep_CarriesPerContainerFlag(bool perContainer)
    {
        using var db = NewDb();
        var (order, _, _, testDef) = SeedQualitativeTest(db);
        testDef.PerContainer = perContainer;
        db.SaveChanges();

        var view = new MicroLIMS.Application.Services.CurrentStepViewService(db, TestServiceFactory.TestWorkflow(db));
        var data = await view.GetAsync(order.Id);

        var prop = data.GetType().GetProperty("perContainer");
        Assert.NotNull(prop);
        Assert.Equal(perContainer, (bool)prop!.GetValue(data)!);
    }

    [Fact]
    public async Task PerContainer_AllComply()
    {
        using var db = NewDb();
        var (order, spec, user) = SeedPerContainer(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Client-sent parameter-level Conforms=false is ignored; the conclusion is derived.
        var result = await engine.RecordQualitativeResultAsync(
            order.Id, PerContainerPayload(spec, 3, AllConform(3), paramConforms: false), user.Id);

        Assert.Equal(ResultStatus.WithinLimits, result.Status);

        var pr = await db.ParameterResults.Include(p => p.Readings).SingleAsync(p => p.TestOrderId == order.Id);
        Assert.Equal("All 3 containers comply", pr.ReportedDisplay);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);
        Assert.Equal(3, pr.Readings.Count);
        foreach (var r in pr.Readings)
        {
            Assert.Equal(ReadingKind.Replicate, r.Kind);
            Assert.True(r.Passed);
            Assert.Equal($"Drum {r.Index} white powder", r.Text);
        }
        Assert.Equal(new[] { 1, 2, 3 }, pr.Readings.Select(r => r.Index).OrderBy(i => i).ToArray());

        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        Assert.True(calc.RootElement.GetProperty("conforms").GetBoolean());
        Assert.Equal(3, calc.RootElement.GetProperty("containerCount").GetInt32());
    }

    [Fact]
    public async Task PerContainer_OneFails()
    {
        using var db = NewDb();
        var (order, spec, user) = SeedPerContainer(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var containers = AllConform(4);
        containers[1] = new QualitativeContainerInput(2, false, "Lumps");

        var result = await engine.RecordQualitativeResultAsync(
            order.Id, PerContainerPayload(spec, 4, containers), user.Id);

        Assert.Equal(ResultStatus.OutOfSpecification, result.Status);
        var pr = await db.ParameterResults.Include(p => p.Readings).SingleAsync(p => p.TestOrderId == order.Id);
        Assert.Equal("Container 2 of 4 does not comply", pr.ReportedDisplay);
        Assert.Equal(ResultStatus.OutOfSpecification, pr.ComparisonStatus);
        Assert.Equal("Lumps", pr.Readings.Single(r => r.Index == 2).Text);
        Assert.False(pr.Readings.Single(r => r.Index == 2).Passed);

        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        Assert.False(calc.RootElement.GetProperty("conforms").GetBoolean());
        Assert.Equal(new[] { 2 }, calc.RootElement.GetProperty("failedContainers").EnumerateArray().Select(e => e.GetInt32()).ToArray());
    }

    [Fact]
    public async Task PerContainer_TwoFail()
    {
        using var db = NewDb();
        var (order, spec, user) = SeedPerContainer(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var containers = AllConform(4);
        containers[1] = new QualitativeContainerInput(2, false, "Lumps");
        containers[3] = new QualitativeContainerInput(4, false, "Yellow");

        await engine.RecordQualitativeResultAsync(order.Id, PerContainerPayload(spec, 4, containers), user.Id);

        var pr = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id);
        Assert.Equal("Containers 2, 4 of 4 do not comply", pr.ReportedDisplay);
        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        Assert.Equal(new[] { 2, 4 }, calc.RootElement.GetProperty("failedContainers").EnumerateArray().Select(e => e.GetInt32()).ToArray());
    }

    private const string CountMsg = "Enter the number of containers received (1 to 200).";
    private const string EachMsg = "Record a result for each container 1 to 3.";

    [Theory]
    [InlineData(null, "ok3", CountMsg)]
    [InlineData(0, "ok3", CountMsg)]
    [InlineData(201, "ok3", CountMsg)]
    [InlineData(3, "none", EachMsg)]
    [InlineData(3, "short", EachMsg)]
    [InlineData(3, "duplicate", EachMsg)]
    [InlineData(3, "outOfRange", EachMsg)]
    [InlineData(3, "failNoText", "Describe what container 2 shows.")]
    public async Task PerContainer_Refused_WritesNothing(int? count, string shape, string expected)
    {
        using var db = NewDb();
        var (order, spec, user) = SeedPerContainer(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        List<QualitativeContainerInput>? containers = shape switch
        {
            "none" => null,
            "short" => AllConform(2),
            "duplicate" => new() { new(1, true, null), new(1, true, null), new(3, true, null) },
            "outOfRange" => new() { new(1, true, null), new(2, true, null), new(4, true, null) },
            "failNoText" => new() { new(1, true, null), new(2, false, "  "), new(3, true, null) },
            _ => AllConform(3)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordQualitativeResultAsync(order.Id, PerContainerPayload(spec, count, containers), user.Id));
        Assert.Equal(expected, ex.Message);
        Assert.Empty(db.TestAnalyses.Where(a => a.TestOrderId == order.Id));
        Assert.Empty(db.ParameterResults.Where(p => p.TestOrderId == order.Id));
    }

    [Fact]
    public async Task NotPerContainer_WithContainers_Refused()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordQualitativeResultAsync(order.Id, PerContainerPayload(spec, 2, AllConform(2)), user.Id));
        Assert.Equal("This test is not recorded per container.", ex.Message);
        Assert.Empty(db.TestAnalyses.Where(a => a.TestOrderId == order.Id));
    }

    [Fact]
    public async Task PerContainer_ManyFailures_DisplayFitsColumnAndFullListKept()
    {
        using var db = NewDb();
        var (order, spec, user) = SeedPerContainer(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // ParameterResult.ReportedDisplay is varchar(100): 30 failing of 200 must not overflow.
        var containers = Enumerable.Range(1, 200)
            .Select(i => new QualitativeContainerInput(i, i > 30, i <= 30 ? "Lumps" : null)).ToList();

        await engine.RecordQualitativeResultAsync(order.Id, PerContainerPayload(spec, 200, containers), user.Id);

        var pr = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id);
        Assert.True(pr.ReportedDisplay.Length <= 100);
        Assert.Equal("30 of 200 containers do not comply", pr.ReportedDisplay);
        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        Assert.Equal(30, calc.RootElement.GetProperty("failedContainers").GetArrayLength());
    }

    [Theory]
    [InlineData(true, "The container complies")]
    [InlineData(false, "The container does not comply")]
    public async Task PerContainer_SingleContainer_Wording(bool conforms, string expected)
    {
        using var db = NewDb();
        var (order, spec, user) = SeedPerContainer(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.RecordQualitativeResultAsync(order.Id, PerContainerPayload(spec, 1,
            new() { new(1, conforms, conforms ? null : "Lumps") }), user.Id);

        var pr = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id);
        Assert.Equal(expected, pr.ReportedDisplay);
    }

    [Fact]
    public async Task PerContainer_AllComply_StoresEmptyFailedList()
    {
        using var db = NewDb();
        var (order, spec, user) = SeedPerContainer(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.RecordQualitativeResultAsync(order.Id, PerContainerPayload(spec, 2, AllConform(2)), user.Id);

        var pr = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id);
        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        Assert.Equal(0, calc.RootElement.GetProperty("failedContainers").GetArrayLength());
    }

    [Fact]
    public async Task NotPerContainer_StoredJsonHasNoContainerFields()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        await engine.RecordQualitativeResultAsync(order.Id, new QualitativePayload(
            DateTime.UtcNow, null,
            new List<QualitativeParameterInput> { new(spec.Id, true, "Ok") },
            "Password123!"), user.Id);

        var pr = await db.ParameterResults.SingleAsync(p => p.TestOrderId == order.Id);
        using var calc = JsonDocument.Parse(pr.CalculationJson!);
        Assert.False(calc.RootElement.TryGetProperty("containerCount", out _));
        Assert.False(calc.RootElement.TryGetProperty("failedContainers", out _));
    }

    [Fact]
    public async Task NotPerContainer_WithContainerCountOnly_Refused()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordQualitativeResultAsync(order.Id, PerContainerPayload(spec, 3, null), user.Id));
        Assert.Equal("This test is not recorded per container.", ex.Message);
        Assert.Empty(db.TestAnalyses.Where(a => a.TestOrderId == order.Id));
    }

    [Fact]
    public async Task NotPerContainer_EmptyContainersWithoutCount_Tolerated()
    {
        using var db = NewDb();
        var (order, spec, user, _) = SeedQualitativeTest(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var result = await engine.RecordQualitativeResultAsync(order.Id,
            PerContainerPayload(spec, null, new List<QualitativeContainerInput>()), user.Id);
        Assert.Equal(ResultStatus.WithinLimits, result.Status);
    }

    [Fact]
    public async Task PerContainer_LongParameterLevelObservation_Refused()
    {
        using var db = NewDb();
        var (order, spec, user) = SeedPerContainer(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var payload = new QualitativePayload(
            DateTime.UtcNow, null,
            new List<QualitativeParameterInput> { new(spec.Id, true, new string('A', 501), AllConform(2)) },
            "Password123!", ContainerCount: 2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecordQualitativeResultAsync(order.Id, payload, user.Id));
        Assert.Equal("Observation cannot exceed 500 characters.", ex.Message);
        Assert.Empty(db.TestAnalyses.Where(a => a.TestOrderId == order.Id));
    }
}
