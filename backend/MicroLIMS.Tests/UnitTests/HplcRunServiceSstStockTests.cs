using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Confirming SST deducts the standard weight (mg) from the standard lot.
public partial class HplcRunServiceTests
{
    private static async Task<(HplcRunService Service, int RunId, Material Lot, int UserId)> ArrangeAsync(
        MicroLimsDbContext db, MaterialUnit unit, decimal remaining, decimal weightMg, decimal[] responses, bool duplicateAnalyte = false)
    {
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, quantityRemaining: remaining);
        lot.Unit = unit;
        await db.SaveChangesAsync();
        var service = TestServiceFactory.HplcRun(db, clock: clock);
        var run = await service.StartRunAsync(StartRequest(s), s.UserId);
        var analyteId = run.Sst!.Analytes[0].Id;
        await service.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput> {
            new(analyteId, lot.Id, weightMg, responses.ToList(), null, null, null, null, null, null, null) }), s.UserId);
        if (duplicateAnalyte)
        {
            var first = await db.HplcSstAnalytes.Include(a => a.Injections).FirstAsync(a => a.Id == analyteId);
            var copy = new HplcSstAnalyte
            {
                HplcSstRecordId = first.HplcSstRecordId, HplcMethodAnalyteId = first.HplcMethodAnalyteId,
                AnalyteName = first.AnalyteName, StandardMaterialId = lot.Id, StandardWeightMg = weightMg,
                StandardPurityPercent = first.StandardPurityPercent, StandardMoisturePercent = first.StandardMoisturePercent,
                Injections = first.Injections.Select(i => new HplcSstInjection { InjectionNo = i.InjectionNo, Response = i.Response }).ToList(),
            };
            db.HplcSstAnalytes.Add(copy);
            await db.SaveChangesAsync();
        }
        await service.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport, "report.pdf", "application/pdf",
            PdfBytes(), s.UserId);
        return (service, run.Id, lot, s.UserId);
    }

    private static readonly decimal[] Good = { 1000m, 1000m, 1000m };

    private static Task ConfirmSstOnlyAsync(HplcRunService svc, int runId, int userId) =>
        svc.ConfirmSstAsync(runId, new ConfirmSstRequest("ValidPassword123!", null), userId, null);

    [Fact]
    public async Task Passed_DeductsWeightFromLot()
    {
        await using var db = NewDb();
        var (svc, runId, lot, uid) = await ArrangeAsync(db, MaterialUnit.Gram, 100m, 50m, Good);
        await ConfirmSstOnlyAsync(svc, runId, uid);
        Assert.Equal(99.95m, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lot.Id)).QuantityRemaining);
    }

    [Fact]
    public async Task Failed_AlsoDeducts()
    {
        await using var db = NewDb();
        var (svc, runId, lot, uid) = await ArrangeAsync(db, MaterialUnit.Gram, 100m, 50m, new[] { 1000m, 1500m, 500m });
        var run = await svc.ConfirmSstAsync(runId, new ConfirmSstRequest("ValidPassword123!", null), uid, null);
        Assert.Equal(HplcSstStatus.Failed, run.Sst!.Status);
        Assert.Equal(99.95m, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lot.Id)).QuantityRemaining);
    }

    [Fact]
    public async Task SameLotTwice_IsSummed()
    {
        await using var db = NewDb();
        var (svc, runId, lot, uid) = await ArrangeAsync(db, MaterialUnit.Gram, 100m, 50m, Good, duplicateAnalyte: true);
        await ConfirmSstOnlyAsync(svc, runId, uid);
        Assert.Equal(99.9m, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lot.Id)).QuantityRemaining);
    }

    [Fact]
    public async Task InsufficientStock_RefusesAndLeavesEverythingUnchanged()
    {
        await using var db = NewDb();
        var (svc, runId, lot, uid) = await ArrangeAsync(db, MaterialUnit.Gram, 0.01m, 50m, Good);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ConfirmSstOnlyAsync(svc, runId, uid));
        Assert.Contains(lot.LotLabel, ex.Message);
        Assert.Equal(0.01m, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lot.Id)).QuantityRemaining);
        Assert.Equal(HplcSstStatus.Pending, (await svc.GetRunAsync(runId, uid)).Sst!.Status);
        Assert.Equal(0, await db.ElectronicSignatures.CountAsync(x => x.MeaningOfSignature == SignatureMeaning.SuitabilityRunPerformed));
    }

    [Fact]
    public async Task KilogramLot_Converts()
    {
        await using var db = NewDb();
        var (svc, runId, lot, uid) = await ArrangeAsync(db, MaterialUnit.Kilogram, 1m, 50m, Good);
        await ConfirmSstOnlyAsync(svc, runId, uid);
        Assert.Equal(1m - 0.00005m, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lot.Id)).QuantityRemaining);
    }

    [Fact]
    public async Task NonMassUnit_RefusedBeforeSigning()
    {
        await using var db = NewDb();
        var (svc, runId, lot, uid) = await ArrangeAsync(db, MaterialUnit.Milliliter, 100m, 50m, Good);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ConfirmSstOnlyAsync(svc, runId, uid));
        Assert.Contains("cannot be deducted", ex.Message);
        Assert.Equal(100m, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lot.Id)).QuantityRemaining);
        Assert.Equal(0, await db.ElectronicSignatures.CountAsync(x => x.MeaningOfSignature == SignatureMeaning.SuitabilityRunPerformed));
    }
}
