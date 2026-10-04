using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public record IcpConcentrationInput(int IcpMethodElementId, decimal SolutionMgPerL);
public record IcpReplicateInputDto(decimal SampleAmount, decimal VolumeMl, decimal DilutionFactor, List<IcpConcentrationInput> Concentrations);
public record SaveIcpReplicatesRequest(IcpAmountUnit AmountUnit, decimal? UnitAmount, List<IcpReplicateInputDto> Replicates);
public record AssignIcpSamplesRequest(List<int> TestOrderIds);

public record IcpReplicateDto(int ReplicateNo, decimal SampleAmount, decimal VolumeMl, decimal DilutionFactor, List<IcpConcentrationInput> Concentrations);
public record IcpPreviewResultDto(
    int IcpMethodElementId, string ParameterName, string Quantity, decimal? Value, string Display, string Unit,
    ResultStatus? Status, string? SpecLimit, string? Problem);
public record IcpOfficialResultDto(string ParameterName, string Quantity, string Display, ResultStatus Status, string? SpecLimit);

public record IcpSampleEntryDto(
    int RunSampleId, int IcpRunId, string RunCode, string CalibrationCode, IcpCalibrationStatus CalibrationStatus,
    int TestOrderId, string SampleNumber, string? BatchNumber, string? ProductName, string TestCode, string? StageName,
    IcpRunSampleStatus Status, IcpMethodMode Mode, IcpAmountUnit AmountUnit, decimal? UnitAmount,
    decimal SampleVolumeMl, decimal DilutionFactor,
    List<IcpElementStateDto> Elements, List<IcpReplicateDto> Replicates,
    List<IcpPreviewResultDto> Preview, List<IcpOfficialResultDto> Official, List<IcpEvidenceDto> Evidence,
    bool Editable, string? EditableReason, bool Submitted, bool CanSubmit, string? CanSubmitReason);

// ICP workspace (I2): sample assignment after the calibration is confirmed, replicate entry with a live
// (preview) calculation. Submission itself is the ICP recorder.
public partial class IcpRunService
{
    private static readonly WorkflowStep[] FinalizedSteps = { WorkflowStep.Ready, WorkflowStep.Reviewed, WorkflowStep.Approved };

    // ---- Eligible tests ----

    public async Task<List<EligibleTestDto>> GetEligibleTestsAsync(int runId, string? search, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        var query = EligibleOrdersQuery(run);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o => o.Sample!.ReferenceNumber.ToLower().Contains(term)
                || (o.Sample.BatchNumber != null && o.Sample.BatchNumber.ToLower().Contains(term))
                || (o.Sample.Item != null && o.Sample.Item.Name.ToLower().Contains(term))
                || o.TestCode.ToLower().Contains(term));
        }

        var orders = await query.OrderBy(o => o.Sample!.ReceivedAt).ThenBy(o => o.Id).ToListAsync(ct);
        return orders.Select(o => new EligibleTestDto(
            o.Id, o.Sample!.ReferenceNumber, o.Sample.BatchNumber, o.Sample.Item?.Name, o.TestCode,
            o.Sample.ReceivedAt, o.Sample.ProductionStageRef?.Name ?? o.Sample.ProductionStage)).ToList();
    }

    // The method's ICP assay tests in the run's laboratory, not finalized, not cancelled/voided, and not
    // already assigned to another run that has not been abandoned.
    private IQueryable<TestOrder> EligibleOrdersQuery(IcpRun run)
    {
        var codes = _db.TestDefinitions
            .Where(t => t.WorkflowType == WorkflowType.IcpMethodAssay && t.IcpMethodId == run.IcpMethodId)
            .Select(t => t.Code);
        var busy = _db.IcpRunSamples
            .Where(s => s.Status == IcpRunSampleStatus.Assigned && s.IcpRun!.Status != IcpRunStatus.Abandoned)
            .Select(s => s.TestOrderId);

        return _db.TestOrders
            .Include(o => o.Sample).ThenInclude(s => s!.Item)
            .Include(o => o.Sample).ThenInclude(s => s!.ProductionStageRef)
            .Where(o => codes.Contains(o.TestCode) && o.SectionId == run.SectionId
                && !o.IsSuperseded && !FinalizedSteps.Contains(o.CurrentStep)
                && o.Sample != null && o.Sample.Status != SampleStatus.Voided && o.Sample.Status != SampleStatus.Cancelled
                && !busy.Contains(o.Id)
                && !_db.TestAnalyses.Any(a => a.TestOrderId == o.Id && a.IsActive));
    }

    // ---- Assign / remove ----

    public async Task<IcpRunDto> AssignSamplesAsync(int runId, List<int> testOrderIds, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != IcpRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");
        if (run.Calibration?.Status != IcpCalibrationStatus.Confirmed)
            throw new InvalidOperationException("Confirm the calibration before assigning samples.");
        if (!IcpElementAvailability.Evaluate(ReadSnapshot(run), run.Calibration, run.CcvReadings, _clock.UtcNow.UtcDateTime, _clock).Any(s => s.Valid))
            throw new InvalidOperationException("No element is valid on this run.");

        var ids = (testOrderIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one test.");

        var eligible = await EligibleOrdersQuery(run).Where(o => ids.Contains(o.Id)).Select(o => o.Id).ToListAsync(ct);
        foreach (var id in ids.Where(id => !eligible.Contains(id)))
        {
            var busyRun = await _db.IcpRunSamples
                .Where(s => s.TestOrderId == id && s.Status == IcpRunSampleStatus.Assigned && s.IcpRun!.Status != IcpRunStatus.Abandoned)
                .Select(s => s.IcpRun!.Code).FirstOrDefaultAsync(ct);
            throw new InvalidOperationException(busyRun != null
                ? $"Test order {id} is already assigned to run {busyRun}."
                : $"Test order {id} is not eligible for this run.");
        }

        var nowUtc = _clock.UtcNow.UtcDateTime;
        foreach (var id in ids)
            run.Samples.Add(new IcpRunSample { TestOrderId = id, Status = IcpRunSampleStatus.Assigned, AssignedAt = nowUtc, AssignedByUserId = userId });

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    public async Task<IcpRunDto> RemoveSampleAsync(int runId, int runSampleId, string reason, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != IcpRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");

        var sample = run.Samples.FirstOrDefault(s => s.Id == runSampleId)
            ?? throw new NotFoundException($"Sample {runSampleId} not found on this run.");
        if (sample.Status != IcpRunSampleStatus.Assigned)
            throw new InvalidOperationException("This sample was already removed from the run.");
        if (await _db.TestAnalyses.AnyAsync(a => a.TestOrderId == sample.TestOrderId && a.IsActive, ct))
            throw new InvalidOperationException("A result has already been sent for review for this sample.");

        var trimmedReason = ValidateReason(reason);
        sample.Status = IcpRunSampleStatus.Removed;
        sample.RemovedReason = trimmedReason;
        sample.RemovedAt = _clock.UtcNow.UtcDateTime;
        sample.RemovedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    // ---- Entry ----

    public async Task<IcpSampleEntryDto> GetSampleEntryAsync(int runSampleId, int userId, CancellationToken ct = default)
    {
        var context = await IcpSampleEntryContext.LoadAsync(_db, runSampleId, ct, _clock);
        await EnsureAccessAsync(context.Run, userId, ct);
        return await BuildEntryDtoAsync(context, ct);
    }

    public async Task<IcpSampleEntryDto> SaveReplicatesAsync(int runSampleId, SaveIcpReplicatesRequest r, int userId, CancellationToken ct = default)
    {
        var context = await IcpSampleEntryContext.LoadAsync(_db, runSampleId, ct, _clock);
        await EnsureAccessAsync(context.Run, userId, ct);

        var problem = context.EditableProblem() ?? context.CalibrationProblem();
        if (problem != null)
            throw new InvalidOperationException(problem);

        var needed = context.NeededElements;
        if (needed.Count == 0)
            throw new InvalidOperationException("No specification is configured for this test on this item.");
        if (context.Snapshot.Mode == IcpMethodMode.ElementalImpurities && r.AmountUnit != IcpAmountUnit.Gram)
            throw new InvalidOperationException("Elemental impurities are reported per gram; enter the sample amount in g.");
        if (r.UnitAmount is <= 0m || (context.NeedsUnitAmount && r.UnitAmount is null))
            throw new InvalidOperationException("Enter the unit amount (average unit weight or dose).");

        var inputs = r.Replicates ?? new List<IcpReplicateInputDto>();
        var neededIds = needed.Select(e => e.Id).ToList();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input.SampleAmount <= 0 || input.VolumeMl <= 0)
                throw new InvalidOperationException($"Replicate {i + 1}: the sample amount and volume must be greater than zero.");
            if (input.DilutionFactor < 1)
                throw new InvalidOperationException($"Replicate {i + 1}: the dilution factor must be at least 1.");

            var concentrations = input.Concentrations ?? new List<IcpConcentrationInput>();
            if (concentrations.Any(c => !neededIds.Contains(c.IcpMethodElementId)))
                throw new InvalidOperationException($"Replicate {i + 1}: a concentration belongs to an element that is not needed by this test.");
            foreach (var el in needed.Where(e => concentrations.Count(c => c.IcpMethodElementId == e.Id) != 1))
                throw new InvalidOperationException($"Replicate {i + 1}: exactly one concentration for {el.Symbol} is required.");
        }

        await ReplaceReplicatesAsync(context.RunSample, r, inputs, ct);
        return await GetSampleEntryAsync(runSampleId, userId, ct);
    }

    // Replace-all: the old rows are removed first so the (sample, replicate no) unique index never sees both generations.
    private async Task ReplaceReplicatesAsync(IcpRunSample runSample, SaveIcpReplicatesRequest r, List<IcpReplicateInputDto> inputs, CancellationToken ct)
    {
        await UnitOfWork.RunAsync(_db, async () =>
        {
            _db.IcpSampleReplicates.RemoveRange(runSample.Replicates);
            runSample.Replicates.Clear();
            runSample.AmountUnit = r.AmountUnit;
            runSample.UnitAmount = r.UnitAmount;
            await _db.SaveChangesAsync(ct);

            for (var i = 0; i < inputs.Count; i++)
            {
                runSample.Replicates.Add(new IcpSampleReplicate
                {
                    ReplicateNo = i + 1, SampleAmount = inputs[i].SampleAmount,
                    VolumeMl = inputs[i].VolumeMl, DilutionFactor = inputs[i].DilutionFactor,
                    Concentrations = inputs[i].Concentrations
                        .Select(x => new IcpReplicateConcentration { IcpMethodElementId = x.IcpMethodElementId, SolutionMgPerL = x.SolutionMgPerL }).ToList(),
                });
            }
            await _db.SaveChangesAsync(ct);
        });
    }

    // ---- Run completion ----

    public async Task<IcpRunDto> CompleteRunAsync(int runId, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != IcpRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");

        var orderIds = run.Samples.Where(s => s.Status == IcpRunSampleStatus.Assigned).Select(s => s.TestOrderId).ToList();
        var submitted = await _db.TestAnalyses
            .Where(a => orderIds.Contains(a.TestOrderId) && a.IsActive)
            .Select(a => a.TestOrderId).Distinct().ToListAsync(ct);
        var pending = orderIds.Count(id => !submitted.Contains(id));
        if (pending > 0)
            throw new InvalidOperationException($"{pending} assigned sample(s) have not been sent for review. Submit or remove them first.");

        run.Status = IcpRunStatus.Completed;
        run.ClosedAt = _clock.UtcNow.UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    // ---- Mapping ----

    private async Task<IcpSampleEntryDto> BuildEntryDtoAsync(IcpSampleEntryContext c, CancellationToken ct)
    {
        var problem = c.SubmitProblem();

        // The preview is what submission would store; shown once every value it needs is entered.
        var preview = new List<IcpPreviewResultDto>();
        if (c.EntryProblem() == null)
        {
            preview = c.EvaluateRows().Select(row => new IcpPreviewResultDto(
                row.ElementId, row.Spec.ParameterName is { Length: > 0 } name ? name : row.Symbol, QuantityOf(row.Basis),
                row.Value, row.Display, row.Unit, row.Status,
                !string.IsNullOrWhiteSpace(row.Spec.SpecLimit) ? row.Spec.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(row.Spec),
                row.Problem)).ToList();
        }

        var official = c.Submitted ? await LoadOfficialResultsAsync(c.Order.Id, ct) : new List<IcpOfficialResultDto>();

        var evidenceRows = c.Run.Evidence.Where(e => e.IcpRunSampleId == c.RunSample.Id).OrderByDescending(e => e.UploadedAt).ToList();
        var uploaderIds = evidenceRows.Select(e => e.UploadedByUserId).Distinct().ToList();
        var names = await _db.Users.Where(u => uploaderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var evidence = evidenceRows.Select(e => ToEvidenceDto(e, names.GetValueOrDefault(e.UploadedByUserId))).ToList();

        var replicates = c.RunSample.Replicates.OrderBy(r => r.ReplicateNo)
            .Select(r => new IcpReplicateDto(r.ReplicateNo, r.SampleAmount, r.VolumeMl, r.DilutionFactor,
                r.Concentrations.OrderBy(x => x.IcpMethodElementId).Select(x => new IcpConcentrationInput(x.IcpMethodElementId, x.SolutionMgPerL)).ToList()))
            .ToList();

        var editableReason = c.EditableProblem();
        return new IcpSampleEntryDto(
            c.RunSample.Id, c.Run.Id, c.Run.Code, c.Run.Calibration!.Code, c.Run.Calibration.Status,
            c.Order.Id, c.Sample.ReferenceNumber, c.Sample.BatchNumber, c.Sample.Item?.Name, c.Order.TestCode, c.StageName,
            c.RunSample.Status, c.Snapshot.Mode, c.RunSample.AmountUnit, c.RunSample.UnitAmount,
            c.Snapshot.SampleVolumeMl, c.Snapshot.DilutionFactor,
            c.NeededStates(), replicates, preview, official, evidence,
            editableReason == null, editableReason, c.Submitted, problem == null, problem);
    }

    private static string QuantityOf(ResultBasis basis) => basis switch
    {
        ResultBasis.MgPerUnit => "Amount per unit",
        ResultBasis.PercentLabelClaim => "Label claim %",
        _ => "Content (µg/g)",
    };

    // What the ICP recorder stored; Quantity comes from its calculation payload, tolerating rows without one.
    private async Task<List<IcpOfficialResultDto>> LoadOfficialResultsAsync(int testOrderId, CancellationToken ct)
    {
        var rows = await _db.ParameterResults.AsNoTracking()
            .Where(p => p.TestOrderId == testOrderId && p.IsActive)
            .OrderBy(p => p.Id).ToListAsync(ct);
        return rows.Select(p =>
        {
            var quantity = "Content (µg/g)";
            if (!string.IsNullOrWhiteSpace(p.CalculationJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(p.CalculationJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(q.GetString()))
                        quantity = q.GetString()!;
                }
                catch (JsonException) { }
            }
            return new IcpOfficialResultDto(p.ParameterName, quantity, p.ReportedDisplay, p.ComparisonStatus, p.SpecLimit);
        }).ToList();
    }
}
