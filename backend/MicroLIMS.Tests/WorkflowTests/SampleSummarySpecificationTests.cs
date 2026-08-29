using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Part 0 removed the CountTest-only restriction on configuring an Item's
// Specifications, and Part 2 (Product/RM/PM Certificate of Analysis) reads
// that Specification text generically for every TestCode on the sample -
// this covers the SampleSummaryService lookup that makes it available,
// independent of whether the underlying test is quantitative or
// qualitative.
public class SampleSummarySpecificationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // A Finished Product sample with two plain (non-located) TestOrders -
    // TAMC (quantitative, CountTestReading) and ECOLI (qualitative,
    // Result) - and a Specification configured for TAMC only, mirroring
    // "nobody has configured one for this pathogen test yet".
    private static async Task<int> SeedProductSampleAsync(MicroLimsDbContext db)
    {
        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", WorkflowType = WorkflowType.CountTest });
        db.TestDefinitions.Add(new TestDefinition { Code = "ECOLI", DisplayName = "E. coli", WorkflowType = WorkflowType.Observation });
        db.Users.Add(new User { Id = 1, FullName = "Alice Analyst", Username = "alice", PasswordHash = "x" });

        var item = new Item { Name = "Talc Powder", Code = "TP-1", Category = SampleCategory.FinishedProduct, SopNumber = "SOP-1" };
        db.Items.Add(item);
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        db.Specifications.Add(new Specification { ItemId = item.Id, TestCode = "TAMC", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, CauseOfTestingId = cause.Id, ControlNumber = "CTRL-1", Status = SampleStatus.Approved };
        var tamcOrder = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Waiting };
        var ecoliOrder = new TestOrder { TestCode = "ECOLI", Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(tamcOrder);
        sample.TestOrders.Add(ecoliOrder);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        db.CountTestReadings.Add(new CountTestReading
        {
            TestOrderId = tamcOrder.Id, PlateReadings = "10,12", DilutionFactor = 1, Average = 11, CalculatedResult = 11,
            ReportedResult = "11", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100", Status = "OutOfSpecification", EnteredByUserId = 1
        });
        db.Results.Add(new Result { TestOrderId = ecoliOrder.Id, RawValue = "Absent", InterpretedValue = "Absent", Type = ResultType.Interpretive, EnteredByUserId = 1 });
        await db.SaveChangesAsync();

        return sample.Id;
    }

    [Fact]
    public async Task GetSummaryAsync_TestCodeHasSpecification_PopulatesSpecificationText()
    {
        await using var db = NewDb();
        var sampleId = await SeedProductSampleAsync(db);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        Assert.NotNull(summary);
        var tamc = summary!.TestOrders.Single(t => t.TestCode == "TAMC");
        Assert.Equal("100", tamc.SpecificationText);
    }

    [Fact]
    public async Task GetSummaryAsync_TestCodeHasNoSpecification_FallsBackToNull()
    {
        await using var db = NewDb();
        var sampleId = await SeedProductSampleAsync(db);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        Assert.NotNull(summary);
        var ecoli = summary!.TestOrders.Single(t => t.TestCode == "ECOLI");
        Assert.Null(ecoli.SpecificationText);
    }

    [Fact]
    public async Task GetSummaryAsync_ProductSample_TestOrdersHaveNoLocations()
    {
        // The exact discriminator the frontend's buildCoaMatrix/
        // buildCoaSimpleRows split on: a Product/RM/PM sample's TestOrders
        // never carry a locations[] entry, which is what routes it to the
        // new non-located COA branch instead of the location matrix.
        await using var db = NewDb();
        var sampleId = await SeedProductSampleAsync(db);

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        Assert.NotNull(summary);
        Assert.All(summary!.TestOrders, t => Assert.Empty(t.Locations));
    }
}
