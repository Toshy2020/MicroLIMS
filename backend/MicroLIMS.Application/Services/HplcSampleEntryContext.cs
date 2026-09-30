using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Everything needed to show, preview and submit one sample's HPLC entry.
// Loaded once and shared by HplcRunService (entry screen) and
// HplcMethodAssayRecorder (submission) so the submission gate has a single
// definition (HPLC chain S6 Part B).
public sealed class HplcSampleEntryContext
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public required HplcRunSample RunSample { get; init; }
    public required HplcRun Run { get; init; }
    public required TestOrder Order { get; init; }
    public required Sample Sample { get; init; }
    public required TestDefinition Definition { get; init; }
    public required HplcMethodResponse Snapshot { get; init; }
    public required StageReplicateResolution Replicates { get; init; }
    public required List<Specification> Specs { get; init; }
    public required bool Submitted { get; init; }
    public required bool Returned { get; init; }
    public required bool HasCurrentSampleReport { get; init; }

    public ProductionStageRole? StageRole => Replicates.StageRole;
    public string? StageName => Sample.ProductionStageRef?.Name ?? Sample.ProductionStage;

    // Editable while the run is open, or - after a submitted result was
    // returned to the analyst - until it is submitted again.
    public bool Editable => EditableProblem() == null;

    public string? EditableProblem()
    {
        if (RunSample.Status != HplcRunSampleStatus.Assigned) return "This sample was removed from the run.";
        if (Submitted) return "The result has already been sent for review.";
        if (Run.Status == HplcRunStatus.Open) return null;
        if (Run.Status == HplcRunStatus.Completed && Returned) return null;
        return "The run is closed.";
    }

    // First reason the sample cannot be sent for review, or null.
    public string? SubmitProblem() =>
        EntryProblem() ?? (HasCurrentSampleReport ? null : "Upload the sample report before sending for review.");

    // Everything the submission needs except the sample report: when this is
    // null the assay can be calculated (and previewed).
    public string? EntryProblem()
    {
        var editable = EditableProblem();
        if (editable != null) return editable;
        if (Run.Sst?.Status != HplcSstStatus.Passed) return "System suitability has not passed.";
        if (!Replicates.IsConfigured) return Replicates.Message ?? "Stage replicate configuration could not be resolved.";

        var required = Replicates.SampleReplicates!.Value;
        var entered = RunSample.Replicates.Count;
        if (entered != required)
            return $"Expected exactly {required} replicates for stage role {Replicates.StageRole}, but {entered} entered.";

        foreach (var analyte in Snapshot.Analytes)
        {
            foreach (var rep in RunSample.Replicates.OrderBy(r => r.ReplicateNo))
            {
                if (rep.ActualWeightMg <= 0)
                    return $"Replicate {rep.ReplicateNo}: the sample weight is required.";
                if (!rep.Responses.Any(x => x.HplcMethodAnalyteId == analyte.Id && x.Response > 0))
                    return $"Replicate {rep.ReplicateNo}: a response for {analyte.Name} is required.";
            }

            if (!Specs.Any(s => s.HplcMethodAnalyteId == analyte.Id && s.ResultBasis == ResultBasis.PercentLabelClaim))
                return $"No assay specification for {analyte.Name} on this item.";

            var sstRow = Run.Sst!.Analytes.FirstOrDefault(a => a.HplcMethodAnalyteId == analyte.Id);
            if (sstRow?.MeanResponse is null or <= 0 || sstRow.StandardWeightMg is null or <= 0
                || sstRow.StandardPurityPercent is null or <= 0 || sstRow.StandardMoisturePercent is null)
                return $"{analyte.Name}: the system suitability standard values are incomplete.";
        }

        return null;
    }

    public List<HplcAssayAnalyteInput> BuildAnalyteInputs()
    {
        var inputs = new List<HplcAssayAnalyteInput>();
        foreach (var analyte in Snapshot.Analytes.OrderBy(a => a.DisplayOrder))
        {
            var sstRow = Run.Sst!.Analytes.First(a => a.HplcMethodAnalyteId == analyte.Id);
            var reps = RunSample.Replicates.OrderBy(r => r.ReplicateNo)
                .Select(r => new AssayReplicateInput(r.ReplicateNo, r.ActualWeightMg,
                    r.Responses.First(x => x.HplcMethodAnalyteId == analyte.Id).Response))
                .ToList();
            var std = new AssayStandard(sstRow.MeanResponse!.Value, sstRow.StandardWeightMg!.Value,
                sstRow.StandardPurityPercent!.Value, sstRow.StandardMoisturePercent!.Value);
            inputs.Add(new HplcAssayAnalyteInput(
                analyte.Id, analyte.Name, analyte.TheoreticalWeightStdMg, analyte.TheoreticalWeightTestMg, std, reps,
                Specs.First(s => s.HplcMethodAnalyteId == analyte.Id && s.ResultBasis == ResultBasis.PercentLabelClaim),
                Specs.FirstOrDefault(s => s.HplcMethodAnalyteId == analyte.Id && s.ResultBasis == ResultBasis.MgPerUnit)));
        }
        return inputs;
    }

    public static async Task<HplcSampleEntryContext> LoadAsync(IMicroLimsDbContext db, int runSampleId, CancellationToken ct = default)
    {
        var runSample = await db.HplcRunSamples
            .Include(s => s.Replicates).ThenInclude(r => r.Responses)
            .FirstOrDefaultAsync(s => s.Id == runSampleId, ct)
            ?? throw new NotFoundException($"Run sample {runSampleId} not found.");

        var run = await db.HplcRuns
            .Include(r => r.Sst).ThenInclude(s => s!.Analytes)
            .Include(r => r.Evidence)
            .FirstAsync(r => r.Id == runSample.HplcRunId, ct);

        var order = await db.TestOrders
            .Include(o => o.Sample).ThenInclude(s => s!.Item)
            .Include(o => o.Sample).ThenInclude(s => s!.ProductionStageRef)
            .FirstAsync(o => o.Id == runSample.TestOrderId, ct);

        var definition = await db.TestDefinitions.FirstAsync(t => t.Code == order.TestCode, ct);
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The run's method snapshot could not be read.");

        var specs = order.Sample?.ItemId is int itemId
            ? await db.Specifications.Where(s => s.ItemId == itemId && s.TestCode == order.TestCode && s.HplcMethodAnalyteId != null).ToListAsync(ct)
            : new List<Specification>();

        var submitted = await db.TestAnalyses.AnyAsync(a => a.TestOrderId == order.Id && a.IsActive, ct);
        var returned = !submitted && await db.TestReturnEvents.AnyAsync(e => e.TestOrderId == order.Id, ct);
        var replicates = await StageReplicateResolver.ResolveForTestOrderAsync(db, order.Id, ct);
        var hasReport = run.Evidence.Any(e => e.HplcRunSampleId == runSample.Id
            && e.Kind == HplcEvidenceKind.SampleReport && e.SupersededByEvidenceId == null);

        return new HplcSampleEntryContext
        {
            RunSample = runSample, Run = run, Order = order, Sample = order.Sample!, Definition = definition,
            Snapshot = snapshot, Replicates = replicates, Specs = specs,
            Submitted = submitted, Returned = returned, HasCurrentSampleReport = hasReport,
        };
    }
}
