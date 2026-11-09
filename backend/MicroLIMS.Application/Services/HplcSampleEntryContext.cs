using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Everything needed to show, preview and submit one sample's HPLC entry.
// Loaded once and shared by HplcRunService (entry screen) and
// HplcMethodAssayRecorder (submission) so the submission gate has a single
// definition (HPLC chain S6 Part B).
public sealed class HplcSampleEntryContext
{
    private static readonly JsonSerializerOptions JsonOptions = SnapshotJson.Options;

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
    public MoistureRole MoistureRole { get; init; } = MoistureRole.None;
    public SampleMoisture? Moisture { get; init; }
    public string? MoistureError { get; init; }

    public ProductionStageRole? StageRole => Replicates.StageRole;
    public bool IsResidualSolvents => Snapshot.ResultMode == HplcResultMode.ResidualSolvents;
    public bool IsRelatedSubstances => Snapshot.ResultMode == HplcResultMode.RelatedSubstances;
    public bool IsCleaningResidue => Snapshot.ResultMode == HplcResultMode.CleaningResidue;
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

        var entered = RunSample.Replicates.Count;
        if (Replicates.SampleReplicates is not int required)
        {
            if (entered == 0) return "Enter at least one replicate.";
        }
        else if (entered != required)
        {
            var forStage = Replicates.StageRole is { } role ? $" for stage role {role}" : "";
            return $"Expected exactly {required} replicates{forStage}, but {entered} entered.";
        }

        if (Sample.Category == SampleCategory.AfterCleaning && Snapshot.ResultMode != HplcResultMode.CleaningResidue)
            return "Use a cleaning-residue method for after-cleaning samples.";

        if (IsRelatedSubstances) return RelatedSubstancesEntryProblem();

        if (IsCleaningResidue)
        {
            foreach (var analyte in Snapshot.Analytes)
            {
                foreach (var rep in RunSample.Replicates.OrderBy(r => r.ReplicateNo))
                {
                    if (!rep.Responses.Any(x => x.HplcMethodAnalyteId == analyte.Id && x.Response >= 0))
                        return $"Replicate {rep.ReplicateNo}: a response for {analyte.Name} is required.";
                }

                var sstRow = Run.Sst!.Analytes.FirstOrDefault(a => a.HplcMethodAnalyteId == analyte.Id);
                if (sstRow?.MeanResponse is null or <= 0)
                    return $"{analyte.Name}: the system suitability standard values are incomplete.";

                if (!Specs.Any(s => s.HplcMethodAnalyteId == analyte.Id))
                    return $"No cleaning-residue specification for {analyte.Name}.";
            }
            return null;
        }

        if (Snapshot.ResultMode == HplcResultMode.Assay && MoistureRole != MoistureRole.None)
        {
            if (MoistureError != null) return MoistureError;
            if (Moisture == null) return SampleMoistureResolver.MissingMessage(MoistureRole);
        }

        foreach (var analyte in Snapshot.Analytes)
        {
            foreach (var rep in RunSample.Replicates.OrderBy(r => r.ReplicateNo))
            {
                if (rep.ActualWeightMg <= 0)
                    return $"Replicate {rep.ReplicateNo}: the sample weight is required.";
                if (!string.IsNullOrWhiteSpace(Snapshot.InternalStandardName) && rep.InternalStandardResponse is null or <= 0)
                    return $"Replicate {rep.ReplicateNo}: the internal standard response is required.";
                if (!rep.Responses.Any(x => x.HplcMethodAnalyteId == analyte.Id && (IsResidualSolvents ? x.Response >= 0 : x.Response > 0)))
                    return $"Replicate {rep.ReplicateNo}: a response for {analyte.Name} is required.";
            }

            var sstRow = Run.Sst!.Analytes.FirstOrDefault(a => a.HplcMethodAnalyteId == analyte.Id);
            if (IsResidualSolvents)
            {
                if (!Specs.Any(s => s.HplcMethodAnalyteId == analyte.Id && s.ResultBasis == ResultBasis.Ppm))
                    return $"No residual-solvent specification for {analyte.Name} on this item.";
                if (sstRow?.MeanResponse is null or <= 0)
                    return $"{analyte.Name}: the system suitability standard values are incomplete.";
                continue;
            }

            if (!Specs.Any(s => s.HplcMethodAnalyteId == analyte.Id && (s.ResultBasis is ResultBasis.PercentLabelClaim or ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis)))
                return $"No assay specification for {analyte.Name} on this item.";

            if (sstRow?.MeanResponse is null or <= 0 || sstRow.StandardWeightMg is null or <= 0
                || sstRow.StandardPurityPercent is null or <= 0 || sstRow.StandardMoisturePercent is null)
                return $"{analyte.Name}: the system suitability standard values are incomplete.";
        }

        return null;
    }

    public List<HplcAssayRow> EvaluateRows(decimal? moisturePercentOverride = null) => Snapshot.ResultMode switch
    {
        HplcResultMode.Assay => HplcSampleAssayEvaluator.Evaluate(BuildAnalyteInputs(moisturePercentOverride), StageRole),
        HplcResultMode.ResidualSolvents => HplcResidualSolventEvaluator.Evaluate(BuildResidualSolventInputs()),
        HplcResultMode.RelatedSubstances => CalculateRelatedSubstances().Select(r => r.Row).ToList(),
        HplcResultMode.CleaningResidue => CleaningResidueCalculator.Evaluate(BuildCleaningResidueInputs()),
        _ => throw new InvalidOperationException($"Unsupported result mode {Snapshot.ResultMode}."),
    };

    public List<HplcCleaningResidueInput> BuildCleaningResidueInputs()
    {
        var inputs = new List<HplcCleaningResidueInput>();
        foreach (var analyte in Snapshot.Analytes.OrderBy(a => a.DisplayOrder))
        {
            var sstRow = Run.Sst!.Analytes.First(a => a.HplcMethodAnalyteId == analyte.Id);
            var reps = RunSample.Replicates.OrderBy(r => r.ReplicateNo)
                .Select(r => new CleaningResidueReplicateInput(r.ReplicateNo,
                    r.Responses.First(x => x.HplcMethodAnalyteId == analyte.Id).Response))
                .ToList();
            inputs.Add(new HplcCleaningResidueInput(
                analyte.Id,
                analyte.Name,
                analyte.StandardConcentrationUgPerMl!.Value,
                Snapshot.SampleSolutionVolumeMl!.Value,
                Snapshot.SwabAreaCm2,
                analyte.MacoLimit!.Value,
                analyte.MacoUnit!.Value,
                sstRow.MeanResponse!.Value,
                reps,
                Specs.First(s => s.HplcMethodAnalyteId == analyte.Id)));
        }
        return inputs;
    }

    // Related substances: the principal analyte's "response" is the reference solution's principal-peak area;
    // the sample weight is not used. Build/Calculate are only called when EntryProblem is null.
    private string? RelatedSubstancesEntryProblem()
    {
        var analyte = Snapshot.Analytes.First();
        if (Snapshot.ReferenceSolutionPercent is not > 0m)
            return "The method has no reference solution concentration.";
        // A null disregard limit means "no disregard limit" (some monographs give none).
        foreach (var rep in RunSample.Replicates.OrderBy(r => r.ReplicateNo))
            if (!rep.Responses.Any(x => x.HplcMethodAnalyteId == analyte.Id && x.Response > 0))
                return $"{analyte.Name}: the reference solution area is required.";

        var rows = Specs.Where(s => s.RelatedSubstanceRow.HasValue && s.HplcMethodAnalyteId == analyte.Id).ToList();
        if (rows.Count == 0)
            return "No related-substances specification on this item.";
        if (rows.Any(s => s.RelatedSubstanceRow == RelatedSubstanceRow.Named && !Snapshot.Impurities.Any(i => i.Id == s.HplcMethodImpurityId)))
            return "A specification names an impurity that is not in this run's method.";
        return null;
    }

    public RelatedSubstancesInput BuildRelatedSubstancesInput()
    {
        var analyte = Snapshot.Analytes.First();
        var reps = RunSample.Replicates.OrderBy(r => r.ReplicateNo)
            .Select(r => new RsReplicate(r.ReplicateNo, r.Responses.First(x => x.HplcMethodAnalyteId == analyte.Id).Response,
                r.Peaks.OrderBy(p => p.Id).Select(p => new HplcReplicatePeakInput(p.HplcMethodImpurityId, p.RelativeRetention, p.Area)).ToList()))
            .ToList();
        return new RelatedSubstancesInput(Snapshot.ReferenceSolutionPercent!.Value, Snapshot.DisregardLimitPercent,
            Snapshot.Impurities, reps, Specs.Where(s => s.HplcMethodAnalyteId == analyte.Id).ToList());
    }

    public List<RsRowResult> CalculateRelatedSubstances()
    {
        var analyte = Snapshot.Analytes.First();
        return RelatedSubstancesEvaluator.Calculate(analyte.Id, analyte.Name, BuildRelatedSubstancesInput());
    }

    public List<HplcResidualSolventInput> BuildResidualSolventInputs()
    {
        var inputs = new List<HplcResidualSolventInput>();
        foreach (var analyte in Snapshot.Analytes.OrderBy(a => a.DisplayOrder))
        {
            var sstRow = Run.Sst!.Analytes.First(a => a.HplcMethodAnalyteId == analyte.Id);
            var reps = RunSample.Replicates.OrderBy(r => r.ReplicateNo)
                .Select(r => new ResidualSolventReplicateInput(r.ReplicateNo, r.ActualWeightMg,
                    r.Responses.First(x => x.HplcMethodAnalyteId == analyte.Id).Response))
                .ToList();
            inputs.Add(new HplcResidualSolventInput(analyte.Id, analyte.Name, analyte.StandardConcentrationUgPerMl!.Value,
                Snapshot.SampleSolutionVolumeMl!.Value, sstRow.MeanResponse!.Value, reps,
                Specs.First(s => s.HplcMethodAnalyteId == analyte.Id && s.ResultBasis == ResultBasis.Ppm)));
        }
        return inputs;
    }

    public List<HplcAssayAnalyteInput> BuildAnalyteInputs(decimal? moisturePercentOverride = null)
    {
        var hasIs = !string.IsNullOrWhiteSpace(Snapshot.InternalStandardName);
        var inputs = new List<HplcAssayAnalyteInput>();
        foreach (var analyte in Snapshot.Analytes.OrderBy(a => a.DisplayOrder))
        {
            var sstRow = Run.Sst!.Analytes.First(a => a.HplcMethodAnalyteId == analyte.Id);
            var reps = RunSample.Replicates.OrderBy(r => r.ReplicateNo)
                .Select(r => new AssayReplicateInput(r.ReplicateNo, r.ActualWeightMg,
                    HplcResponse.ForReplicate(r.ReplicateNo, r.Responses.First(x => x.HplcMethodAnalyteId == analyte.Id).Response, r.InternalStandardResponse, hasIs)))
                .ToList();
            var std = new AssayStandard(sstRow.MeanResponse!.Value, sstRow.StandardWeightMg!.Value,
                sstRow.StandardPurityPercent!.Value, sstRow.StandardMoisturePercent!.Value);
            var assaySpec = Specs.First(s => s.HplcMethodAnalyteId == analyte.Id &&
                (s.ResultBasis is ResultBasis.PercentLabelClaim or ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis));
            var amountSpec = Specs.FirstOrDefault(s => s.HplcMethodAnalyteId == analyte.Id && s.ResultBasis == ResultBasis.MgPerUnit);
            decimal? moisturePercent = (assaySpec.ResultBasis is ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis)
                ? (moisturePercentOverride ?? Moisture?.Percent)
                : null;
            inputs.Add(new HplcAssayAnalyteInput(
                analyte.Id, analyte.Name, analyte.TheoreticalWeightStdMg, analyte.TheoreticalWeightTestMg, std, reps,
                assaySpec, amountSpec, moisturePercent));
        }
        return inputs;
    }

    public static async Task<HplcSampleEntryContext> LoadAsync(IMicroLimsDbContext db, int runSampleId, CancellationToken ct = default)
    {
        var runSample = await db.HplcRunSamples
            .Include(s => s.Replicates).ThenInclude(r => r.Responses)
            .Include(s => s.Replicates).ThenInclude(r => r.Peaks)
            .FirstOrDefaultAsync(s => s.Id == runSampleId, ct)
            ?? throw new NotFoundException($"Run sample {runSampleId} not found.");

        if (runSample.TestOrderId is not int testOrderId)
            throw new InvalidOperationException("This run sample is a working standard qualification - open it from its qualification entry.");

        var run = await db.HplcRuns
            .Include(r => r.Sst).ThenInclude(s => s!.Analytes)
            .Include(r => r.Evidence)
            .FirstAsync(r => r.Id == runSample.HplcRunId, ct);

        var order = await db.TestOrders
            .Include(o => o.Sample).ThenInclude(s => s!.Item)
            .Include(o => o.Sample).ThenInclude(s => s!.ProductionStageRef)
            .FirstAsync(o => o.Id == testOrderId, ct);

        var definition = await db.TestDefinitions.FirstAsync(t => t.Code == order.TestCode, ct);
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The run's method snapshot could not be read.");

        List<Specification> specs;
        if (order.Sample?.ItemId is int itemId)
        {
            specs = (await SpecificationLookup.ForSampleAsync(db, order.SampleId, itemId, order.TestCode, ct)).Where(s => s.HplcMethodAnalyteId != null).ToList();
        }
        else if (order.Sample?.Category == SampleCategory.AfterCleaning)
        {
            specs = snapshot.Analytes.Select(analyte => new Specification
            {
                Id = 0,
                HplcMethodAnalyteId = analyte.Id,
                LimitType = LimitType.NotMoreThan,
                UpperLimit = analyte.MacoLimit,
                Unit = analyte.MacoUnit.HasValue ? CleaningResidueCalculator.UnitText(analyte.MacoUnit.Value) : string.Empty,
                ParameterName = analyte.Name,
            }).ToList();
        }
        else
        {
            specs = new List<Specification>();
        }

        var submitted = await db.TestAnalyses.AnyAsync(a => a.TestOrderId == order.Id && a.IsActive, ct);
        var returned = !submitted && await db.TestReturnEvents.AnyAsync(e => e.TestOrderId == order.Id, ct);
        var replicates = await StageReplicateResolver.ResolveForTestOrderAsync(db, order.Id, ct);
        var hasReport = run.Evidence.Any(e => e.HplcRunSampleId == runSample.Id
            && e.Kind == HplcEvidenceKind.SampleReport && e.SupersededByEvidenceId == null);

        var moistureRole = MoistureRole.None;
        SampleMoisture? moisture = null;
        string? moistureError = null;
        if (snapshot.ResultMode == HplcResultMode.Assay)
        {
            try { moistureRole = SampleMoistureResolver.NeededRole(specs.Select(s => s.ResultBasis), "HPLC test"); }
            catch (InvalidOperationException ex)
            {
                // dried and anhydrous on one test: an entry problem, never a throw. The role is only a marker that EntryProblem reports MoistureError.
                moistureRole = MoistureRole.LossOnDrying;
                moistureError = ex.Message;
            }
        }
        if (moistureRole != MoistureRole.None && moistureError == null)
        {
            try
            {
                moisture = await new SampleMoistureResolver(db).ResolveAsync(order.SampleId, moistureRole);
            }
            catch (InvalidOperationException ex)
            {
                moistureError = ex.Message;
            }
        }

        return new HplcSampleEntryContext
        {
            RunSample = runSample, Run = run, Order = order, Sample = order.Sample!, Definition = definition,
            Snapshot = snapshot, Replicates = replicates, Specs = specs,
            Submitted = submitted, Returned = returned, HasCurrentSampleReport = hasReport,
            MoistureRole = moistureRole, Moisture = moisture, MoistureError = moistureError,
        };
    }
}
