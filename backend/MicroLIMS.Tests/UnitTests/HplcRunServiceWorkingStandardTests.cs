using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Working standard qualification samples on HPLC runs (spec 2026-10-03, Task 5).
public partial class HplcRunServiceTests
{
    private static async Task<WorkingStandardQualification> AddQualificationAsync(
        MicroLimsDbContext db, Scenario s, decimal? moisture = 0.5m, string code = "WSQ-01/09/2026")
    {
        var q = new WorkingStandardQualification
        {
            SectionId = s.Section.Id, Code = code, Kind = WorkingStandardQualificationKind.Initial,
            Status = WorkingStandardQualificationStatus.Draft, MaterialMasterEntryId = s.StandardEntry.Id,
            SourceSampleId = null, SourceMaterialName = "Vitamin C API", SourceBatchNumber = "RM-1",
            QuantityGrams = 10m, Location = "Fridge", MoisturePercent = moisture,
            CreatedByUserId = s.UserId, CreatedAt = DateTime.UtcNow,
        };
        q.Documents.Add(new WorkingStandardDocument { Kind = WorkingStandardDocumentKind.SourceReport, FileName = "r.pdf",
            ContentType = "application/pdf", FilePath = "x", UploadedByUserId = s.UserId, UploadedAt = DateTime.UtcNow });
        db.WorkingStandardQualifications.Add(q);
        await db.SaveChangesAsync();
        return q;
    }

    private static SaveReplicatesRequest SixReplicates(int analyteId, decimal response = 1000m) =>
        new(Enumerable.Range(1, 6).Select(_ => new HplcReplicateInput(50m,
            new List<HplcReplicateResponseInput> { new(analyteId, response) })).ToList());

    private static async Task<(HplcRunService Service, HplcRunDto Run, WorkingStandardQualification Q, int RunSampleId)> AssignedWsAsync(
        MicroLimsDbContext db, ILabClock clock, Scenario s)
    {
        var q = await AddQualificationAsync(db, s);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        var assigned = await service.AssignQualificationsAsync(run.Id, new List<int> { q.Id }, s.UserId);
        return (service, assigned, q, assigned.Samples.Single(x => x.WorkingStandardQualificationId == q.Id).Id);
    }

    [Fact]
    public async Task WsEligible_ListsDraftForRunStandardEntry()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var q = await AddQualificationAsync(db, s);
        var noMoisture = await AddQualificationAsync(db, s, moisture: null, code: "WSQ-02/09/2026");
        var (service, run) = await StartPassedRunAsync(db, clock, s);

        var list = await service.GetEligibleQualificationsAsync(run.Id, s.UserId);

        var row = Assert.Single(list);
        Assert.Equal(q.Id, row.QualificationId);
        Assert.Equal(q.Code, row.Code);
        Assert.DoesNotContain(list, x => x.QualificationId == noMoisture.Id);
    }

    [Fact]
    public async Task WsAssign_SstUsedWorkingStandard_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var q = await AddQualificationAsync(db, s);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        lot.MaterialType = MaterialType.WorkingStandard;
        await db.SaveChangesAsync();
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(run.Sst!.Analytes[0].Id, lot.Id, 50m, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "std.pdf", "application/pdf", PdfBytes(), s.UserId);
        await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), s.UserId, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignQualificationsAsync(run.Id, new List<int> { q.Id }, s.UserId));
        Assert.Contains("primary reference standard", ex.Message);
    }

    [Fact]
    public async Task WsEligible_SstUsedWorkingStandard_Empty()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var q = await AddQualificationAsync(db, s);
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry);
        lot.MaterialType = MaterialType.WorkingStandard;
        await db.SaveChangesAsync();
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(run.Sst!.Analytes[0].Id, lot.Id, 50m, new List<decimal> { 1000m, 1000m, 1000m }, null, null, null, null, null, null, null) }), s.UserId);
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "std.pdf", "application/pdf", PdfBytes(), s.UserId);
        await service.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), s.UserId, null);

        Assert.Empty(await service.GetEligibleQualificationsAsync(run.Id, s.UserId));
    }

    [Fact]
    public async Task WsAssign_SetsAnalyteAndShowsInRunSummary()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var (service, run, q, _) = await AssignedWsAsync(db, clock, s);

        var row = Assert.Single(run.Samples);
        Assert.Null(row.TestOrderId);
        Assert.Equal("WS qualification", row.TestCode);
        Assert.Equal(q.Code, row.SampleNumber);
        Assert.False(row.Submitted);
        Assert.Equal(q.Id, row.WorkingStandardQualificationId);
        var analyte = await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == s.Method.Id);
        Assert.Equal(analyte.Id, (await db.WorkingStandardQualifications.FindAsync(q.Id))!.HplcMethodAnalyteId);
        Assert.Empty(await service.GetEligibleQualificationsAsync(run.Id, s.UserId));
    }

    [Fact]
    public async Task WsEntry_FiveReplicates_NoPreviewAndCannotSubmit()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var (service, _, q, rsId) = await AssignedWsAsync(db, clock, s);
        var analyteId = (await db.WorkingStandardQualifications.FindAsync(q.Id))!.HplcMethodAnalyteId!.Value;

        var five = new SaveReplicatesRequest(SixReplicates(analyteId).Replicates.Take(5).ToList());
        var entry = await service.SaveQualificationReplicatesAsync(rsId, five, s.UserId);

        Assert.Null(entry.Preview);
        Assert.False(entry.CanSubmit);
        Assert.Contains("Exactly 6 replicates are required (5 entered)", entry.CanSubmitReason);
        Assert.Equal(6, entry.RequiredReplicates);
        Assert.Single(entry.MethodWeights);
    }

    [Fact]
    public async Task WsSubmit_WithoutSampleReport_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var (service, _, q, rsId) = await AssignedWsAsync(db, clock, s);
        var analyteId = (await db.WorkingStandardQualifications.FindAsync(q.Id))!.HplcMethodAnalyteId!.Value;
        await service.SaveQualificationReplicatesAsync(rsId, SixReplicates(analyteId), s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitQualificationAsync(rsId, new SubmitHplcSampleRequest(Password, null), s.UserId, null));
        Assert.Equal("Upload the sample report before submitting.", ex.Message);
    }

    [Fact]
    public async Task WsSubmit_ComputesAndMovesToAssayed()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var (service, run, q, rsId) = await AssignedWsAsync(db, clock, s);
        var analyte = await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == s.Method.Id);
        await service.SaveQualificationReplicatesAsync(rsId, SixReplicates(analyte.Id), s.UserId);
        await service.UploadEvidenceAsync(run.Id, rsId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport,
            "s.pdf", "application/pdf", PdfBytes(), s.UserId);

        var entry = await service.SubmitQualificationAsync(rsId, new SubmitHplcSampleRequest(Password, null), s.UserId, null);

        var expected = StandardComparisonCalculator.CalculatePreparationAssay(
            1000m, 1000m, 50m, analyte.TheoreticalWeightStdMg, analyte.TheoreticalWeightTestMg, 50m, 0.5m, 99.5m);
        var saved = await db.WorkingStandardQualifications.AsNoTracking().FirstAsync(x => x.Id == q.Id);
        Assert.Equal(expected, saved.MeanAssayPercent);
        Assert.Equal(0m, saved.RsdPercent);
        Assert.Equal(Math.Round(expected * 100m / (100m - saved.MoisturePercent!.Value), 3, MidpointRounding.AwayFromZero), saved.PotencyPercent);
        Assert.Equal(WorkingStandardQualificationStatus.Assayed, saved.Status);
        Assert.NotNull(saved.PreparedSignatureId);
        Assert.True(saved.Passed);
        Assert.NotNull(saved.ReplicateAssaysJson);
        Assert.True(entry.Submitted);
        Assert.False(entry.Editable);
    }

    [Fact]
    public async Task WsRemove_AfterSubmit_Throws()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var (service, run, q, rsId) = await AssignedWsAsync(db, clock, s);
        var analyteId = (await db.WorkingStandardQualifications.FindAsync(q.Id))!.HplcMethodAnalyteId!.Value;
        await service.SaveQualificationReplicatesAsync(rsId, SixReplicates(analyteId), s.UserId);
        await service.UploadEvidenceAsync(run.Id, rsId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport,
            "s.pdf", "application/pdf", PdfBytes(), s.UserId);
        await service.SubmitQualificationAsync(rsId, new SubmitHplcSampleRequest(Password, null), s.UserId, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemoveSampleAsync(run.Id, rsId, "oops", s.UserId));
        Assert.Equal("The qualification has already been submitted.", ex.Message);
    }

    [Fact]
    public async Task CompleteRun_DraftQualificationPending_Throws_ProductAndWsCounted()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var f = await SeedAssayAsync(db, s);
        var order = await AddAssayOrderAsync(db, s, f);
        var q = await AddQualificationAsync(db, s);
        var (service, run) = await StartPassedRunAsync(db, clock, s);
        await service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        await service.AssignQualificationsAsync(run.Id, new List<int> { q.Id }, s.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteRunAsync(run.Id, s.UserId));
        Assert.StartsWith("2 assigned sample(s)", ex.Message);
    }

    [Fact]
    public async Task ProductEntry_OnWsRunSample_ThrowsClearError()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var (service, _, _, rsId) = await AssignedWsAsync(db, clock, s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSampleEntryAsync(rsId, s.UserId));
        Assert.Equal("This run sample is a working standard qualification - open it from its qualification entry.", ex.Message);
    }

    [Fact]
    public async Task WsEntry_AnalyteRemovedFromSnapshot_ThrowsClearError()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var (service, _, q, rsId) = await AssignedWsAsync(db, clock, s);
        (await db.WorkingStandardQualifications.FindAsync(q.Id))!.HplcMethodAnalyteId = 999999;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetQualificationEntryAsync(rsId, s.UserId));
        Assert.Equal("This qualification is no longer measured on this run's method analyte.", ex.Message);
    }

    [Fact]
    public async Task WsEntry_AfterSubmit_ShowsStoredPreview()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var (service, run, q, rsId) = await AssignedWsAsync(db, clock, s);
        var analyte = await db.HplcMethodAnalytes.FirstAsync(a => a.HplcMethodId == s.Method.Id);
        await service.SaveQualificationReplicatesAsync(rsId, SixReplicates(analyte.Id), s.UserId);
        await service.UploadEvidenceAsync(run.Id, rsId, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport,
            "s.pdf", "application/pdf", PdfBytes(), s.UserId);
        await service.SubmitQualificationAsync(rsId, new SubmitHplcSampleRequest(Password, null), s.UserId, null);

        var entry = await service.GetQualificationEntryAsync(rsId, s.UserId);

        var saved = await db.WorkingStandardQualifications.AsNoTracking().FirstAsync(x => x.Id == q.Id);
        Assert.NotNull(entry.Preview);
        Assert.Equal(saved.MeanAssayPercent, entry.Preview!.MeanAssayPercent);
        Assert.Equal(saved.PotencyPercent, entry.Preview.PotencyPercent);
        Assert.Equal(saved.RsdPercent, entry.Preview.RsdPercent);
        Assert.Equal(6, entry.Preview.ReplicateAssayPercents.Count);
        Assert.True(entry.Preview.Passed);
    }
}
