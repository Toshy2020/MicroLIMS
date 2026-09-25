using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Finished Product (FP section) tests have no Test Preparation stage.
public class FpPreparationRuleTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Item> SeedItemAsync(MicroLimsDbContext db, params (string Code, bool Fp)[] tests)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { DepartmentId = micro.DepartmentId, Name = "Finished Product", Code = "FP", IsActive = true };
        db.DocumentSections.Add(fp);
        await db.SaveChangesAsync();

        var item = new Item { Name = "Vitamin C Tablet", Code = "VC-01", Category = SampleCategory.FinishedProduct, IsActive = true };
        foreach (var (code, isFp) in tests)
        {
            db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, SectionId = isFp ? fp.Id : micro.Id });
            item.AssignedTests.Add(new SampleTest { TestCode = code });
        }
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static Task<Sample> ReceiveAsync(MicroLimsDbContext db, Item item)
    {
        TestServiceFactory.EnsureProductionStage(db, "Bulk");
        return new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db)).ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, 1, "100g", "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(2), "Bulk", 1));
    }

    [Fact]
    public async Task Receive_OnlyFpTests_SampleIsReadyWithoutPreparation()
    {
        await using var db = NewDb();
        var item = await SeedItemAsync(db, ("VITC-ASSAY", true));

        var sample = await ReceiveAsync(db, item);

        Assert.Equal(SamplePreparationStatus.Ready, sample.PreparationStatus);
    }

    [Fact]
    public async Task Receive_MixedFpAndMicroTests_SampleStillNeedsPreparation()
    {
        await using var db = NewDb();
        var item = await SeedItemAsync(db, ("VITC-ASSAY", true), ("TAMC", false));

        var sample = await ReceiveAsync(db, item);

        Assert.Equal(SamplePreparationStatus.NeedsPreparation, sample.PreparationStatus);
    }

    [Fact]
    public async Task Advance_FpTestOnUnpreparedMixedSample_IsNotBlockedByPreparation()
    {
        await using var db = NewDb();
        var item = await SeedItemAsync(db, ("VITC-ASSAY", true), ("TAMC", false));
        var sample = await ReceiveAsync(db, item);
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        var fpOrder = sample.TestOrders.Single(o => o.TestCode == "VITC-ASSAY");
        var microOrder = sample.TestOrders.Single(o => o.TestCode == "TAMC");

        Assert.Equal(WorkflowStep.Running, await engine.AdvanceAsync(fpOrder.Id, performedByUserId: 1));
        await Assert.ThrowsAsync<WorkflowStepException>(() => engine.AdvanceAsync(microOrder.Id, performedByUserId: 1));
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "TestStartRefused" && a.TestOrderId == fpOrder.Id));
    }
}
