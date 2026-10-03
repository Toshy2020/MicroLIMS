using System.Text.Json;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public record EligibleQualificationDto(
    int QualificationId, string Code, WorkingStandardQualificationKind Kind, string MaterialName, string BatchNumber, string AnalyteName);
public record AssignQualificationsRequest(List<int> QualificationIds);
public record WorkingStandardPreviewDto(
    List<decimal> ReplicateAssayPercents, decimal MeanAssayPercent, decimal? RsdPercent, decimal PotencyPercent, bool Passed, string? FailureReasons);
public record HplcQualificationEntryDto(
    int RunSampleId, int HplcRunId, string RunCode, string SstCode, HplcSstStatus SstStatus,
    int QualificationId, string QualificationCode, WorkingStandardQualificationStatus QualificationStatus,
    string MaterialName, string BatchNumber, decimal? MoisturePercent,
    List<HplcMethodWeightDto> MethodWeights, int RequiredReplicates, List<HplcReplicateDto> Replicates,
    WorkingStandardPreviewDto? Preview, List<HplcEvidenceDto> Evidence,
    bool Editable, string? EditableReason, bool Submitted, bool CanSubmit, string? CanSubmitReason);

// Working standard qualification samples on an HPLC run (spec 2026-10-03):
// assigned like a sample after SST passes, six replicates entered against the
// qualified analyte, submitted with a signature. The assay itself reuses
// HplcAssayCalculator; pass/fail and potency come from WorkingStandardCalculator.
public partial class HplcRunService
{
    // ---- Eligible / assign ----

    public async Task<List<EligibleQualificationDto>> GetEligibleQualificationsAsync(int runId, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);
        return (await EligibleQualificationsAsync(run, null, ct)).Select(e => e.Dto).ToList();
    }

    private async Task<List<(WorkingStandardQualification Q, HplcMethodAnalyteResponse Analyte, EligibleQualificationDto Dto)>>
        EligibleQualificationsAsync(HplcRun run, List<int>? ids, CancellationToken ct)
    {
        var snapshot = ReadSnapshot(run);
        var entryIds = snapshot.Analytes.Select(a => a.StandardEntryId).ToList();
        var busy = _db.HplcRunSamples
            .Where(s => s.WorkingStandardQualificationId != null && s.Status == HplcRunSampleStatus.Assigned
                && s.HplcRun!.Status != HplcRunStatus.Abandoned)
            .Select(s => s.WorkingStandardQualificationId!.Value);

        var query = _db.WorkingStandardQualifications.Include(q => q.Documents)
            .Where(q => q.Status == WorkingStandardQualificationStatus.Draft && q.SectionId == run.SectionId
                && entryIds.Contains(q.MaterialMasterEntryId) && !busy.Contains(q.Id));
        if (ids != null) query = query.Where(q => ids.Contains(q.Id));

        var list = await query.OrderBy(q => q.CreatedAt).ThenBy(q => q.Id).ToListAsync(ct);
        return list.Where(q => WorkingStandardService.AssignProblem(q) == null).Select(q =>
        {
            var analyte = snapshot.Analytes.First(a => a.StandardEntryId == q.MaterialMasterEntryId);
            return (q, analyte, new EligibleQualificationDto(q.Id, q.Code, q.Kind, q.SourceMaterialName, q.SourceBatchNumber, analyte.Name));
        }).ToList();
    }

    public async Task<HplcRunDto> AssignQualificationsAsync(int runId, List<int> qualificationIds, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");
        if (run.Sst?.Status != HplcSstStatus.Passed)
            throw new InvalidOperationException("Sample assignment is locked until system suitability passes.");
        if (run.Sst.Analytes.Any(a => a.StandardMaterial?.MaterialType != MaterialType.ReferenceStandard))
            throw new InvalidOperationException(
                "A working standard can only be qualified against a primary reference standard - this run's system suitability used a working standard.");

        var ids = (qualificationIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one qualification.");

        var eligible = await EligibleQualificationsAsync(run, ids, ct);
        foreach (var id in ids.Where(id => eligible.All(e => e.Q.Id != id)))
        {
            var code = await _db.WorkingStandardQualifications.Where(q => q.Id == id).Select(q => q.Code).FirstOrDefaultAsync(ct) ?? id.ToString();
            var busyRun = await _db.HplcRunSamples
                .Where(s => s.WorkingStandardQualificationId == id && s.Status == HplcRunSampleStatus.Assigned && s.HplcRun!.Status != HplcRunStatus.Abandoned)
                .Select(s => s.HplcRun!.Code).FirstOrDefaultAsync(ct);
            throw new InvalidOperationException(busyRun != null
                ? $"Qualification {code} is already assigned to run {busyRun}."
                : $"Qualification {code} is not eligible for this run.");
        }

        var nowUtc = _clock.UtcNow.UtcDateTime;
        foreach (var (q, analyte, _) in eligible)
        {
            q.HplcMethodAnalyteId = analyte.Id;
            run.Samples.Add(new HplcRunSample
            {
                WorkingStandardQualificationId = q.Id, Status = HplcRunSampleStatus.Assigned,
                AssignedAt = nowUtc, AssignedByUserId = userId,
            });
        }

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    // ---- Entry ----

    public async Task<HplcQualificationEntryDto> GetQualificationEntryAsync(int runSampleId, int userId, CancellationToken ct = default)
    {
        var c = await LoadQualificationSampleAsync(runSampleId, ct);
        await EnsureAccessAsync(c.Run, userId, ct);
        return await BuildQualificationEntryAsync(c, ct);
    }

    public async Task<HplcQualificationEntryDto> SaveQualificationReplicatesAsync(int runSampleId, SaveReplicatesRequest r, int userId, CancellationToken ct = default)
    {
        var c = await LoadQualificationSampleAsync(runSampleId, ct);
        await EnsureAccessAsync(c.Run, userId, ct);

        var problem = QualificationEditableProblem(c.Run, c.RunSample, c.Q);
        if (problem != null)
            throw new InvalidOperationException(problem);

        var inputs = r.Replicates ?? new List<HplcReplicateInput>();
        for (var i = 0; i < inputs.Count; i++)
        {
            if (inputs[i].ActualWeightMg <= 0)
                throw new InvalidOperationException($"Replicate {i + 1}: the sample weight must be greater than zero.");
            var responses = inputs[i].Responses ?? new List<HplcReplicateResponseInput>();
            var matches = responses.Where(x => x.HplcMethodAnalyteId == c.Analyte.Id).ToList();
            if (matches.Count != 1 || responses.Count != 1)
                throw new InvalidOperationException($"Replicate {i + 1}: exactly one response for {c.Analyte.Name} is required.");
            if (matches[0].Response <= 0)
                throw new InvalidOperationException($"Replicate {i + 1}: the response for {c.Analyte.Name} must be greater than zero.");
        }

        await ReplaceReplicatesAsync(c.RunSample, inputs, ct);
        return await BuildQualificationEntryAsync(c, ct);
    }

    public async Task<HplcQualificationEntryDto> SubmitQualificationAsync(int runSampleId, SubmitHplcSampleRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var c = await LoadQualificationSampleAsync(runSampleId, ct);
        await EnsureAccessAsync(c.Run, userId, ct);

        var problem = QualificationSubmitProblem(c);
        if (problem != null)
            throw new InvalidOperationException(problem);

        // Pure calculation first, then sign (a failed password saves its own
        // audit row), then mutate - same order as ConfirmSstAsync.
        var result = EvaluateQualification(c);

        var signature = await _signatures.SignAsync(
            userId, r.Password, SignatureMeaning.ResultRecorded, nameof(WorkingStandardQualification), c.Q.Id, r.Comment, ip);

        var q = c.Q;
        q.MeanAssayPercent = result.MeanAssayPercent;
        q.RsdPercent = result.RsdPercent;
        q.PotencyPercent = result.PotencyPercent;
        q.Passed = result.Passed;
        q.FailureReasons = result.FailureReasons;
        q.ReplicateAssaysJson = JsonSerializer.Serialize(result.ReplicateAssayPercents);
        q.Status = WorkingStandardQualificationStatus.Assayed;
        q.PreparedByUserId = userId;
        q.PreparedAt = _clock.UtcNow.UtcDateTime;
        q.PreparedSignatureId = signature.Id;
        q.ReturnReason = null;

        await _db.SaveChangesAsync(ct);
        return await BuildQualificationEntryAsync(c, ct);
    }

    // ---- Helpers ----

    private sealed record QualificationSampleContext(
        HplcRun Run, HplcRunSample RunSample, WorkingStandardQualification Q, HplcMethodResponse Snapshot, HplcMethodAnalyteResponse Analyte);

    private HplcMethodResponse ReadSnapshot(HplcRun run) =>
        JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The run's method snapshot could not be read.");

    private async Task<QualificationSampleContext> LoadQualificationSampleAsync(int runSampleId, CancellationToken ct)
    {
        var runSample = await _db.HplcRunSamples.Include(s => s.Replicates).ThenInclude(r => r.Responses)
            .FirstOrDefaultAsync(s => s.Id == runSampleId, ct)
            ?? throw new NotFoundException($"Run sample {runSampleId} not found.");
        if (runSample.WorkingStandardQualificationId is not int qId)
            throw new InvalidOperationException("This run sample is a test order - open it from the sample entry.");
        var run = await LoadRunAsync(runSample.HplcRunId, ct);
        var q = await _db.WorkingStandardQualifications.Include(x => x.Documents).FirstAsync(x => x.Id == qId, ct);
        var snapshot = ReadSnapshot(run);
        var analyte = snapshot.Analytes.First(a => a.Id == q.HplcMethodAnalyteId);
        return new QualificationSampleContext(run, runSample, q, snapshot, analyte);
    }

    private static string? QualificationEditableProblem(HplcRun run, HplcRunSample runSample, WorkingStandardQualification q)
    {
        if (runSample.Status != HplcRunSampleStatus.Assigned) return "This sample was removed from the run.";
        if (q.Status != WorkingStandardQualificationStatus.Draft) return "The qualification has already been submitted.";
        if (run.Status == HplcRunStatus.Open) return null;
        if (run.Status == HplcRunStatus.Completed && q.ReturnReason != null) return null;
        return "The run is closed.";
    }

    // Everything the calculation needs except the sample report.
    private static string? QualificationEntryProblem(QualificationSampleContext c)
    {
        var editable = QualificationEditableProblem(c.Run, c.RunSample, c.Q);
        if (editable != null) return editable;
        if (c.Run.Sst?.Status != HplcSstStatus.Passed) return "System suitability has not passed.";

        var entered = c.RunSample.Replicates.Count;
        if (entered != WorkingStandardRules.Replicates)
            return $"Exactly {WorkingStandardRules.Replicates} replicates are required ({entered} entered).";

        var sstRow = c.Run.Sst.Analytes.FirstOrDefault(a => a.HplcMethodAnalyteId == c.Analyte.Id);
        if (sstRow?.MeanResponse is null or <= 0 || sstRow.StandardWeightMg is null or <= 0
            || sstRow.StandardPurityPercent is null or <= 0 || sstRow.StandardMoisturePercent is null)
            return $"{c.Analyte.Name}: the system suitability standard values are incomplete.";
        if (c.Q.MoisturePercent == null) return "Enter the moisture content first.";
        return null;
    }

    private static string? QualificationSubmitProblem(QualificationSampleContext c)
    {
        var problem = QualificationEntryProblem(c);
        if (problem != null) return problem;
        var hasReport = c.Run.Evidence.Any(e => e.HplcRunSampleId == c.RunSample.Id
            && e.Kind == HplcEvidenceKind.SampleReport && e.SupersededByEvidenceId == null);
        return hasReport ? null : "Upload the sample report before submitting.";
    }

    // Only called when QualificationEntryProblem is null.
    private static WorkingStandardResult EvaluateQualification(QualificationSampleContext c)
    {
        var sstRow = c.Run.Sst!.Analytes.First(a => a.HplcMethodAnalyteId == c.Analyte.Id);
        var std = new AssayStandard(sstRow.MeanResponse!.Value, sstRow.StandardWeightMg!.Value,
            sstRow.StandardPurityPercent!.Value, sstRow.StandardMoisturePercent!.Value);
        var reps = c.RunSample.Replicates.OrderBy(r => r.ReplicateNo)
            .Select(r => new AssayReplicateInput(r.ReplicateNo, r.ActualWeightMg,
                r.Responses.First(x => x.HplcMethodAnalyteId == c.Analyte.Id).Response))
            .ToList();
        var assay = HplcAssayCalculator.Calculate(c.Analyte.Id, c.Analyte.Name,
            c.Analyte.TheoreticalWeightStdMg, c.Analyte.TheoreticalWeightTestMg, std, reps);
        return WorkingStandardCalculator.Evaluate(assay.Replicates.Select(x => x.AssayPercent).ToList(), c.Q.MoisturePercent!.Value);
    }

    private async Task<HplcQualificationEntryDto> BuildQualificationEntryAsync(QualificationSampleContext c, CancellationToken ct)
    {
        var editableReason = QualificationEditableProblem(c.Run, c.RunSample, c.Q);
        var submitProblem = QualificationSubmitProblem(c);

        WorkingStandardPreviewDto? preview = null;
        if (QualificationEntryProblem(c) == null)
        {
            var result = EvaluateQualification(c);
            preview = new WorkingStandardPreviewDto(result.ReplicateAssayPercents.ToList(), result.MeanAssayPercent,
                result.RsdPercent, result.PotencyPercent, result.Passed, result.FailureReasons);
        }

        var replicates = c.RunSample.Replicates.OrderBy(r => r.ReplicateNo)
            .Select(r => new HplcReplicateDto(r.ReplicateNo, r.ActualWeightMg,
                r.Responses.OrderBy(x => x.HplcMethodAnalyteId).Select(x => new HplcReplicateResponseInput(x.HplcMethodAnalyteId, x.Response)).ToList()))
            .ToList();
        var weights = new List<HplcMethodWeightDto>
        {
            new(c.Analyte.Id, c.Analyte.Name, c.Analyte.TheoreticalWeightStdMg, c.Analyte.TheoreticalWeightTestMg),
        };
        var evidence = await LoadSampleEvidenceAsync(c.Run, c.RunSample.Id, ct);

        return new HplcQualificationEntryDto(
            c.RunSample.Id, c.Run.Id, c.Run.Code, c.Run.Sst?.Code ?? string.Empty, c.Run.Sst?.Status ?? HplcSstStatus.Pending,
            c.Q.Id, c.Q.Code, c.Q.Status, c.Q.SourceMaterialName, c.Q.SourceBatchNumber, c.Q.MoisturePercent,
            weights, WorkingStandardRules.Replicates, replicates, preview, evidence,
            editableReason == null, editableReason, c.Q.Status != WorkingStandardQualificationStatus.Draft,
            submitProblem == null, submitProblem);
    }
}
