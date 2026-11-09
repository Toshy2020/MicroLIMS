using MicroLIMS.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public record IcpConcentrationInput(int IcpTestElementId, decimal ResultPpm);
public record SaveIcpSampleEntryRequest(decimal? ActualWeightG, List<IcpConcentrationInput> Concentrations);
public record AssignIcpSamplesRequest(List<int> TestOrderIds, string Password, string? Comment);

public record IcpPreviewResultDto(
    int IcpTestElementId, string ParameterName, string Quantity, decimal? Value, string Display, string Unit,
    ResultStatus? Status, string? SpecLimit, string? Problem);
public record IcpOfficialResultDto(string ParameterName, string Quantity, string Display, ResultStatus Status, string? SpecLimit);

// One element of the test with its target, whether the run's calibration allows it, and the entered result.
public record IcpEntryElementDto(int IcpTestElementId, string Symbol, decimal? TargetPpm, bool Valid, string? Reason, decimal? ResultPpm);

public record IcpSampleEntryDto(
    int RunSampleId, int IcpRunId, string RunCode, string CalibrationCode, IcpCalibrationStatus CalibrationStatus,
    int TestOrderId, string SampleNumber, string? BatchNumber, string? ProductName, string TestCode, string? StageName,
    IcpRunSampleStatus Status, IcpTestMode Mode, IcpSampleForm? SampleForm, decimal? TheoreticalAmount, decimal? SpecificGravity,
    decimal? ActualWeightG, List<IcpEntryElementDto> Elements,
    List<IcpPreviewResultDto> Preview, List<IcpOfficialResultDto> Official, List<IcpEvidenceDto> Evidence,
    bool Editable, string? EditableReason, bool Submitted, bool CanSubmit, string? CanSubmitReason,
    string? SubjectName = null);

// ICP workspace: sample assignment after the calibration is confirmed, result entry with a live
// (preview) calculation. Submission itself is the ICP recorder.
public partial class IcpRunService
{
    private static readonly WorkflowStep[] FinalizedSteps = { WorkflowStep.Ready, WorkflowStep.Reviewed, WorkflowStep.Approved };

    // ---- Eligible tests ----

    public async Task<List<EligibleTestDto>> GetEligibleTestsAsync(int runId, string? search, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct, SectionAccess.Read);

        var query = EligibleOrdersQuery(run.SectionId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o => o.Sample!.ReferenceNumber.ToLower().Contains(term)
                || (o.Sample.BatchNumber != null && o.Sample.BatchNumber.ToLower().Contains(term))
                || (o.Sample.Item != null && o.Sample.Item.Name.ToLower().Contains(term))
                || o.TestCode.ToLower().Contains(term));
        }

        var orders = await query.OrderBy(o => o.Sample!.ReceivedAt).ThenBy(o => o.Id).ToListAsync(ct);
        var orderIds = orders.Select(o => o.Id).ToList();
        var locations = orderIds.Count == 0 ? new List<SampleLocation>() : await _db.SampleLocations.AsNoTracking()
            .Include(l => l.Sample).ThenInclude(s => s!.Machine)
            .Include(l => l.MachinePartConfiguration).ThenInclude(c => c!.MachinePart).ThenInclude(p => p!.Machine)
            .Where(l => orderIds.Contains(l.TestOrderId))
            .ToListAsync(ct);
        var locationsByOrder = locations.GroupBy(l => l.TestOrderId).ToDictionary(g => g.Key, g => g.ToList());

        return orders.Select(o =>
        {
            locationsByOrder.TryGetValue(o.Id, out var locs);
            return new EligibleTestDto(
                o.Id, o.Sample!.ReferenceNumber, o.Sample.BatchNumber, o.Sample.Item?.Name, o.TestCode,
                o.Sample.ReceivedAt, o.Sample.ProductionStageRef?.Name ?? o.Sample.ProductionStage,
                SubjectNameHelper.FormatSingleLocationSubject(locs));
        }).ToList();
    }

    // ICP assay tests in the run's laboratory, not finalized, not cancelled/voided, and not
    // already assigned to another run that has not been abandoned.
    private IQueryable<TestOrder> EligibleOrdersQuery(int sectionId)
    {
        var codes = _db.TestDefinitions
            .Where(t => t.WorkflowType == WorkflowType.IcpMethodAssay)
            .Select(t => t.Code);
        var busy = _db.IcpRunSamples
            .Where(s => s.Status == IcpRunSampleStatus.Assigned && s.IcpRun!.Status != IcpRunStatus.Abandoned)
            .Select(s => s.TestOrderId);

        return _db.TestOrders
            .Include(o => o.Sample).ThenInclude(s => s!.Item)
            .Include(o => o.Sample).ThenInclude(s => s!.ProductionStageRef)
            .Where(o => codes.Contains(o.TestCode) && o.SectionId == sectionId
                && !o.IsSuperseded && !FinalizedSteps.Contains(o.CurrentStep)
                && o.Sample != null && o.Sample.Status != SampleStatus.Voided && o.Sample.Status != SampleStatus.Cancelled
                && !busy.Contains(o.Id)
                && !_db.TestAnalyses.Any(a => a.TestOrderId == o.Id && a.IsActive));
    }

    // ---- Assign / remove ----

    // First reason samples cannot be assigned now, or null. Shared by AssignSamplesAsync and the run DTO.
    private string? AssignBlockReason(IcpRun run)
    {
        if (run.Status != IcpRunStatus.Open) return "The run is closed.";
        if (run.Calibration?.Status != IcpCalibrationStatus.Confirmed) return "Confirm the calibration before assigning samples.";
        if (IcpElementAvailability.ExpiresAt(run.Calibration) is { } expiresAt && expiresAt < _clock.UtcNow.UtcDateTime)
            return "The calibration has expired. Start a new run with a new calibration.";
        if (!run.Calibration.Elements.Any(e => e.Passed)) return "No calibrated element passes on this run.";
        return null;
    }

    public async Task<IcpRunDto> AssignSamplesAsync(int runId, AssignIcpSamplesRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        var blocked = AssignBlockReason(run);
        if (blocked != null) throw new BusinessRuleException(blocked);

        var ids = (r.TestOrderIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new BusinessRuleException("Select at least one test.");

        var eligible = await EligibleOrdersQuery(run.SectionId).Where(o => ids.Contains(o.Id)).Select(o => o.Id).ToListAsync(ct);
        foreach (var id in ids.Where(id => !eligible.Contains(id)))
        {
            var busyRun = await _db.IcpRunSamples
                .Where(s => s.TestOrderId == id && s.Status == IcpRunSampleStatus.Assigned && s.IcpRun!.Status != IcpRunStatus.Abandoned)
                .Select(s => s.IcpRun!.Code).FirstOrDefaultAsync(ct);
            throw new BusinessRuleException(busyRun != null
                ? $"Test order {id} is already assigned to run {busyRun}."
                : $"Test order {id} is not eligible for this run.");
        }

        // Signs first - a wrong password must leave nothing assigned. One signature covers the whole action.
        var signature = await _signatures.SignAsync(
            userId, r.Password, SignatureMeaning.SampleAssignedToRun, nameof(IcpRun), run.Id, r.Comment, ip);

        var nowUtc = _clock.UtcNow.UtcDateTime;
        foreach (var id in ids)
            run.Samples.Add(new IcpRunSample
            {
                TestOrderId = id, Status = IcpRunSampleStatus.Assigned, AssignedAt = nowUtc,
                AssignedByUserId = userId, AssignmentSignature = signature
            });

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    public async Task<IcpRunDto> RemoveSampleAsync(int runId, int runSampleId, string reason, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        // A returned sample on a completed run must be removable, or its order stays busy forever.
        // The active-analysis check below keeps submitted samples in place.
        if (run.Status is not (IcpRunStatus.Open or IcpRunStatus.Completed))
            throw new BusinessRuleException("The run is closed.");

        var sample = run.Samples.FirstOrDefault(s => s.Id == runSampleId)
            ?? throw new NotFoundException($"Sample {runSampleId} not found on this run.");
        if (sample.Status != IcpRunSampleStatus.Assigned)
            throw new BusinessRuleException("This sample was already removed from the run.");
        if (await _db.TestAnalyses.AnyAsync(a => a.TestOrderId == sample.TestOrderId && a.IsActive, ct))
            throw new BusinessRuleException("A result has already been sent for review for this sample.");

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
        await EnsureAccessAsync(context.Run, userId, ct, SectionAccess.Read);
        return await BuildEntryDtoAsync(context, ct);
    }

    public async Task<IcpSampleEntryDto> SaveEntryAsync(int runSampleId, SaveIcpSampleEntryRequest r, int userId, CancellationToken ct = default)
    {
        var context = await IcpSampleEntryContext.LoadAsync(_db, runSampleId, ct, _clock);
        await EnsureAccessAsync(context.Run, userId, ct);

        var problem = context.EditableProblem() ?? context.CalibrationProblem();
        if (problem != null)
            throw new BusinessRuleException(problem);

        var needed = context.NeededElements;
        if (needed.Count == 0)
            throw new BusinessRuleException("No specification is configured for this test on this item.");
        if (context.NeedsWeight && r.ActualWeightG is not > 0m)
            throw new BusinessRuleException("Enter the actual weight of the sample (g).");
        if (!context.NeedsWeight && r.ActualWeightG.HasValue)
            throw new BusinessRuleException("The actual weight is only used for assay tests.");

        var inputs = r.Concentrations ?? new List<IcpConcentrationInput>();
        var neededIds = needed.Select(e => e.Id).ToList();
        if (inputs.Any(c => !neededIds.Contains(c.IcpTestElementId)))
            throw new BusinessRuleException("A result belongs to an element that is not needed by this test.");
        foreach (var el in needed.Where(e => inputs.Count(c => c.IcpTestElementId == e.Id) != 1))
            throw new BusinessRuleException($"Exactly one result for {el.Symbol} is required.");
        if (inputs.Any(c => c.ResultPpm < 0m))
            throw new BusinessRuleException("A result cannot be negative.");

        var sample = context.RunSample;
        sample.ActualWeightG = r.ActualWeightG;
        foreach (var input in inputs)
        {
            var row = sample.Concentrations.FirstOrDefault(c => c.IcpTestElementId == input.IcpTestElementId);
            if (row == null)
                sample.Concentrations.Add(new IcpSampleConcentration { IcpTestElementId = input.IcpTestElementId, ResultPpm = input.ResultPpm });
            else
                row.ResultPpm = input.ResultPpm;
        }
        await _db.SaveChangesAsync(ct);
        return await GetSampleEntryAsync(runSampleId, userId, ct);
    }

    // ---- Run completion ----

    public async Task<IcpRunDto> CompleteRunAsync(int runId, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != IcpRunStatus.Open)
            throw new BusinessRuleException("The run is closed.");

        var orderIds = run.Samples.Where(s => s.Status == IcpRunSampleStatus.Assigned).Select(s => s.TestOrderId).ToList();
        var submitted = await _db.TestAnalyses
            .Where(a => orderIds.Contains(a.TestOrderId) && a.IsActive)
            .Select(a => a.TestOrderId).Distinct().ToListAsync(ct);
        var pending = orderIds.Count(id => !submitted.Contains(id));
        if (pending > 0)
            throw new BusinessRuleException($"{pending} assigned sample(s) have not been sent for review. Submit or remove them first.");

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
                row.ElementId, row.Spec.ParameterName is { Length: > 0 } name ? name : row.Symbol, QuantityOf(row.Mode),
                row.Value, row.Display, row.Unit, row.Status,
                !string.IsNullOrWhiteSpace(row.Spec.SpecLimit) ? row.Spec.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(row.Spec),
                row.Problem)).ToList();
        }

        var official = c.Submitted ? await LoadOfficialResultsAsync(c.Order.Id, ct) : new List<IcpOfficialResultDto>();

        var evidenceRows = c.Run.Evidence.Where(e => e.IcpRunSampleId == c.RunSample.Id).OrderByDescending(e => e.UploadedAt).ToList();
        var uploaderIds = evidenceRows.Select(e => e.UploadedByUserId).Distinct().ToList();
        var names = await _db.Users.Where(u => uploaderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var evidence = evidenceRows.Select(e => ToEvidenceDto(e, names.GetValueOrDefault(e.UploadedByUserId))).ToList();

        var states = c.NeededStates();
        var elements = c.NeededElements.Select(e =>
        {
            var state = states.First(s => s.IcpTestElementId == e.Id);
            var entered = c.RunSample.Concentrations.FirstOrDefault(x => x.IcpTestElementId == e.Id);
            return new IcpEntryElementDto(e.Id, e.Symbol, e.TargetPpm, state.Valid, state.Reason, entered?.ResultPpm);
        }).ToList();

        var editableReason = c.EditableProblem();
        var assay = c.Mode == IcpTestMode.Assay;

        var orderLocations = await _db.SampleLocations.AsNoTracking()
            .Include(l => l.Sample).ThenInclude(s => s!.Machine)
            .Include(l => l.MachinePartConfiguration).ThenInclude(c => c!.MachinePart).ThenInclude(p => p!.Machine)
            .Where(l => l.TestOrderId == c.Order.Id)
            .ToListAsync(ct);
        var subjectName = SubjectNameHelper.FormatSingleLocationSubject(orderLocations);

        return new IcpSampleEntryDto(
            c.RunSample.Id, c.Run.Id, c.Run.Code, c.Run.Calibration!.Code, c.Run.Calibration.Status,
            c.Order.Id, c.Sample.ReferenceNumber, c.Sample.BatchNumber, c.Sample.Item?.Name, c.Order.TestCode, c.StageName,
            c.RunSample.Status, c.Mode, assay ? c.Definition.IcpSampleForm : null,
            assay ? c.Definition.IcpTheoreticalAmount : null, assay ? c.Definition.IcpSpecificGravity : null,
            c.RunSample.ActualWeightG, elements, preview, official, evidence,
            editableReason == null, editableReason, c.Submitted, problem == null, problem,
            subjectName);
    }

    private static string QuantityOf(IcpTestMode mode) => mode == IcpTestMode.Assay ? "IcpAssayPercent" : "IcpImpurityPpm";

    // What the ICP recorder stored; Quantity comes from its calculation payload, tolerating rows without one.
    private async Task<List<IcpOfficialResultDto>> LoadOfficialResultsAsync(int testOrderId, CancellationToken ct)
    {
        var rows = await _db.ParameterResults.AsNoTracking()
            .Where(p => p.TestOrderId == testOrderId && p.IsActive)
            .OrderBy(p => p.Id).ToListAsync(ct);
        return rows.Select(p =>
        {
            var quantity = "IcpAssayPercent";
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
