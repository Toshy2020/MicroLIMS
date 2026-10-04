using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;
using static MicroLIMS.Tests.UnitTests.IcpRunServiceTests;

namespace MicroLIMS.Tests.UnitTests;

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

    // Method elements come from the scenario; the test, item and its specs are added here.
    internal static async Task<Fixture> SeedFixtureAsync(
        Scenario s, bool znBases = true, LimitType znLimit = LimitType.Range, IcpMethodMode? mode = null)
    {
        var db = s.Db;
        var elements = await db.IcpMethodElements.Where(e => e.IcpMethodId == s.MethodId).ToListAsync();
        var zn = elements.Single(e => e.Symbol == "Zn");
        var ca = elements.Single(e => e.Symbol == "Ca");
        if (mode != null)
            (await db.IcpMethods.FirstAsync(m => m.Id == s.MethodId)).Mode = mode.Value;

        var def = new TestDefinition
        {
            Code = "ZN_ICP", DisplayName = "Zinc by ICP", SectionId = s.Section.Id,
            WorkflowType = WorkflowType.IcpMethodAssay, EquationType = EquationType.IcpMethodAssay,
            IcpMethodId = s.MethodId, IsActive = true,
        };
        var item = new Item { Code = "ITM-Z", Name = "Zinc Tablets", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.TestDefinitions.Add(def);
        db.Items.Add(item);
        await db.SaveChangesAsync();

        Specification Spec(int elementId, string name, ResultBasis basis, LimitType limit, decimal? lo, decimal? hi, decimal? claim, string? claimUnit, int order) => new()
        {
            ItemId = item.Id, TestCode = def.Code, ParameterName = name, IcpMethodElementId = elementId, ResultBasis = basis,
            LimitType = limit, LowerLimit = lo, UpperLimit = hi, LabelClaim = claim, LabelClaimUnit = claimUnit, Unit = claimUnit ?? "", DisplayOrder = order,
        };
        if (znBases)
        {
            db.Specifications.Add(Spec(zn.Id, "Zn (amount)", ResultBasis.MgPerUnit, LimitType.Range, 0.9m, 1.1m, 1m, "mg", 1));
            db.Specifications.Add(Spec(zn.Id, "Zn (%LC)", ResultBasis.PercentLabelClaim, LimitType.Range, 90m, 110m, 1m, "mg", 2));
        }
        else
            db.Specifications.Add(Spec(zn.Id, "Zn (impurity)", ResultBasis.MgPerKg, znLimit, 0m, 1000m, null, null, 1));
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

    internal static async Task<IcpRunDto> ConfirmedRunAsync(Scenario s, decimal znR = 0.9995m)
    {
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, znR: znR), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        return await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null);
    }

    internal static async Task<(Fixture F, IcpRunDto Run, int RunSampleId)> AssignedAsync(
        bool znBases = true, LimitType znLimit = LimitType.Range, IcpMethodMode? mode = null)
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s, znBases, znLimit, mode);
        var order = await AddOrderAsync(f);
        var run = await ConfirmedRunAsync(s);
        run = await s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        return (f, run, run.Samples.Single().Id);
    }

    internal static IcpReplicateInputDto Rep(Fixture f, decimal w, decimal zn) =>
        new(w, 50m, 10m, new List<IcpConcentrationInput> { new(f.ZnId, zn) });

    internal static Task<IcpSampleEntryDto> SaveAsync(Fixture f, int runSampleId, params IcpReplicateInputDto[] reps) =>
        f.S.Service.SaveReplicatesAsync(runSampleId, new SaveIcpReplicatesRequest(IcpAmountUnit.Gram, 1.25m, reps.ToList()), f.S.UserId);

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
    public async Task Assign_NoValidElement_Throws()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var order = await AddOrderAsync(f);
        var run = await StartAsync(s);
        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, znR: 0.9980m, caR: 0.9970m), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId));
        Assert.Equal("No element is valid on this run.", ex.Message);
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
    public async Task Eligible_ListsAssignableOrdersAndHidesAssigned()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var a = await AddOrderAsync(f, "ALPHA");
        var b = await AddOrderAsync(f, "BETA");
        var run = await ConfirmedRunAsync(s);
        Assert.Equal(2, (await s.Service.GetEligibleTestsAsync(run.Id, null, s.UserId)).Count);
        Assert.Equal(b.Id, Assert.Single(await s.Service.GetEligibleTestsAsync(run.Id, "beta", s.UserId)).TestOrderId);
        await s.Service.AssignSamplesAsync(run.Id, new List<int> { a.Id }, s.UserId);
        Assert.Equal(b.Id, Assert.Single(await s.Service.GetEligibleTestsAsync(run.Id, null, s.UserId)).TestOrderId);
    }

    [Fact]
    public async Task Remove_RequiresReason()
    {
        var (f, run, rsId) = await AssignedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.RemoveSampleAsync(run.Id, rsId, " ", f.S.UserId));
        Assert.Equal("A reason is required.", ex.Message);

        var after = await f.S.Service.RemoveSampleAsync(run.Id, rsId, "Wrong sample", f.S.UserId);
        Assert.Equal(IcpRunSampleStatus.Removed, after.Samples.Single().Status);
    }

    [Fact]
    public async Task Assign_AfterRemove_SameRun_CreatesNewAssignedRow()
    {
        var (f, run, oldId) = await AssignedAsync();
        var orderId = run.Samples.Single().TestOrderId;
        await f.S.Service.RemoveSampleAsync(run.Id, oldId, "Wrong prep", f.S.UserId);

        run = await f.S.Service.AssignSamplesAsync(run.Id, new List<int> { orderId }, f.S.UserId);

        Assert.Equal(2, run.Samples.Count);
        var removed = run.Samples.Single(x => x.Status == IcpRunSampleStatus.Removed);
        var active = run.Samples.Single(x => x.Status == IcpRunSampleStatus.Assigned);
        Assert.Equal(oldId, removed.Id);
        Assert.NotEqual(oldId, active.Id);
        Assert.Equal("Wrong prep", (await f.S.Db.IcpRunSamples.FirstAsync(x => x.Id == oldId)).RemovedReason);
        var instrument = (await f.S.Service.GetInstrumentsAsync(f.S.UserId)).Single(i => i.ActiveRun != null);
        Assert.Equal(1, instrument.ActiveRun!.SampleCount);

        var entry = await SaveAsync(f, active.Id, Rep(f, 0.5m, 0.8m));
        Assert.Equal(active.Id, entry.RunSampleId);
        Assert.Equal("This sample was removed from the run.", (await f.S.Service.GetSampleEntryAsync(oldId, f.S.UserId)).EditableReason);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.CompleteRunAsync(run.Id, f.S.UserId));
        Assert.Equal("1 assigned sample(s) have not been sent for review. Submit or remove them first.", ex.Message);
    }

    [Fact]
    public async Task CompleteRun_WithUnsubmittedSample_Throws_ThenRemovedCompletes()
    {
        var (f, run, rsId) = await AssignedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.CompleteRunAsync(run.Id, f.S.UserId));
        Assert.Equal("1 assigned sample(s) have not been sent for review. Submit or remove them first.", ex.Message);
        await f.S.Service.RemoveSampleAsync(run.Id, rsId, "Not needed", f.S.UserId);
        Assert.Equal(IcpRunStatus.Completed, (await f.S.Service.CompleteRunAsync(run.Id, f.S.UserId)).Status);
    }

    [Fact]
    public async Task SaveReplicates_MissingElement_Throws()
    {
        var (f, _, rsId) = await AssignedAsync();
        var bad = new IcpReplicateInputDto(0.5m, 50m, 10m, new List<IcpConcentrationInput>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(f, rsId, bad));
        Assert.Equal("Replicate 1: exactly one concentration for Zn is required.", ex.Message);
    }

    [Fact]
    public async Task SaveReplicates_CalibrationExpired_Throws()
    {
        var (f, _, rsId) = await AssignedAsync();
        var cal = await f.S.Db.IcpCalibrations.SingleAsync();
        cal.ConfirmedAt = SepFirst.UtcDateTime.AddHours(-30);
        await f.S.Db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(f, rsId, Rep(f, 0.5m, 0.8m)));
        Assert.StartsWith("Calibration expired at ", ex.Message);
    }

    [Fact]
    public async Task SaveReplicates_ImpuritiesInMl_Throws()
    {
        var (f, _, rsId) = await AssignedAsync(znBases: false, mode: IcpMethodMode.ElementalImpurities);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.S.Service.SaveReplicatesAsync(rsId, new SaveIcpReplicatesRequest(IcpAmountUnit.Milliliter, null, new() { Rep(f, 0.5m, 0.8m) }), f.S.UserId));
        Assert.Equal("Elemental impurities are reported per gram; enter the sample amount in g.", ex.Message);
    }

    [Fact]
    public async Task SaveReplicates_UnitAmountMissing_Throws()
    {
        var (f, _, rsId) = await AssignedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.S.Service.SaveReplicatesAsync(rsId, new SaveIcpReplicatesRequest(IcpAmountUnit.Gram, null, new() { Rep(f, 0.5m, 0.8m) }), f.S.UserId));
        Assert.Equal("Enter the unit amount (average unit weight or dose).", ex.Message);
    }

    [Fact]
    public async Task Preview_MineralRows_HandChecked()
    {
        var (f, _, rsId) = await AssignedAsync();
        var entry = await SaveAsync(f, rsId, Rep(f, 0.5000m, 0.80m), Rep(f, 0.5100m, 0.82m));

        Assert.Equal(new[] { f.ZnId }, entry.Elements.Select(e => e.IcpMethodElementId).ToArray()); // only Zn is needed
        Assert.Equal(2, entry.Replicates.Count);
        Assert.Equal(2, entry.Preview.Count);
        var amount = entry.Preview.Single(p => p.ParameterName == "Zn (amount)");
        Assert.Equal("IcpMgPerUnit", amount.Quantity);
        Assert.Equal("IcpPercentLabelClaim", entry.Preview.Single(p => p.ParameterName == "Zn (%LC)").Quantity);
        Assert.Equal("1.002 mg", amount.Display);
        Assert.Equal(ResultStatus.WithinLimits, amount.Status);
        Assert.Equal(1.002451m, Math.Round(amount.Value!.Value, 6));
        var pct = entry.Preview.Single(p => p.ParameterName == "Zn (%LC)");
        Assert.Equal("100.2 %", pct.Display);
        Assert.Equal(ResultStatus.WithinLimits, pct.Status);
        Assert.False(entry.CanSubmit);
        Assert.Equal("Upload the sample result report before sending for review.", entry.CanSubmitReason);
    }

    [Fact]
    public async Task Preview_OverRange_ShowsProblem()
    {
        var (f, _, rsId) = await AssignedAsync();
        var entry = await SaveAsync(f, rsId, Rep(f, 0.5m, 0.8m), Rep(f, 0.5m, 6.5m));
        Assert.All(entry.Preview, p =>
        {
            Assert.Equal("Zn: replicate 2 is above the top standard - dilute and re-measure.", p.Problem);
            Assert.Null(p.Status);
        });
        Assert.False(entry.CanSubmit);
    }

    [Fact]
    public async Task Preview_AllBelowLoq_NotMoreThanPasses()
    {
        var (f, _, rsId) = await AssignedAsync(znBases: false, znLimit: LimitType.NotMoreThan);
        var entry = await SaveAsync(f, rsId, Rep(f, 0.5m, 0.05m), Rep(f, 0.5m, 0.09m));
        var row = Assert.Single(entry.Preview);
        Assert.Equal("<LOQ", row.Display);
        Assert.Equal(ResultStatus.WithinLimits, row.Status);
        Assert.Null(row.Value);
    }

    [Fact]
    public async Task Preview_SomeBelowLoq_RequiresReviewWithSuffix()
    {
        var (f, _, rsId) = await AssignedAsync(znBases: false, znLimit: LimitType.NotMoreThan);
        var entry = await SaveAsync(f, rsId, Rep(f, 0.5m, 0.05m), Rep(f, 0.5m, 0.8m));
        var row = Assert.Single(entry.Preview);
        Assert.Equal("800.00 µg/g (some replicates <LOQ)", row.Display);
        Assert.Equal(ResultStatus.RequiresReview, row.Status);
    }

    [Fact]
    public async Task Preview_ImpurityQuantity_IsIcpMgPerKg()
    {
        var (f, _, rsId) = await AssignedAsync(znBases: false, znLimit: LimitType.NotMoreThan);
        var entry = await SaveAsync(f, rsId, Rep(f, 0.5m, 0.8m));
        Assert.Equal("IcpMgPerKg", Assert.Single(entry.Preview).Quantity);
    }

    // ---- returned / submitted samples ----

    private static async Task SubmitSampleAsync(Fixture f, int runId, int rsId)
    {
        await SaveAsync(f, rsId, Rep(f, 0.5000m, 0.80m), Rep(f, 0.5100m, 0.82m));
        await f.S.Service.UploadEvidenceAsync(runId, rsId, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport,
            "s.pdf", "application/pdf", new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }), f.S.UserId);
        await TestServiceFactory.TestWorkflow(f.S.Db, clock: NewClock()).SubmitIcpMethodAssayAsync(rsId, Password, "ok", f.S.UserId, "127.0.0.1");
    }

    private static async Task ReturnOrderAsync(Fixture f, int orderId)
    {
        var db = f.S.Db;
        var role = new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var head = new User
        {
            Username = "head_" + Guid.NewGuid().ToString("N")[..6], FullName = "Head", RoleId = role.Id, IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(head);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, head.Id);
        await TestServiceFactory.Review(db).ReturnToAnalystAsync(orderId, head.Id, "Recheck");
    }

    [Fact]
    public async Task Remove_AfterSubmit_Throws()
    {
        var (f, run, rsId) = await AssignedAsync();
        await SubmitSampleAsync(f, run.Id, rsId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.RemoveSampleAsync(run.Id, rsId, "Oops", f.S.UserId));
        Assert.Equal("A result has already been sent for review for this sample.", ex.Message);

        // Still refused once the run is completed while the result is active.
        await f.S.Service.CompleteRunAsync(run.Id, f.S.UserId);
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.RemoveSampleAsync(run.Id, rsId, "Oops", f.S.UserId));
        Assert.Equal("A result has already been sent for review for this sample.", ex.Message);
    }

    [Fact]
    public async Task Remove_AbandonedRun_Throws()
    {
        var (f, run, rsId) = await AssignedAsync();
        await f.S.Service.AbandonRunAsync(run.Id, "Torch out", f.S.UserId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.RemoveSampleAsync(run.Id, rsId, "x", f.S.UserId));
        Assert.Equal("The run is closed.", ex.Message);
    }

    [Fact]
    public async Task Remove_ReturnedSampleOnCompletedRun_FreesOrder()
    {
        var (f, run, rsId) = await AssignedAsync();
        var orderId = run.Samples.Single().TestOrderId;
        await SubmitSampleAsync(f, run.Id, rsId);
        await f.S.Service.CompleteRunAsync(run.Id, f.S.UserId);
        await ReturnOrderAsync(f, orderId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.S.Service.RemoveSampleAsync(run.Id, rsId, " ", f.S.UserId));
        Assert.Equal("A reason is required.", ex.Message);
        var after = await f.S.Service.RemoveSampleAsync(run.Id, rsId, "Calibration expired", f.S.UserId);
        Assert.Equal(IcpRunSampleStatus.Removed, after.Samples.Single().Status);
        Assert.Equal(IcpRunStatus.Completed, after.Status);

        // The order is free again: a new run can take it.
        var next = await ConfirmedRunAsync(f.S);
        next = await f.S.Service.AssignSamplesAsync(next.Id, new List<int> { orderId }, f.S.UserId);
        Assert.Equal(IcpRunSampleStatus.Assigned, next.Samples.Single().Status);
    }

    // ---- specification linkage ----

    private static Specification ExtraSpec(Fixture f, string name, int? elementId) => new()
    {
        ItemId = f.Item.Id, TestCode = f.Definition.Code, ParameterName = name, IcpMethodElementId = elementId,
        ResultBasis = ResultBasis.MgPerUnit, LimitType = LimitType.Range, LowerLimit = 0.9m, UpperLimit = 1.1m,
        LabelClaim = 1m, LabelClaimUnit = "mg", Unit = "mg", DisplayOrder = 9,
    };

    [Fact]
    public async Task Entry_SpecForElementNotOnMethod_BlocksSubmit()
    {
        var (f, _, rsId) = await AssignedAsync();
        await SaveAsync(f, rsId, Rep(f, 0.5m, 0.8m));
        f.S.Db.Specifications.Add(ExtraSpec(f, "Fe (amount)", 99999));
        await f.S.Db.SaveChangesAsync();

        var entry = await f.S.Service.GetSampleEntryAsync(rsId, f.S.UserId);

        Assert.False(entry.CanSubmit);
        Assert.Equal("Specification \"Fe (amount)\" refers to an element that is not on this run's method.", entry.CanSubmitReason);
        Assert.Empty(entry.Preview);
    }

    [Fact]
    public async Task Entry_SpecWithoutElement_BlocksSubmit()
    {
        var (f, _, rsId) = await AssignedAsync();
        await SaveAsync(f, rsId, Rep(f, 0.5m, 0.8m));
        f.S.Db.Specifications.Add(ExtraSpec(f, "Loose", null));
        await f.S.Db.SaveChangesAsync();

        var entry = await f.S.Service.GetSampleEntryAsync(rsId, f.S.UserId);

        Assert.False(entry.CanSubmit);
        Assert.Equal("Specification \"Loose\" is not linked to an ICP method element.", entry.CanSubmitReason);
    }

    // ---- method options ----

    [Fact]
    public async Task MethodOptions_CountsOnlyEligibleOrders()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var busy = await AddOrderAsync(f, "BUSY");
        var finalized = await AddOrderAsync(f, "DONE");
        await AddOrderAsync(f, "FREE");
        finalized.CurrentStep = WorkflowStep.Reviewed;
        await s.Db.SaveChangesAsync();
        var run = await ConfirmedRunAsync(s);
        await s.Service.AssignSamplesAsync(run.Id, new List<int> { busy.Id }, s.UserId);

        var option = Assert.Single(await s.Service.GetMethodOptionsAsync(s.UserId));

        Assert.Equal(1, option.EligibleTestOrderCount);
    }

    // ---- assignment gate on the run DTO ----

    [Fact]
    public async Task RunDto_CanAssignSamples_FollowsCalibrationAndCcv()
    {
        var s = await SeedAsync(requireCcv: true);
        var run = await StartAsync(s);
        Assert.False(run.CanAssignSamples);
        Assert.Equal("Confirm the calibration before assigning samples.", run.CanAssignSamplesReason);

        await s.Service.SaveCalibrationAsync(run.Id, Save(run, s.CalLot.Id, znR: 0.9995m), s.UserId);
        await UploadCalibrationReportAsync(s, run.Id);
        run = await s.Service.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), s.UserId, null);
        Assert.False(run.CanAssignSamples);
        Assert.Equal("No element is valid on this run: Needs a passing CCV.", run.CanAssignSamplesReason);

        run = await s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(run.Method.Elements.First().Id, 1.0m), s.UserId);
        Assert.True(run.CanAssignSamples);
        Assert.Null(run.CanAssignSamplesReason);
    }

    [Fact]
    public async Task Preview_BlockedElement_ShowsProblem()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var order = await AddOrderAsync(f);
        var run = await ConfirmedRunAsync(s, znR: 0.9980m); // Zn fails calibration, Ca passes -> run can take samples
        run = await s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);

        var entry = await SaveAsync(f, run.Samples.Single().Id, Rep(f, 0.5m, 0.8m));

        var zn = Assert.Single(entry.Elements);
        Assert.False(zn.Valid);
        Assert.All(entry.Preview, p => Assert.StartsWith("Zn: Failed calibration", p.Problem));
        Assert.False(entry.CanSubmit);
    }
}
