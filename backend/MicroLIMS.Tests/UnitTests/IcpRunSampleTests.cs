using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;
using static MicroLIMS.Tests.UnitTests.IcpRunServiceTests;

namespace MicroLIMS.Tests.UnitTests;

// Sample assignment and result entry. The test defines the elements; the item's specifications judge the result.
public class IcpRunSampleTests
{
    internal const string Password = "ValidPassword123!";

    internal sealed class Fixture
    {
        public required Scenario S;
        public required TestDefinition Definition;
        public required Item Item;
        public required int ZnId;
        public required int CaId;
    }

    // Assay solid: Th.Wt 0.5 g, Zn target 10 ppm, spec 90-110 %. Result 10 ppm at Act.Wt 0.5 g = 100 %.
    // Assay liquid: Th.Vol 10 mL, Sp.gr 1.2. Impurity: Zn not more than 5 ppm.
    internal static async Task<Fixture> SeedFixtureAsync(Scenario s, IcpTestMode mode = IcpTestMode.Assay,
        IcpSampleForm form = IcpSampleForm.Solid, bool withCalcium = false)
    {
        var db = s.Db;
        var assay = mode == IcpTestMode.Assay;
        var def = new TestDefinition
        {
            Code = "ZN_ICP", DisplayName = "Zinc by ICP", SectionId = s.Section.Id,
            WorkflowType = WorkflowType.IcpMethodAssay, EquationType = EquationType.IcpMethodAssay, IsActive = true,
            IcpMode = mode,
            IcpSampleForm = assay ? form : null,
            IcpTheoreticalAmount = assay ? (form == IcpSampleForm.Solid ? 0.5m : 10m) : null,
            IcpSpecificGravity = assay && form == IcpSampleForm.Liquid ? 1.2m : null,
            IcpElements =
            {
                new TestDefinitionIcpElement { Symbol = "Zn", DisplayOrder = 0, TargetPpm = assay ? 10m : null },
                new TestDefinitionIcpElement { Symbol = "Ca", DisplayOrder = 1, TargetPpm = assay ? 20m : null },
            },
        };
        var item = new Item { Code = "ITM-Z", Name = "Zinc Tablets", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.TestDefinitions.Add(def);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        var zn = def.IcpElements.Single(e => e.Symbol == "Zn");
        var ca = def.IcpElements.Single(e => e.Symbol == "Ca");

        Specification Spec(int elementId, string name, int order) => assay
            ? new() { ItemId = item.Id, TestCode = def.Code, ParameterName = name, IcpTestElementId = elementId, LimitType = LimitType.Range, LowerLimit = 90m, UpperLimit = 110m, Unit = "%", DisplayOrder = order }
            : new() { ItemId = item.Id, TestCode = def.Code, ParameterName = name, IcpTestElementId = elementId, LimitType = LimitType.NotMoreThan, UpperLimit = 5m, Unit = "ppm", DisplayOrder = order };
        db.Specifications.Add(Spec(zn.Id, "Zn", 1));
        if (withCalcium) db.Specifications.Add(Spec(ca.Id, "Ca", 2));
        await db.SaveChangesAsync();
        return new Fixture { S = s, Definition = def, Item = item, ZnId = zn.Id, CaId = ca.Id };
    }

    internal static async Task<TestOrder> AddOrderAsync(Fixture f, string batch = "B-001")
    {
        var db = f.S.Db;
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync() ?? new CauseOfTesting { Name = "Release", IsActive = true };
        if (cause.Id == 0) db.CausesOfTesting.Add(cause);
        var sample = new Sample
        {
            ReferenceNumber = "FP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            ItemId = f.Item.Id, Category = SampleCategory.FinishedProduct, BatchNumber = batch,
            ReceivedAt = SepFirst.UtcDateTime, CauseOfTesting = cause, ReceivedByUserId = f.S.UserId, Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = new TestOrder
        {
            SampleId = sample.Id, TestCode = f.Definition.Code, SectionId = f.S.Section.Id,
            CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress,
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    // Zn 0.9995 and Ca 0.9995 pass unless overridden.
    internal static async Task<IcpRunDto> ConfirmedRunAsync(Scenario s, params (string Symbol, decimal? R)[] elements)
    {
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id,
            Save(s.CalLot.Id, elements.Length > 0 ? elements : new (string, decimal?)[] { ("Zn", 0.9995m), ("Ca", 0.9995m) }), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        return await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null);
    }

    internal static async Task<(Fixture F, IcpRunDto Run, int RunSampleId)> AssignedAsync(
        IcpTestMode mode = IcpTestMode.Assay, IcpSampleForm form = IcpSampleForm.Solid, bool withCalcium = false)
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s, mode, form, withCalcium);
        var order = await AddOrderAsync(f);
        var run = await ConfirmedRunAsync(s);
        run = await s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        return (f, run, run.Samples.Single().Id);
    }

    internal static Task<IcpSampleEntryDto> SaveAsync(Fixture f, int runSampleId, decimal? weight, params (int ElementId, decimal Ppm)[] results) =>
        f.S.Service.SaveEntryAsync(runSampleId, new SaveIcpSampleEntryRequest(weight,
            results.Select(r => new IcpConcentrationInput(r.ElementId, r.Ppm)).ToList()), f.S.UserId);

    [Fact]
    public async Task Assign_BeforeCalibration_Throws()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var order = await AddOrderAsync(f);
        var run = await StartAsync(s);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId));
        Assert.Equal("Confirm the calibration before assigning samples.", ex.Message);
    }

    [Fact]
    public async Task Assign_IneligibleOrder_Throws()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var order = await AddOrderAsync(f);
        order.CurrentStep = WorkflowStep.Ready;
        await s.Db.SaveChangesAsync();
        var run = await ConfirmedRunAsync(s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId));
        Assert.Equal($"Test order {order.Id} is not eligible for this run.", ex.Message);
    }

    [Fact]
    public async Task Eligible_ListsAnyIcpTest_AndHidesAssigned()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var a = await AddOrderAsync(f, "B-A");
        var b = await AddOrderAsync(f, "B-B");
        var run = await ConfirmedRunAsync(s);

        Assert.Equal(2, (await s.Service.GetEligibleTestsAsync(run.Id, null, s.UserId)).Count);
        await s.Service.AssignSamplesAsync(run.Id, new List<int> { a.Id }, s.UserId);

        var left = await s.Service.GetEligibleTestsAsync(run.Id, null, s.UserId);
        Assert.Equal(b.Id, left.Single().TestOrderId);
    }

    [Fact]
    public async Task Remove_RequiresReason_AndAllowsReassigning()
    {
        var (f, run, rsId) = await AssignedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.RemoveSampleAsync(run.Id, rsId, " ", f.S.UserId));

        var after = await f.S.Service.RemoveSampleAsync(run.Id, rsId, "Wrong sample", f.S.UserId);

        Assert.Equal(IcpRunSampleStatus.Removed, after.Samples.Single().Status);
        var again = await f.S.Service.AssignSamplesAsync(run.Id, new List<int> { after.Samples.Single().TestOrderId }, f.S.UserId);
        Assert.Equal(2, again.Samples.Count);
    }

    [Fact]
    public async Task CompleteRun_WithUnsubmittedSample_Throws_ThenRemovedCompletes()
    {
        var (f, run, rsId) = await AssignedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.CompleteRunAsync(run.Id, f.S.UserId));
        Assert.Equal("1 assigned sample(s) have not been sent for review. Submit or remove them first.", ex.Message);

        await f.S.Service.RemoveSampleAsync(run.Id, rsId, "Not tested", f.S.UserId);
        var done = await f.S.Service.CompleteRunAsync(run.Id, f.S.UserId);
        Assert.Equal(IcpRunStatus.Completed, done.Status);
    }

    [Fact]
    public async Task SaveEntry_AssayWithoutWeight_Throws()
    {
        var (f, _, rsId) = await AssignedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(f, rsId, null, (f.ZnId, 10m)));
        Assert.Equal("Enter the actual weight of the sample (g).", ex.Message);
    }

    [Fact]
    public async Task SaveEntry_MissingElementResult_Throws()
    {
        var (f, _, rsId) = await AssignedAsync(withCalcium: true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(f, rsId, 0.5m, (f.ZnId, 10m)));
        Assert.Equal("Exactly one result for Ca is required.", ex.Message);
    }

    [Fact]
    public async Task SaveEntry_ElementNotNeededByTheTest_Throws()
    {
        var (f, _, rsId) = await AssignedAsync(); // only Zn has a specification
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(f, rsId, 0.5m, (f.ZnId, 10m), (f.CaId, 10m)));
        Assert.Equal("A result belongs to an element that is not needed by this test.", ex.Message);
    }

    [Fact]
    public async Task SaveEntry_NegativeResult_Throws()
    {
        var (f, _, rsId) = await AssignedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(f, rsId, 0.5m, (f.ZnId, -1m)));
        Assert.Equal("A result cannot be negative.", ex.Message);
    }

    [Fact]
    public async Task SaveEntry_ImpurityWithWeight_Throws()
    {
        var (f, _, rsId) = await AssignedAsync(IcpTestMode.Impurity);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(f, rsId, 0.5m, (f.ZnId, 1m)));
        Assert.Equal("The actual weight is only used for assay tests.", ex.Message);
    }

    [Fact]
    public async Task SaveEntry_CalibrationExpired_Throws()
    {
        var (f, _, rsId) = await AssignedAsync();
        f.S.Db.IcpCalibrations.Single().ConfirmedAt = SepFirst.UtcDateTime.AddDays(-30);
        await f.S.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(f, rsId, 0.5m, (f.ZnId, 10m)));
        Assert.StartsWith("Calibration expired at", ex.Message);
    }

    [Fact]
    public async Task Preview_AssaySolid_HandChecked()
    {
        var (f, _, rsId) = await AssignedAsync();

        // 9.5 x 0.5 x 100 / (10 x 0.5) = 95 %
        var entry = await SaveAsync(f, rsId, 0.5m, (f.ZnId, 9.5m));

        var row = Assert.Single(entry.Preview);
        Assert.Equal("IcpAssayPercent", row.Quantity);
        Assert.Equal(95m, row.Value);
        Assert.Equal("95.0 %", row.Display);
        Assert.Equal(ResultStatus.WithinLimits, row.Status);
        Assert.Equal("90-110", row.SpecLimit);
        Assert.Equal(IcpTestMode.Assay, entry.Mode);
        Assert.Equal(0.5m, entry.TheoreticalAmount);
        Assert.Equal(10m, entry.Elements.Single().TargetPpm);
        Assert.True(entry.CanSubmit == false && entry.CanSubmitReason == "Upload the sample result report before sending for review.");
    }

    [Fact]
    public async Task Preview_AssayWeightScalesTheResult()
    {
        var (f, _, rsId) = await AssignedAsync();

        // Taking 0.55 g instead of 0.5 g: 10 x 0.5 x 100 / (10 x 0.55) = 90.909 %
        var entry = await SaveAsync(f, rsId, 0.55m, (f.ZnId, 10m));

        Assert.Equal("90.9 %", entry.Preview.Single().Display);
        Assert.Equal(ResultStatus.WithinLimits, entry.Preview.Single().Status);
    }

    [Fact]
    public async Task Preview_AssayLiquid_UsesVolumeAndSpecificGravity()
    {
        var (f, _, rsId) = await AssignedAsync(form: IcpSampleForm.Liquid);

        // 10 x 10 mL x 1.2 x 100 / (10 x 12 g) = 100 %
        var entry = await SaveAsync(f, rsId, 12m, (f.ZnId, 10m));

        Assert.Equal(IcpSampleForm.Liquid, entry.SampleForm);
        Assert.Equal(1.2m, entry.SpecificGravity);
        Assert.Equal("100.0 %", entry.Preview.Single().Display);
    }

    [Fact]
    public async Task Preview_AssayOutOfRange_IsOutOfSpecification()
    {
        var (f, _, rsId) = await AssignedAsync();
        var entry = await SaveAsync(f, rsId, 0.5m, (f.ZnId, 8m));
        Assert.Equal("80.0 %", entry.Preview.Single().Display);
        Assert.Equal(ResultStatus.OutOfSpecification, entry.Preview.Single().Status);
    }

    [Theory]
    [InlineData(0, ResultStatus.WithinLimits, "0 ppm")]
    [InlineData(3.5, ResultStatus.WithinLimits, "3.5 ppm")]
    [InlineData(5, ResultStatus.WithinLimits, "5 ppm")]
    [InlineData(7.25, ResultStatus.OutOfSpecification, "7.25 ppm")]
    public async Task Preview_Impurity_JudgedByNotMoreThan(double ppm, ResultStatus status, string display)
    {
        var (f, _, rsId) = await AssignedAsync(IcpTestMode.Impurity);

        var entry = await SaveAsync(f, rsId, null, (f.ZnId, (decimal)ppm));

        var row = Assert.Single(entry.Preview);
        Assert.Equal("IcpImpurityPpm", row.Quantity);
        Assert.Equal(display, row.Display);
        Assert.Equal(status, row.Status);
        Assert.Equal("NMT 5", row.SpecLimit);
        Assert.Null(entry.ActualWeightG);
    }

    [Fact]
    public async Task Preview_ElementNotCalibratedOnTheRun_IsBlocked()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var order = await AddOrderAsync(f);
        var run = await ConfirmedRunAsync(s, ("Ca", 0.9995m)); // the test needs Zn
        run = await s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        var rsId = run.Samples.Single().Id;

        var entry = await SaveAsync(f, rsId, 0.5m, (f.ZnId, 10m));

        var el = Assert.Single(entry.Elements);
        Assert.False(el.Valid);
        Assert.Equal("Zn was not calibrated on this run.", el.Reason);
        Assert.Equal("Zn: Zn was not calibrated on this run.", entry.Preview.Single().Problem);
        Assert.False(entry.CanSubmit);
    }

    [Fact]
    public async Task Preview_ElementFailedCalibration_IsBlocked()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var order = await AddOrderAsync(f);
        var run = await ConfirmedRunAsync(s, ("Zn", 0.9980m), ("Ca", 0.9995m));
        run = await s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        var entry = await SaveAsync(f, run.Samples.Single().Id, 0.5m, (f.ZnId, 10m));

        Assert.StartsWith("Failed calibration: r 0.998 is below the minimum 0.999", entry.Elements.Single().Reason);
        Assert.False(entry.CanSubmit);
    }

    [Fact]
    public async Task Entry_SpecWithoutElement_BlocksSubmit()
    {
        var (f, _, rsId) = await AssignedAsync();
        f.S.Db.Specifications.Add(new Specification
        {
            ItemId = f.Item.Id, TestCode = f.Definition.Code, ParameterName = "Loose", LimitType = LimitType.Range,
            LowerLimit = 0m, UpperLimit = 1m, DisplayOrder = 9,
        });
        await f.S.Db.SaveChangesAsync();

        var entry = await f.S.Service.GetSampleEntryAsync(rsId, f.S.UserId);
        Assert.Equal("Specification \"Loose\" is not linked to an ICP element.", entry.CanSubmitReason);
    }

    [Fact]
    public async Task Remove_AfterSubmit_Throws()
    {
        var (f, run, rsId) = await AssignedAsync();
        await SaveAsync(f, rsId, 0.5m, (f.ZnId, 10m));
        await IcpMethodAssayRecorderTests.UploadReportAsync(f, run.Id, rsId);
        await IcpMethodAssayRecorderTests.SubmitAsync(f, rsId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.RemoveSampleAsync(run.Id, rsId, "oops", f.S.UserId));
        Assert.Equal("A result has already been sent for review for this sample.", ex.Message);
    }

    [Fact]
    public async Task RunDto_CanAssignSamples_FollowsCalibration()
    {
        var s = await SeedAsync();
        var run = await StartAsync(s);
        Assert.False(run.CanAssignSamples);
        Assert.Equal("Confirm the calibration before assigning samples.", run.CanAssignSamplesReason);

        await s.Service.SaveCalibrationAsync(run.Id, Save(s.CalLot.Id, ("Zn", 0.9995m)), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        run = await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null);
        Assert.True(run.CanAssignSamples);
    }
}
