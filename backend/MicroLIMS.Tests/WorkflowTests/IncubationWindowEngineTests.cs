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

public class IncubationWindowEngineTests
{
    [Fact]
    public async Task SelectMediaAsync_StepMediumWindowNotConfigured_Blocks()
    {
        var db = PathogenTestData.NewDb();
        await using var _ = db;
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var brothStep = await db.TestWorkflowSteps.FirstAsync(s => s.StepName == "Broth Enrichment");
        var stepMedium = await db.TestWorkflowStepMedias.FirstAsync(m => m.TestWorkflowStepId == brothStep.Id);
        stepMedium.IncubationMinHours = 0;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(
            () => engine.SelectMediaAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, userId: 4));
        Assert.Equal(WorkflowErrorCodes.IncubationWindowNotConfigured, ex.ErrorCode);

        var incubationExists = await db.Incubations.AnyAsync(i => i.TestOrderId == order.Id && i.StepName == "Broth Enrichment");
        Assert.False(incubationExists);
    }

    [Fact]
    public async Task SelectMediaAsync_IgnoresStepLevelHoursAndTemperature()
    {
        var db = PathogenTestData.NewDb();
        await using var _ = db;
        var (order, media, incubator) = await PathogenTestData.SeedFiveStageOrderAsync(db);
        var engine = TestServiceFactory.TestWorkflow(db);

        var brothStep = await db.TestWorkflowSteps.FirstAsync(s => s.StepName == "Broth Enrichment");
        brothStep.IncubationMinHours = 99;
        brothStep.IncubationMaxHours = 120;
        brothStep.TemperatureMin = 1;
        brothStep.TemperatureMax = 2;
        await db.SaveChangesAsync();

        var incubation = await engine.SelectMediaAsync(order.Id, "Broth Enrichment", media.BrothLotId, incubator.Id, userId: 4);

        Assert.NotNull(incubation);
        Assert.Equal("18-24 hours", incubation.Duration);
        Assert.Equal("35-37 °C", incubation.Temperature);
        Assert.Equal(TimeSpan.FromHours(24), incubation.IncubationEndUtc - incubation.IncubationStartUtc);
    }
}
