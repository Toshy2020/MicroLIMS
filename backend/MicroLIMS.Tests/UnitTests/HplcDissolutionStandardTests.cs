using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcDissolutionStandardTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };

    // Seeds a run (method snapshot with one analyte), its SST and one assigned
    // run sample for the test order. Returns the run so tests can alter it.
    // The id parameters default to InMemory placeholders; the Postgres tests
    // pass real rows (foreign keys are enforced there).
    internal static HplcRun SeedRun(MicroLimsDbContext db, int testOrderId = 100,
        HplcSstStatus sst = HplcSstStatus.Passed, HplcRunStatus runStatus = HplcRunStatus.Open,
        decimal? dilution = 2500m, int analyteCount = 1,
        int sectionId = 1, int equipmentId = 7, int columnId = 1, int methodId = 1, int userId = 1,
        int firstAnalyteId = 1, string codeSuffix = "")
    {
        var analytes = Enumerable.Range(0, analyteCount).Select(i => new HplcMethodAnalyteResponse(
            firstAnalyteId + i, i + 1, $"Analyte {i + 1}", 290m, 1, "STD", 50m, 50m, 5,
            null, null, null, null, null, null, null, dilution)).ToList();
        var run = new HplcRun
        {
            SectionId = sectionId, Code = "SILD RUN 01/102026" + codeSuffix, EquipmentId = equipmentId, ChromatographyColumnId = columnId, HplcMethodId = methodId,
            MethodSnapshotJson = JsonSerializer.Serialize(new HplcMethodResponse { Id = methodId, Name = "M", Abbreviation = "SILD", ColumnDesignation = "L1", Analytes = analytes }, SnapshotJson.Options),
            AnalystUserId = userId, StartedAt = DateTime.UtcNow, Status = runStatus,
            Sst = new HplcSstRecord
            {
                Code = "SILD S.S 01/102026" + codeSuffix, Status = sst,
                Analytes = analytes.Select(a => new HplcSstAnalyte
                {
                    HplcMethodAnalyteId = a.Id, AnalyteName = a.Name,
                    StandardWeightMg = 50m, StandardPurityPercent = 100m, StandardMoisturePercent = 0m, MeanResponse = 0.500m
                }).ToList()
            },
            Samples = { new HplcRunSample { TestOrderId = testOrderId, Status = HplcRunSampleStatus.Assigned, AssignedAt = DateTime.UtcNow, AssignedByUserId = userId } }
        };
        db.HplcRuns.Add(run);
        db.SaveChanges();
        return run;
    }

    [Fact]
    public async Task Standard_FromAssignedPassedRun()
    {
        await using var db = NewDb();
        var run = SeedRun(db);

        var result = await HplcDissolutionStandard.ResolveAsync(db, 100);

        Assert.Null(result.Problem);
        Assert.Equal(run.Id, result.Standard!.HplcRunId);
        Assert.Equal(7, result.Standard.EquipmentId);
        Assert.Equal(0.02m, result.Standard.Cs); // 50 x 1.0 x 1.0 / 2500
    }

    [Fact]
    public async Task Standard_NotAssigned_IsMissing()
    {
        await using var db = NewDb();
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_RemovedSample_IsMissing()
    {
        await using var db = NewDb();
        var run = SeedRun(db);
        run.Samples[0].Status = HplcRunSampleStatus.Removed;
        db.SaveChanges();
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_AbandonedRun_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, runStatus: HplcRunStatus.Abandoned);
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_SstNotPassed_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, sst: HplcSstStatus.Pending);
        Assert.Contains("has not passed", (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_NoDilution_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, dilution: null);
        Assert.Contains("standard dilution", (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }
}
