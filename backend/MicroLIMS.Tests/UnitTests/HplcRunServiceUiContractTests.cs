using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Fields the HPLC workspace screens read: SST gating and live statistics,
// column designation check at run start, official results and reviewer evidence.
public partial class HplcRunServiceTests
{
    [Fact]
    public async Task GetRun_WithoutStandardReport_CannotConfirmSstAndSaysWhy()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock, standardInjections: 3);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        // Blank entries are reported first, then the missing report.
        Assert.False(run.CanConfirmSst);
        Assert.StartsWith("Complete these entries", run.CanConfirmSstReason);
        run = await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(run.Sst!.Analytes[0].Id, lot.Id, 50m, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);
        Assert.False(run.CanConfirmSst);
        Assert.Equal("Upload the standard report before confirming system suitability.", run.CanConfirmSstReason);
        Assert.Equal(s.StandardEntry.Id, run.Sst!.Analytes[0].StandardEntryId);
    }

    [Fact]
    public async Task SaveSst_StoresMeanAndRsdWithoutJudging()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock, sstMaxRsdPercent: 0.01m);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;

        SaveSstRequest Save(params decimal[] responses) => new(new List<SaveSstAnalyteInput> {
            new(analyteId, lot.Id, 50m, responses.ToList(), null, null, null, null, null, null, null) });

        var saved = (await service.SaveSstAsync(run.Id, Save(1000m, 1050m, 950m), s.UserId)).Sst!.Analytes[0];
        Assert.Equal(1000m, saved.MeanResponse);
        Assert.Equal(5.0m, Math.Round(saved.ComputedRsdPercent!.Value, 1));
        Assert.False(saved.Passed);
        Assert.Null(saved.FailureReasons);

        var single = (await service.SaveSstAsync(run.Id, Save(1000m), s.UserId)).Sst!.Analytes[0];
        Assert.Equal(1000m, single.MeanResponse);
        Assert.Null(single.ComputedRsdPercent);

        var none = (await service.SaveSstAsync(run.Id, Save(), s.UserId)).Sst!.Analytes[0];
        Assert.Null(none.MeanResponse);
        Assert.Null(none.ComputedRsdPercent);
    }

    [Fact]
    public async Task MethodOptions_CarryTheColumnDesignation()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);

        var options = await TestServiceFactory.HplcRun(db, clock: clock).GetMethodOptionsAsync(s.UserId);

        Assert.Equal("L1", Assert.Single(options).ColumnDesignation);
    }

    // ---- Column USP designation ----

    [Fact]
    public async Task StartRun_ColumnWithoutUspDesignation_Refused()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        s.Column.UspDesignation = null;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestServiceFactory.HplcRun(db, clock: clock).StartRunAsync(StartRequest(s), s.UserId));

        Assert.Equal("Column COL-01 has no USP designation; set it in the column master.", ex.Message);
    }

    [Fact]
    public async Task StartRun_ColumnOfAnotherDesignation_Refused()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        s.Column.UspDesignation = "L7";
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestServiceFactory.HplcRun(db, clock: clock).StartRunAsync(StartRequest(s), s.UserId));

        Assert.Equal("Column COL-01 is USP L7; method VIT-C requires L1.", ex.Message);
    }

    [Fact]
    public async Task StartRun_DesignationComparedCaseInsensitively()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        s.Column.UspDesignation = " l1 ";
        await db.SaveChangesAsync();

        var run = await TestServiceFactory.HplcRun(db, clock: clock).StartRunAsync(StartRequest(s), s.UserId);

        Assert.Equal(HplcRunStatus.Open, run.Status);
    }

    [Fact]
    public async Task ColumnMaster_NormalisesUspDesignation()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var columns = new ChromatographyColumnService(db, new UserSectionScopeService(db));

        var updated = await columns.UpdateAsync(s.Column.Id, new UpdateChromatographyColumnRequest(
            s.Column.Code, s.Column.Name, UspDesignation: "l7 "), s.UserId);
        Assert.Equal("L7", updated.UspDesignation);

        var cleared = await columns.UpdateAsync(s.Column.Id, new UpdateChromatographyColumnRequest(
            s.Column.Code, s.Column.Name, UspDesignation: "  "), s.UserId);
        Assert.Null(cleared.UspDesignation);
    }

    // ---- Official results / recorder payload / reviewer evidence ----

    [Fact]
    public async Task Entry_BeforeSubmit_HasNoOfficialResults()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);

        var entry = await t.Service.GetSampleEntryAsync(t.RunSampleId, t.Scenario.UserId);

        Assert.Empty(entry.Official);
    }

    [Fact]
    public async Task Entry_AfterSubmit_ListsTheOfficialResults()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db, ProductionStageRole.Bulk, responses: new[] { 1000m, 1100m });
        await SubmitAsync(db, t);

        var entry = await t.Service.GetSampleEntryAsync(t.RunSampleId, t.Scenario.UserId);

        Assert.Equal(2, entry.Official.Count);
        Assert.Equal("Vitamin C – replicate 1", entry.Official[0].ParameterName);
        Assert.Equal("AssayPercent", entry.Official[0].Quantity);
        Assert.Equal(new int?[] { 1, 2 }, entry.Official.Select(o => o.ReplicateNo).ToArray());
        Assert.Equal(ResultStatus.WithinLimits, entry.Official[0].Status);
        Assert.Equal(ResultStatus.OutOfSpecification, entry.Official[1].Status);
        Assert.False(string.IsNullOrEmpty(entry.Official[0].Display));
    }

    [Fact]
    public async Task Entry_OfficialResultWithUnreadablePayload_DefaultsToAssayPercent()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await SubmitAsync(db, t);
        foreach (var pr in db.ParameterResults.Where(p => p.TestOrderId == t.Order.Id)) pr.CalculationJson = "{not json";
        await db.SaveChangesAsync();

        var entry = await t.Service.GetSampleEntryAsync(t.RunSampleId, t.Scenario.UserId);

        var official = Assert.Single(entry.Official);
        Assert.Equal("AssayPercent", official.Quantity);
        Assert.Null(official.ReplicateNo);
    }

    [Fact]
    public async Task Submit_PayloadCarriesRunIdsAndDisplayedReplicateAssay()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        await SubmitAsync(db, t);

        var pr = await db.ParameterResults.SingleAsync(p => p.TestOrderId == t.Order.Id);
        using var doc = JsonDocument.Parse(pr.CalculationJson!);
        Assert.Equal(t.Run.Id, doc.RootElement.GetProperty("hplcRunId").GetInt32());
        Assert.Equal(t.RunSampleId, doc.RootElement.GetProperty("hplcRunSampleId").GetInt32());
        var replicate = doc.RootElement.GetProperty("replicates")[0];
        Assert.Equal("99.0 %", replicate.GetProperty("assayPercentDisplay").GetString());
    }

    [Fact]
    public async Task TestOrderEvidence_ListsCurrentSampleAndSstStandardReports_NewestFirst()
    {
        await using var db = NewDb();
        var t = await ArrangeSubmittableAsync(db);
        var other = await t.Service.UploadEvidenceAsync(t.Run.Id, t.RunSampleId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport,
            "old.pdf", "application/pdf", PdfBytes(), t.Scenario.UserId);
        await t.Service.SupersedeEvidenceAsync(other.Id, "wrong file", "new.pdf", "application/pdf", PdfBytes(), t.Scenario.UserId);

        var evidence = await t.Service.GetTestOrderEvidenceAsync(t.Order.Id, t.Scenario.UserId);

        Assert.All(evidence, e => Assert.True(e.IsCurrent));
        Assert.DoesNotContain(evidence, e => e.Id == other.Id);
        Assert.Contains(evidence, e => e.Context == HplcEvidenceContext.Sst && e.Kind == HplcEvidenceKind.StandardReport);
        Assert.Equal(2, evidence.Count(e => e.Context == HplcEvidenceContext.Sample));
        Assert.Equal(evidence.OrderByDescending(e => e.UploadedAt).Select(e => e.Id), evidence.Select(e => e.Id));
    }
}
