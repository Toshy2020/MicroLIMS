using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Everything needed to show, preview and submit one sample's ICP entry. Loaded once and shared by
// IcpRunService (entry screen) and the ICP recorder (submission) so the submission gate has a single definition.
public sealed class IcpSampleEntryContext
{
    public required IcpRunSample RunSample { get; init; }
    public required IcpRun Run { get; init; }
    public required TestOrder Order { get; init; }
    public required Sample Sample { get; init; }
    public required TestDefinition Definition { get; init; }
    public required IcpMethodResponse Snapshot { get; init; }
    public required List<Specification> Specs { get; init; }
    public required bool Submitted { get; init; }
    public required bool Returned { get; init; }
    public required bool HasCurrentSampleReport { get; init; }
    public required ILabClock Clock { get; init; }

    public string? StageName => Sample.ProductionStageRef?.Name ?? Sample.ProductionStage;
    public bool Editable => EditableProblem() == null;

    // Elements the test's specs refer to, in method order.
    public List<IcpMethodElementResponse> NeededElements =>
        Snapshot.Elements.Where(e => Specs.Any(s => s.IcpMethodElementId == e.Id)).OrderBy(e => e.DisplayOrder).ToList();

    public bool NeedsUnitAmount => Specs.Any(s => s.ResultBasis is ResultBasis.MgPerUnit or ResultBasis.PercentLabelClaim);

    public List<IcpElementStateDto> ElementStates() =>
        IcpElementAvailability.Evaluate(Snapshot, Run.Calibration!, Run.CcvReadings, Clock.UtcNow.UtcDateTime, Clock);

    // States of the needed elements only.
    public List<IcpElementStateDto> NeededStates()
    {
        var needed = NeededElements.Select(e => e.Id).ToHashSet();
        return ElementStates().Where(s => needed.Contains(s.IcpMethodElementId)).ToList();
    }

    // Editable while the run is open, or - after a submitted result was returned to the analyst - until submitted again.
    public string? EditableProblem()
    {
        if (RunSample.Status != IcpRunSampleStatus.Assigned) return "This sample was removed from the run.";
        if (Submitted) return "The result has already been sent for review.";
        if (Run.Status == IcpRunStatus.Open) return null;
        if (Run.Status == IcpRunStatus.Completed && Returned) return null;
        return "The run is closed.";
    }

    // Null while the calibration is confirmed and inside its validity window.
    public string? CalibrationProblem()
    {
        var expiresAt = IcpElementAvailability.ExpiresAt(Run.Calibration!, Snapshot.MaxCalibrationAgeHours);
        if (expiresAt is null)
            return "Confirm the calibration before entering results.";
        if (expiresAt < Clock.UtcNow.UtcDateTime)
            return $"Calibration expired at {Clock.ToLabLocal(expiresAt.Value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.";
        return null;
    }

    // First reason the sample cannot be sent for review, or null.
    public string? SubmitProblem() =>
        EntryProblem()
        ?? BlockedElementProblem()
        ?? EvaluateRows().FirstOrDefault(r => r.Problem != null)?.Problem
        ?? (HasCurrentSampleReport ? null : "Upload the sample result report before sending for review.");

    // Everything the submission needs except the sample report and element validity: when this is null the
    // rows can be calculated (and previewed; a blocked element shows as a row problem).
    public string? EntryProblem()
    {
        var editable = EditableProblem();
        if (editable != null) return editable;
        var cal = CalibrationProblem();
        if (cal != null) return cal;

        foreach (var spec in Specs.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
        {
            if (spec.IcpMethodElementId is not int specElementId)
                return $"Specification \"{spec.ParameterName}\" is not linked to an ICP method element.";
            if (Snapshot.Elements.All(e => e.Id != specElementId))
                return $"Specification \"{spec.ParameterName}\" refers to an element that is not on this run's method.";
        }

        var needed = NeededElements;
        if (needed.Count == 0) return "No specification is configured for this test on this item.";
        if (RunSample.Replicates.Count == 0) return "Enter the replicates.";

        foreach (var rep in RunSample.Replicates.OrderBy(r => r.ReplicateNo))
        {
            if (rep.SampleAmount <= 0 || rep.VolumeMl <= 0 || rep.DilutionFactor < 1)
                return $"Replicate {rep.ReplicateNo}: the sample amount, volume and dilution factor are required.";
            foreach (var el in needed)
                if (!rep.Concentrations.Any(c => c.IcpMethodElementId == el.Id))
                    return $"Replicate {rep.ReplicateNo}: a concentration for {el.Symbol} is required.";
        }
        if (NeedsUnitAmount && RunSample.UnitAmount is not > 0m)
            return "Enter the unit amount (average unit weight or dose).";
        return null;
    }

    private string? BlockedElementProblem()
    {
        var blocked = NeededStates().FirstOrDefault(s => !s.Valid);
        return blocked == null ? null : $"{blocked.Symbol} is not valid on this run: {blocked.Reason}";
    }

    public List<IcpSampleRow> EvaluateRows() =>
        IcpSampleEvaluator.Evaluate(Snapshot, ElementStates(), Specs, RunSample.Replicates, RunSample.UnitAmount);

    public static async Task<IcpSampleEntryContext> LoadAsync(
        IMicroLimsDbContext db, int runSampleId, CancellationToken ct = default, ILabClock? clock = null)
    {
        var runSample = await db.IcpRunSamples
            .Include(s => s.Replicates).ThenInclude(r => r.Concentrations)
            .FirstOrDefaultAsync(s => s.Id == runSampleId, ct)
            ?? throw new NotFoundException($"Run sample {runSampleId} not found.");

        var run = await db.IcpRuns
            .Include(r => r.Calibration).ThenInclude(c => c!.Elements)
            .Include(r => r.CcvReadings)
            .Include(r => r.Evidence)
            .FirstAsync(r => r.Id == runSample.IcpRunId, ct);

        var order = await db.TestOrders
            .Include(o => o.Sample).ThenInclude(s => s!.Item)
            .Include(o => o.Sample).ThenInclude(s => s!.ProductionStageRef)
            .FirstAsync(o => o.Id == runSample.TestOrderId, ct);

        var definition = await db.TestDefinitions.FirstAsync(t => t.Code == order.TestCode, ct);
        var snapshot = JsonSerializer.Deserialize<IcpMethodResponse>(run.MethodSnapshotJson, SnapshotJson.Options)
            ?? throw new InvalidOperationException("The run's method snapshot could not be read.");

        var specs = order.Sample?.ItemId is int itemId
            ? (await SpecificationLookup.ForSampleAsync(db, order.SampleId, itemId, order.TestCode, ct)).ToList()
            : new List<Specification>();

        var submitted = await db.TestAnalyses.AnyAsync(a => a.TestOrderId == order.Id && a.IsActive, ct);
        var returned = !submitted && await db.TestReturnEvents.AnyAsync(e => e.TestOrderId == order.Id, ct);
        var hasReport = run.Evidence.Any(e => e.IcpRunSampleId == runSample.Id
            && e.Kind == IcpEvidenceKind.SampleReport && e.SupersededByEvidenceId == null);

        return new IcpSampleEntryContext
        {
            RunSample = runSample, Run = run, Order = order, Sample = order.Sample!, Definition = definition,
            Snapshot = snapshot, Specs = specs, Submitted = submitted, Returned = returned,
            HasCurrentSampleReport = hasReport, Clock = clock ?? LabClock.Default,
        };
    }
}
