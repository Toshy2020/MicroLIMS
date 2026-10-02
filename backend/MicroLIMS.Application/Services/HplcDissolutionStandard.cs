using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public sealed record HplcDissolutionStandardValues(
    int HplcRunId, int RunSampleId, int EquipmentId, string RunCode, string SstCode,
    decimal MeanResponse, decimal StandardWeightMg, decimal PurityPercent, decimal MoisturePercent,
    decimal StandardDilution, decimal Cs);

public sealed record HplcDissolutionStandardResult(HplcDissolutionStandardValues? Standard, string? Problem);

// The dissolution standard for one test order, taken from the HPLC workspace
// run it is assigned to (one source for the recorder, the read endpoint and
// the approval gate). Dissolution methods have exactly one analyte.
public static class HplcDissolutionStandard
{
    public const string NotAssigned = "Assign this sample to an HPLC run with a passed system suitability.";

    public static async Task<HplcDissolutionStandardResult> ResolveAsync(IMicroLimsDbContext db, int testOrderId, CancellationToken ct = default)
    {
        var runSample = await db.HplcRunSamples
            .Include(s => s.HplcRun!).ThenInclude(r => r.Sst!).ThenInclude(s => s.Analytes)
            .Where(s => s.TestOrderId == testOrderId && s.Status == HplcRunSampleStatus.Assigned
                && s.HplcRun!.Status != HplcRunStatus.Abandoned)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
        if (runSample?.HplcRun?.Sst == null)
            return new(null, NotAssigned);

        var run = runSample.HplcRun;
        if (run.Sst!.Status != HplcSstStatus.Passed)
            return new(null, $"System suitability {run.Sst.Code} has not passed.");

        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, SnapshotJson.Options);
        if (snapshot == null || snapshot.Analytes.Count != 1)
            return new(null, "Dissolution needs an HPLC method with exactly one analyte.");
        var analyte = snapshot.Analytes[0];
        if (analyte.StandardDilution is not > 0m)
            return new(null, $"Method {snapshot.Abbreviation} has no standard dilution for {analyte.Name}.");

        var sst = run.Sst.Analytes.FirstOrDefault(a => a.HplcMethodAnalyteId == analyte.Id);
        if (sst?.MeanResponse is not > 0m || sst.StandardWeightMg is not > 0m
            || sst.StandardPurityPercent is not > 0m || sst.StandardMoisturePercent is null)
            return new(null, $"{analyte.Name}: the system suitability standard values are incomplete.");

        var cs = DissolutionCalculator.CalculateCs(sst.StandardWeightMg.Value, sst.StandardPurityPercent.Value,
            sst.StandardMoisturePercent.Value, analyte.StandardDilution.Value);
        return new(new HplcDissolutionStandardValues(
            run.Id, runSample.Id, run.EquipmentId, run.Code, run.Sst.Code,
            sst.MeanResponse.Value, sst.StandardWeightMg.Value, sst.StandardPurityPercent.Value,
            sst.StandardMoisturePercent.Value, analyte.StandardDilution.Value, cs), null);
    }
}
