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

        Assert.Equal("WithinLimits", result.Status);
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
        Assert.Equal("WithinLimits", pr.ComparisonStatus);
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

        Assert.Equal("OutOfSpecification", result.Status);
        Assert.Contains("Does not comply", result.OutcomeSummary);

        var analysis = await db.TestAnalyses
            .Include(a => a.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(a => a.TestOrderId == order.Id);

        Assert.NotNull(analysis);
        var pr = analysis.ParameterResults[0];
        Assert.Equal("Does not comply", pr.ReportedDisplay);
        Assert.Equal("OutOfSpecification", pr.ComparisonStatus);
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
}
