using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class StepMediumProductMatchingTests
{
    [Fact]
    public void StepMediumMatcher_ExactBatch_Matches()
    {
        var stepMedium = new TestWorkflowStepMedia { MaterialId = 10 };

        Assert.True(StepMediumMatcher.Matches(stepMedium, 10, null));
        Assert.True(StepMediumMatcher.Matches(stepMedium, 10, 99));
    }

    [Fact]
    public void StepMediumMatcher_SameProductViaMediaConfiguration_Matches()
    {
        var stepMedium = new TestWorkflowStepMedia
        {
            MaterialId = 10,
            MediaConfiguration = new MediaConfiguration { MediaProductId = 5 }
        };

        Assert.True(StepMediumMatcher.Matches(stepMedium, 20, 5));
    }

    [Fact]
    public void StepMediumMatcher_SameProductViaMaterial_Matches()
    {
        var stepMedium = new TestWorkflowStepMedia
        {
            MaterialId = 10,
            Material = new Material { MediaProductId = 5 }
        };

        Assert.True(StepMediumMatcher.Matches(stepMedium, 20, 5));
    }

    [Fact]
    public void StepMediumMatcher_DifferentProduct_Rejected()
    {
        var stepMedium = new TestWorkflowStepMedia
        {
            MaterialId = 10,
            MediaConfiguration = new MediaConfiguration { MediaProductId = 5 }
        };

        Assert.False(StepMediumMatcher.Matches(stepMedium, 20, 6));
    }

    [Fact]
    public void StepMediumMatcher_BothProductsNull_ExactBatchOnly()
    {
        var stepMedium = new TestWorkflowStepMedia { MaterialId = 10 };

        Assert.False(StepMediumMatcher.Matches(stepMedium, 20, null));
        Assert.True(StepMediumMatcher.Matches(stepMedium, 10, null));
    }

    private static async Task<(int orderId, SeededMedia media, int incubatorId, ITestWorkflowEngine engine, MicroLimsDbContext db)> SetupReadyForPlatingAsync()
    {
        var db = PathogenTestData.NewDb();
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var start = DateTime.UtcNow.AddHours(-30);
        var end = DateTime.UtcNow.AddHours(-6);
        await engine.SubmitBrothAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, start, end, null, userId: 4);
        await engine.SubmitBrothAsync(order.Id, "Selective Broth", media.SelectiveBrothLotId, media.SelectiveBrothIncubatorId, start, end, null, userId: 4);
        return (order.Id, media, incubator.Id, engine, db);
    }

    [Fact]
    public async Task SelectMediaAsync_ConfiguredWithBatchA_AcceptsLotFromBatchBOfSameProduct()
    {
        var db = PathogenTestData.NewDb();
        await using var _ = db;
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Batch A was brothMaterialId. Now create batch B under the same TSB product.
        var tsbProduct = await MediaProductTestData.CreateOrGetAsync(db, "Tryptone Soya Broth", "TSB");
        var batchB = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = tsbProduct.Name,
            ManufacturerName = "Himedia",
            BatchNumber = "LOT-TSB-BATCH-B",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            Location = "Micro Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            Code = tsbProduct.Code,
            MediaProductId = tsbProduct.Id
        };
        db.Materials.Add(batchB);
        await db.SaveChangesAsync();

        var lotB = new Media
        {
            MaterialId = batchB.Id,
            LotNumber = "TSB/02/26",
            IsReleasedForUse = true,
            Status = MediaStatus.Active,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        db.Media.Add(lotB);
        await db.SaveChangesAsync();

        var incubation = await engine.SelectMediaAsync(order.Id, "Broth Enrichment", lotB.Id, incubator.Id, userId: 4);

        Assert.NotNull(incubation);
        Assert.Equal(lotB.Id, incubation.MediaId);
    }

    [Fact]
    public async Task SelectMediaAsync_LotOfDifferentProduct_RefusedWithExistingError()
    {
        var db = PathogenTestData.NewDb();
        await using var _ = db;
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        // Media lot XldLotId is XLD Agar, but step is "Broth Enrichment" which requires TSB
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.SelectMediaAsync(order.Id, "Broth Enrichment", media.XldLotId, incubator.Id, userId: 4));
        Assert.Contains("This step requires a different medium", ex.Message);
    }

    [Fact]
    public async Task StartSelectivePlatingIncubationAsync_ConfiguredWithBatchA_AcceptsLotFromBatchBOfSameProduct()
    {
        var (orderId, media, incubatorId, engine, db) = await SetupReadyForPlatingAsync();
        await using var _ = db;

        // Step medium was configured with batch A of XLD Agar.
        // Seed batch B under the same XLD Agar product.
        var xldProduct = await MediaProductTestData.CreateOrGetAsync(db, "XLD Agar", "XLD");
        var batchB = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = xldProduct.Name,
            ManufacturerName = "Himedia",
            BatchNumber = "LOT-XLD-BATCH-B",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            Location = "Micro Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            Code = xldProduct.Code,
            MediaProductId = xldProduct.Id
        };
        db.Materials.Add(batchB);
        await db.SaveChangesAsync();

        var lotB = new Media
        {
            MaterialId = batchB.Id,
            LotNumber = "XLD/02/26",
            IsReleasedForUse = true,
            Status = MediaStatus.Active,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        db.Media.Add(lotB);
        await db.SaveChangesAsync();

        // Forget everything seeded above, so the engine loads the step medium
        // from the store the way a real request does - without this, change
        // tracking fills in its Material/MediaConfiguration and the test
        // would pass even if RequireSingleStepMediumAsync never loaded them.
        db.ChangeTracker.Clear();

        var start = DateTime.UtcNow;
        var incubation = await engine.StartSelectivePlatingIncubationAsync(
            orderId, "Selective Plating", lotB.Id, incubatorId, start, userId: 4);

        Assert.NotNull(incubation);
        Assert.Equal(lotB.Id, incubation.MediaId);
    }

    [Fact]
    public async Task StartSelectivePlatingIncubationAsync_LotOfDifferentProduct_RefusedWithExistingError()
    {
        var (orderId, media, incubatorId, engine, db) = await SetupReadyForPlatingAsync();
        await using var _ = db;

        // Pass TSI lot for "Selective Plating" step which requires XLD
        var ex = await Assert.ThrowsAsync<WorkflowStepException>(
            () => engine.StartSelectivePlatingIncubationAsync(
                orderId, "Selective Plating", media.TsiLotId, incubatorId, DateTime.UtcNow, userId: 4));

        Assert.Equal(WorkflowErrorCodes.MediaNotInPermittedList, ex.ErrorCode);
        Assert.Contains("not a lot of the permitted medium for this step", ex.Message);
    }
}
