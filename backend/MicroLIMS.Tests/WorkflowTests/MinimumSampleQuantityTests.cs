using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class MinimumSampleQuantityTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Item Item, CauseOfTesting RoutineCause, CauseOfTesting RetestCause)> SeedDataAsync(
        MicroLimsDbContext db,
        decimal? minQty = 100m,
        decimal? minRetestQty = 50m,
        string? unit = "g")
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var item = new Item
        {
            Name = "Raw Material A",
            Code = "RM-A",
            Category = SampleCategory.RawMaterial,
            IsActive = true,
            MinSampleQuantity = minQty,
            MinRetestSampleQuantity = minRetestQty,
            MinSampleQuantityUnit = unit
        };
        item.AssignedTests.Add(new SampleTest { TestCode = "TAMC", DisplayName = "TAMC" });
        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "TAMC", SectionId = micro.Id });
        db.Items.Add(item);

        var routineCause = new CauseOfTesting { Id = 1, Name = "Routine", RequiresTestSelection = false };
        var retestCause = new CauseOfTesting { Id = 2, Name = "Periodic retest", RequiresTestSelection = true };
        db.CausesOfTesting.AddRange(routineCause, retestCause);

        await db.SaveChangesAsync();
        return (item, routineCause, retestCause);
    }

    private static ItemBasedReceiveRequest Request(int itemId, int causeId, string sampleQuantity) =>
        new(itemId, causeId, sampleQuantity, "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, 1);

    private static ProductWorkflowEngine Engine(MicroLimsDbContext db) =>
        new(db, new ReferenceNumberGenerator(db));

    [Theory]
    [InlineData("1,5 kg", 1.5)]
    [InlineData("80 g", 80)]
    [InlineData("120 g", 120)]
    [InlineData("2 bottles", 2)]
    [InlineData("0.5 mL", 0.5)]
    public void LeadingQuantity_ParsesValidQuantities(string input, decimal expected)
    {
        var actual = ProductWorkflowEngine.LeadingQuantity(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no number")]
    public void LeadingQuantity_ReturnsNullWhenNoLeadingNumber(string? input)
    {
        var actual = ProductWorkflowEngine.LeadingQuantity(input);
        Assert.Null(actual);
    }

    [Fact]
    public async Task Receive_QuantityBelowMinimum_WarnsAndSucceeds()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "80 g"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Contains("Sample quantity 80 g is below the STM minimum of 100 g.", sample.Warnings);
    }

    [Fact]
    public async Task Receive_QuantityAtOrAboveMinimum_ProducesNoWarning()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "120 g"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Empty(sample.Warnings);
    }

    [Fact]
    public async Task Receive_DifferentUnit_ProducesNoWarningAndSucceeds()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "2 bottles"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Empty(sample.Warnings);
    }

    [Fact]
    public async Task Receive_PeriodicRetest_UsesMinRetestSampleQuantity()
    {
        await using var db = NewDb();
        var (item, _, retest) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        // 60 g is below routine min (100 g) but above retest min (50 g) -> no warning
        var sample = await Engine(db).ReceiveAsync(Request(item.Id, retest.Id, "60 g"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Empty(sample.Warnings);
    }

    [Fact]
    public async Task Receive_PeriodicRetest_BelowRetestMinimum_Warns()
    {
        await using var db = NewDb();
        var (item, _, retest) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        // 40 g is below retest min (50 g) -> warning
        var sample = await Engine(db).ReceiveAsync(Request(item.Id, retest.Id, "40 g"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Contains("Sample quantity 40 g is below the STM minimum of 50 g.", sample.Warnings);
    }

    [Fact]
    public async Task Receive_NoMinimumConfigured_ProducesNoWarning()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: null, minRetestQty: null, unit: null);

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "10 g"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Empty(sample.Warnings);
    }

    [Fact]
    public async Task Receive_ItemUnitNull_QuantityWithUnitText_ProducesNoWarning()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: null);

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "2 bottles"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Empty(sample.Warnings);
    }

    [Fact]
    public async Task Receive_ItemUnitNull_BareNumberBelowMinimum_Warns()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: null);

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "80"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Contains("Sample quantity 80 is below the STM minimum of 100.", sample.Warnings);
    }

    [Fact]
    public async Task Receive_MassUnitConversion_0_08_kg_vs_100_g_Warns()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "0.08 kg"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Contains("Sample quantity 0.08 kg is below the STM minimum of 100 g.", sample.Warnings);
    }

    [Fact]
    public async Task Receive_MassUnitConversion_0_2_kg_vs_100_g_ProducesNoWarning()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "0.2 kg"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Empty(sample.Warnings);
    }

    [Fact]
    public async Task Receive_BareNumberWithUnitConfigured_80_vs_100_g_Warns()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "80"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Contains("Sample quantity 80 is below the STM minimum of 100 g.", sample.Warnings);
    }

    [Fact]
    public async Task Receive_IncommensurateUnits_80_mL_vs_100_g_ProducesNoWarning()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 100m, minRetestQty: 50m, unit: "g");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "80 mL"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Empty(sample.Warnings);
    }

    [Fact]
    public async Task Receive_VolumeUnitConversion_0_5_L_vs_300_mL_ProducesNoWarning()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 300m, minRetestQty: 150m, unit: "mL");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "0.5 L"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Empty(sample.Warnings);
    }

    [Fact]
    public async Task Receive_VolumeUnitConversion_250_mL_vs_300_mL_Warns()
    {
        await using var db = NewDb();
        var (item, routine, _) = await SeedDataAsync(db, minQty: 300m, minRetestQty: 150m, unit: "mL");

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, routine.Id, "250 mL"));

        Assert.NotNull(sample);
        Assert.Equal(SampleStatus.Received, sample.Status);
        Assert.Contains("Sample quantity 250 mL is below the STM minimum of 300 mL.", sample.Warnings);
    }
}
