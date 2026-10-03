using System.Text.Json;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// ---- Eligible tests / entry ----

public record EligibleTestDto(
    int TestOrderId, string SampleNumber, string? BatchNumber, string? ProductName, string TestCode,
    DateTime ReceivedAt, string? StageName);

public record HplcReplicateResponseInput(int HplcMethodAnalyteId, decimal Response);
public record HplcReplicateInput(decimal ActualWeightMg, List<HplcReplicateResponseInput> Responses);
public record AssignHplcSamplesRequest(List<int> TestOrderIds);
public record SaveReplicatesRequest(List<HplcReplicateInput> Replicates);
public record SubmitHplcSampleRequest(string Password, string? Comment);

public record HplcReplicateDto(int ReplicateNo, decimal ActualWeightMg, List<HplcReplicateResponseInput> Responses);
public record HplcMethodWeightDto(int HplcMethodAnalyteId, string AnalyteName, decimal TheoreticalWeightStdMg, decimal TheoreticalWeightTestMg);
public record HplcPreviewResultDto(
    int HplcMethodAnalyteId, string ParameterName, string Quantity, int? ReplicateNo,
    decimal Value, string Display, string Unit, ResultStatus Status, string? SpecLimit);

public record HplcOfficialResultDto(
    string ParameterName, string Quantity, int? ReplicateNo, string Display, ResultStatus Status, string? SpecLimit);

public record HplcSampleEntryDto(
    int RunSampleId, int HplcRunId, string RunCode, string SstCode, HplcSstStatus SstStatus,
    int TestOrderId, string SampleNumber, string? BatchNumber, string? ProductName, string TestCode, string? StageName,
    HplcRunSampleStatus Status, string Basis, int? RequiredReplicates,
    List<HplcMethodWeightDto> MethodWeights, List<HplcReplicateDto> Replicates,
    List<HplcPreviewResultDto> Preview, List<HplcOfficialResultDto> Official, List<HplcEvidenceDto> Evidence,
    bool Editable, string? EditableReason, bool Submitted, bool CanSubmit, string? CanSubmitReason);

// HPLC Workspace Part B: sample assignment after SST passes, replicate entry
// with a live (preview) assay calculation. Submission itself is
// HplcMethodAssayRecorder (Send for Review).
public partial class HplcRunService
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

    // Test orders that can be assigned to this run: the method's assay tests in
    // the run's laboratory, not finalized, not cancelled/voided, and not
    // already assigned to another run that has not been abandoned.
    private IQueryable<TestOrder> EligibleOrdersQuery(HplcRun run)
    {
        var codes = _db.TestDefinitions
            .Where(t => (t.WorkflowType == WorkflowType.HplcMethodAssay || t.WorkflowType == WorkflowType.Dissolution)
                && t.HplcMethodId == run.HplcMethodId)
            .Select(t => t.Code);
        var busy = _db.HplcRunSamples
            .Where(s => s.TestOrderId != null && s.Status == HplcRunSampleStatus.Assigned && s.HplcRun!.Status != HplcRunStatus.Abandoned)
            .Select(s => s.TestOrderId!.Value);

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

    public async Task<HplcRunDto> AssignSamplesAsync(int runId, List<int> testOrderIds, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");
        if (run.Sst?.Status != HplcSstStatus.Passed)
            throw new InvalidOperationException("Sample assignment is locked until system suitability passes.");

        var ids = (testOrderIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one test.");

        var eligible = await EligibleOrdersQuery(run).Where(o => ids.Contains(o.Id)).Select(o => o.Id).ToListAsync(ct);
        foreach (var id in ids.Where(id => !eligible.Contains(id)))
        {
            var busyRun = await _db.HplcRunSamples
                .Where(s => s.TestOrderId == id && s.Status == HplcRunSampleStatus.Assigned && s.HplcRun!.Status != HplcRunStatus.Abandoned)
                .Select(s => s.HplcRun!.Code).FirstOrDefaultAsync(ct);
            throw new InvalidOperationException(busyRun != null
                ? $"Test order {id} is already assigned to run {busyRun}."
                : $"Test order {id} is not eligible for this run.");
        }

        var dissolutionCodes = await _db.TestDefinitions
            .Where(t => t.WorkflowType == WorkflowType.Dissolution && t.HplcMethodId == run.HplcMethodId)
            .Select(t => t.Code).ToListAsync(ct);
        if (await _db.TestOrders.AnyAsync(o => ids.Contains(o.Id) && dissolutionCodes.Contains(o.TestCode), ct))
        {
            var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)!;
            if (snapshot.Analytes.Count != 1)
                throw new InvalidOperationException("Dissolution needs an HPLC method with exactly one analyte.");
            if (snapshot.Analytes[0].StandardDilution is not > 0m)
                throw new InvalidOperationException($"Method {snapshot.Abbreviation} needs a standard dilution before dissolution samples can be assigned.");
        }

        var nowUtc = _clock.UtcNow.UtcDateTime;
        foreach (var id in ids)
            run.Samples.Add(new HplcRunSample { TestOrderId = id, Status = HplcRunSampleStatus.Assigned, AssignedAt = nowUtc, AssignedByUserId = userId });

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    public async Task<HplcRunDto> RemoveSampleAsync(int runId, int runSampleId, string reason, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");

        var sample = run.Samples.FirstOrDefault(s => s.Id == runSampleId)
            ?? throw new NotFoundException($"Sample {runSampleId} not found on this run.");
        if (sample.Status != HplcRunSampleStatus.Assigned)
            throw new InvalidOperationException("This sample was already removed from the run.");
        if (sample.WorkingStandardQualificationId != null)
        {
            if (!await _db.WorkingStandardQualifications.AnyAsync(
                    q => q.Id == sample.WorkingStandardQualificationId && q.Status == WorkingStandardQualificationStatus.Draft, ct))
                throw new InvalidOperationException("The qualification has already been submitted.");
        }
        else if (await _db.TestAnalyses.AnyAsync(a => a.TestOrderId == sample.TestOrderId && a.IsActive, ct))
            throw new InvalidOperationException("A result has already been sent for review for this sample.");

        var trimmedReason = ValidateReason(reason);
        sample.Status = HplcRunSampleStatus.Removed;
        sample.RemovedReason = trimmedReason;
        sample.RemovedAt = _clock.UtcNow.UtcDateTime;
        sample.RemovedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    // ---- Entry ----

    public async Task<HplcSampleEntryDto> GetSampleEntryAsync(int runSampleId, int userId, CancellationToken ct = default)
    {
        var context = await HplcSampleEntryContext.LoadAsync(_db, runSampleId, ct);
        await EnsureAccessAsync(context.Run, userId, ct);
        return await BuildEntryDtoAsync(context, ct);
    }

    public async Task<HplcSampleEntryDto> SaveReplicatesAsync(int runSampleId, SaveReplicatesRequest r, int userId, CancellationToken ct = default)
    {
        var context = await HplcSampleEntryContext.LoadAsync(_db, runSampleId, ct);
        await EnsureAccessAsync(context.Run, userId, ct);

        var problem = context.EditableProblem();
        if (problem != null)
            throw new InvalidOperationException(problem);
        if (await IsDissolutionOrderAsync(context.Order.Id, ct))
            throw new InvalidOperationException("Dissolution results are entered on the Testing page, not as replicates.");

        var inputs = r.Replicates ?? new List<HplcReplicateInput>();
        var analyteIds = context.Snapshot.Analytes.Select(a => a.Id).ToList();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input.ActualWeightMg <= 0)
                throw new InvalidOperationException($"Replicate {i + 1}: the sample weight must be greater than zero.");

            var responses = input.Responses ?? new List<HplcReplicateResponseInput>();
            if (responses.Any(x => !analyteIds.Contains(x.HplcMethodAnalyteId)))
                throw new InvalidOperationException($"Replicate {i + 1}: a response belongs to an analyte that is not in the method.");
            foreach (var analyte in context.Snapshot.Analytes)
            {
                var matches = responses.Where(x => x.HplcMethodAnalyteId == analyte.Id).ToList();
                if (matches.Count != 1)
                    throw new InvalidOperationException($"Replicate {i + 1}: exactly one response for {analyte.Name} is required.");
                if (context.IsResidualSolvents)
                {
                    // A solvent that is not detected gives a zero response.
                    if (matches[0].Response < 0)
                        throw new InvalidOperationException($"Replicate {i + 1}: the response for {analyte.Name} must be zero or more.");
                }
                else if (matches[0].Response <= 0)
                    throw new InvalidOperationException($"Replicate {i + 1}: the response for {analyte.Name} must be greater than zero.");
            }
        }

        await ReplaceReplicatesAsync(context.RunSample, inputs, ct);

        return await GetSampleEntryAsync(runSampleId, userId, ct);
    }

    // Replace-all: the old rows are removed first so the (sample, replicate no)
    // unique index never sees both generations at once.
    private async Task ReplaceReplicatesAsync(HplcRunSample runSample, List<HplcReplicateInput> inputs, CancellationToken ct)
    {
        await UnitOfWork.RunAsync(_db, async () =>
        {
            _db.HplcSampleReplicates.RemoveRange(runSample.Replicates);
            runSample.Replicates.Clear();
            await _db.SaveChangesAsync(ct);

            for (var i = 0; i < inputs.Count; i++)
            {
                runSample.Replicates.Add(new HplcSampleReplicate
                {
                    ReplicateNo = i + 1,
                    ActualWeightMg = inputs[i].ActualWeightMg,
                    Responses = inputs[i].Responses
                        .Select(x => new HplcReplicateResponse { HplcMethodAnalyteId = x.HplcMethodAnalyteId, Response = x.Response }).ToList(),
                });
            }
            await _db.SaveChangesAsync(ct);
        });
    }

    // Current-and-past evidence of one run sample, newest first.
    private async Task<List<HplcEvidenceDto>> LoadSampleEvidenceAsync(HplcRun run, int runSampleId, CancellationToken ct)
    {
        var rows = run.Evidence.Where(e => e.HplcRunSampleId == runSampleId).OrderByDescending(e => e.UploadedAt).ToList();
        var uploaderIds = rows.Select(e => e.UploadedByUserId).Distinct().ToList();
        var names = await _db.Users.Where(u => uploaderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return rows.Select(e => ToEvidenceDto(e, names.GetValueOrDefault(e.UploadedByUserId))).ToList();
    }

    // ---- Run completion ----

    public async Task<HplcRunDto> CompleteRunAsync(int runId, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");

        var assigned = run.Samples.Where(s => s.Status == HplcRunSampleStatus.Assigned).ToList();
        var orderIds = assigned.Where(s => s.TestOrderId != null).Select(s => s.TestOrderId!.Value).ToList();
        var submittedOrderIds = await SubmittedOrderIdsAsync(orderIds, ct);
        var draftQualificationIds = assigned.Where(s => s.WorkingStandardQualificationId != null).Select(s => s.WorkingStandardQualificationId!.Value).ToList();
        var pending = assigned.Count(s => s.TestOrderId != null && !submittedOrderIds.Contains(s.TestOrderId.Value))
            + await _db.WorkingStandardQualifications.CountAsync(
                q => draftQualificationIds.Contains(q.Id) && q.Status == WorkingStandardQualificationStatus.Draft, ct);
        if (pending > 0)
            throw new InvalidOperationException($"{pending} assigned sample(s) have not been sent for review. Submit or remove them first.");

        run.Status = HplcRunStatus.Completed;
        run.ClosedAt = _clock.UtcNow.UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    // Orders whose result went for review: an active result, and for dissolution
    // (Stage 1 can be signed while the order stays open for the next stage) also a finalized step.
    private async Task<List<int>> SubmittedOrderIdsAsync(List<int> orderIds, CancellationToken ct)
    {
        var withResult = await _db.TestAnalyses
            .Where(a => orderIds.Contains(a.TestOrderId) && a.IsActive)
            .Select(a => a.TestOrderId).Distinct().ToListAsync(ct);
        if (withResult.Count == 0) return withResult;
        var dissolutionOpen = await _db.TestOrders
            .Where(o => withResult.Contains(o.Id) && !FinalizedSteps.Contains(o.CurrentStep)
                && _db.TestDefinitions.Any(t => t.Code == o.TestCode && t.WorkflowType == WorkflowType.Dissolution))
            .Select(o => o.Id).ToListAsync(ct);
        return withResult.Except(dissolutionOpen).ToList();
    }

    private async Task<bool> IsDissolutionOrderAsync(int testOrderId, CancellationToken ct) =>
        await _db.TestOrders.AnyAsync(o => o.Id == testOrderId
            && _db.TestDefinitions.Any(t => t.Code == o.TestCode && t.WorkflowType == WorkflowType.Dissolution), ct);

    // ---- Mapping ----

    private async Task<HplcSampleEntryDto> BuildEntryDtoAsync(HplcSampleEntryContext c, CancellationToken ct)
    {
        var individual = !c.IsResidualSolvents && HplcAssayCalculator.IsIndividualBasis(c.StageRole);
        var problem = c.SubmitProblem();

        // The preview is what submission would store; shown only once every
        // value it needs is entered (the sample report is not needed for it).
        var preview = new List<HplcPreviewResultDto>();
        if (c.EntryProblem() == null)
        {
            var rows = c.EvaluateRows();
            preview = rows.Select(row => new HplcPreviewResultDto(
                row.AnalyteId, row.AnalyteName,
                row.Basis == ResultBasis.Ppm ? "Residual solvent (ppm)" : row.Basis == ResultBasis.MgPerUnit ? "Amount per unit" : "Assay %", row.ReplicateNo,
                row.Value, row.Display, row.Unit, row.Status,
                !string.IsNullOrWhiteSpace(row.Spec.SpecLimit) ? row.Spec.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(row.Spec))).ToList();
        }

        var official = c.Submitted ? await LoadOfficialResultsAsync(c.Order.Id, ct) : new List<HplcOfficialResultDto>();

        var evidence = await LoadSampleEvidenceAsync(c.Run, c.RunSample.Id, ct);

        var replicates = c.RunSample.Replicates.OrderBy(r => r.ReplicateNo)
            .Select(r => new HplcReplicateDto(r.ReplicateNo, r.ActualWeightMg,
                r.Responses.OrderBy(x => x.HplcMethodAnalyteId).Select(x => new HplcReplicateResponseInput(x.HplcMethodAnalyteId, x.Response)).ToList()))
            .ToList();

        var weights = c.Snapshot.Analytes.OrderBy(a => a.DisplayOrder)
            .Select(a => new HplcMethodWeightDto(a.Id, a.Name, a.TheoreticalWeightStdMg, a.TheoreticalWeightTestMg)).ToList();

        var editableReason = c.EditableProblem();
        return new HplcSampleEntryDto(
            c.RunSample.Id, c.Run.Id, c.Run.Code, c.Run.Sst?.Code ?? string.Empty, c.Run.Sst?.Status ?? HplcSstStatus.Pending,
            c.Order.Id, c.Sample.ReferenceNumber, c.Sample.BatchNumber, c.Sample.Item?.Name, c.Order.TestCode, c.StageName,
            c.RunSample.Status, individual ? "Individual" : "Mean", c.Replicates.SampleReplicates,
            weights, replicates, preview, official, evidence,
            editableReason == null, editableReason, c.Submitted, problem == null, problem);
    }

    // What HplcMethodAssayRecorder stored; Quantity and replicate number come
    // from its calculation payload, tolerating rows without one.
    private async Task<List<HplcOfficialResultDto>> LoadOfficialResultsAsync(int testOrderId, CancellationToken ct)
    {
        var rows = await _db.ParameterResults.AsNoTracking()
            .Where(p => p.TestOrderId == testOrderId && p.IsActive)
            .OrderBy(p => p.Id).ToListAsync(ct);
        return rows.Select(p =>
        {
            var quantity = "AssayPercent";
            int? replicateNo = null;
            if (!string.IsNullOrWhiteSpace(p.CalculationJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(p.CalculationJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(q.GetString()))
                            quantity = q.GetString()!;
                        if (doc.RootElement.TryGetProperty("replicateNo", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var no))
                            replicateNo = no;
                    }
                }
                catch (JsonException) { }
            }
            return new HplcOfficialResultDto(p.ParameterName, quantity, replicateNo, p.ReportedDisplay, p.ComparisonStatus, p.SpecLimit);
        }).ToList();
    }

    private async Task<List<HplcRunSampleSummaryDto>> BuildSampleSummariesAsync(HplcRun run, CancellationToken ct)
    {
        if (run.Samples.Count == 0) return new List<HplcRunSampleSummaryDto>();

        var orderIds = run.Samples.Where(s => s.TestOrderId != null).Select(s => s.TestOrderId!.Value).ToList();
        var orders = await _db.TestOrders.AsNoTracking()
            .Include(o => o.Sample).ThenInclude(s => s!.Item)
            .Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
        var submitted = (await SubmittedOrderIdsAsync(orderIds, ct)).ToHashSet();

        var codes = orders.Values.Select(o => o.TestCode).Distinct().ToList();
        var dissolutionCodes = (await _db.TestDefinitions.Where(t => codes.Contains(t.Code) && t.WorkflowType == WorkflowType.Dissolution)
            .Select(t => t.Code).ToListAsync(ct)).ToHashSet();
        var qualIds = run.Samples.Where(s => s.WorkingStandardQualificationId != null).Select(s => s.WorkingStandardQualificationId!.Value).ToList();
        var quals = await _db.WorkingStandardQualifications.AsNoTracking()
            .Where(q => qualIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);

        return run.Samples.OrderBy(s => s.Id).Select(s =>
        {
            if (s.WorkingStandardQualificationId is int qId && quals.TryGetValue(qId, out var q))
                return new HplcRunSampleSummaryDto(
                    s.Id, null, s.Status, q.Code, q.SourceBatchNumber, q.SourceMaterialName, "WS qualification",
                    q.Status != WorkingStandardQualificationStatus.Draft, false, q.Id);

            TestOrder? o = null;
            if (s.TestOrderId is int id) orders.TryGetValue(id, out o);
            return new HplcRunSampleSummaryDto(
                s.Id, s.TestOrderId, s.Status, o?.Sample?.ReferenceNumber ?? string.Empty, o?.Sample?.BatchNumber,
                o?.Sample?.Item?.Name, o?.TestCode ?? string.Empty, s.TestOrderId is int tid && submitted.Contains(tid),
                o != null && dissolutionCodes.Contains(o.TestCode), s.WorkingStandardQualificationId);
        }).ToList();
    }
}
