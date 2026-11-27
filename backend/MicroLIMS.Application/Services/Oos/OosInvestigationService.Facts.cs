using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Oos;

public sealed record OosChecklistFactsDto(uint Version, IReadOnlyList<OosOrderFactsDto> Orders);
public sealed record OosOrderFactsDto(int TestOrderId, int ResultAuditCount,
    IReadOnlyList<OosEquipmentFact> Equipment, IReadOnlyList<OosMediaFact> Media,
    IReadOnlyList<OosSolutionFact> Solutions, IReadOnlyList<OosStandardFact> Standards,
    IReadOnlyList<OosIncubationFact> Incubations);
public sealed record OosEquipmentFact(int EquipmentId, DateTime? UsedAt,
    IcpCalibrationStatus? RunCalibrationStatus, DateTime? RunCalibrationConfirmedAt, DateTime? RunAssignedAt);
public sealed record OosMediaFact(int MediaId, string LotNumber, bool IsReleasedForUse, DateTime ExpiryDate);
public sealed record OosSolutionFact(int SolutionPreparationId, string? Code, ApprovalGateStatus? ApprovalStatus, DateTime? ReleasedAt, DateTime? ExpiresAt);
public sealed record OosStandardFact(int MaterialId, decimal? PurityPercent, decimal? MoisturePercent);
public sealed record OosIncubationFact(int StepNumber, int StageNumber, DateTime StartedAt, DateTime? CompletedAt,
    DateTime? IncubationStartUtc, DateTime? IncubationEndUtc, int? CurrentMinimumHours, int? CurrentMaximumHours);

public sealed partial class OosInvestigationService
{
    public async Task<OosChecklistFactsDto> GetFactsAsync(int investigationId, CancellationToken ct = default)
    {
        var actor = db.CurrentUserId ?? 0;
        if (!await permissions.UserHasPermissionAsync(actor, PermissionConstants.OosView, ct))
            throw new CodedAccessDeniedException(MessageCodes.OosInvestigationAccessDenied, "You do not have permission for this investigation action.");

        var investigation = await db.OosInvestigations.AsNoTracking().Where(i => i.Id == investigationId)
            .Select(i => new { i.SectionId, i.Version }).SingleOrDefaultAsync(ct)
            ?? throw InvestigationNotFound();
        var readable = await scope.GetReadableSectionIdsAsync(actor, ct);
        if (readable is not null && !readable.Contains(investigation.SectionId))
            throw InvestigationNotFound();

        var orders = await db.OosInvestigationResults.AsNoTracking().Where(r => r.InvestigationId == investigationId)
            .Join(db.TestOrders.AsNoTracking(), r => r.TestOrderId, o => o.Id, (_, order) => new { order.Id, order.TestCode })
            .OrderBy(o => o.Id).ToListAsync(ct);
        var analyst = await db.Users.AsNoTracking().AnyAsync(u => u.Id == actor && u.Role != null && u.Role.Type == RoleType.Analyst, ct);
        if (analyst && !await permissions.UserHasPermissionAsync(actor, PermissionConstants.OosManage, ct) &&
            (await duties.GetTestOrderIdsPerformedByAsync(actor, orders.Select(o => o.Id).ToArray(), ct)).Count == 0)
            throw InvestigationNotFound();

        var facts = new List<OosOrderFactsDto>(orders.Count);
        foreach (var order in orders)
            facts.Add(await GetOrderFactsAsync(order.Id, order.TestCode, investigation.SectionId, ct));
        return new(investigation.Version, facts);
    }

    private async Task<OosOrderFactsDto> GetOrderFactsAsync(int id, string testCode, int sectionId, CancellationToken ct = default)
    {
        var incubations = db.Incubations.AsNoTracking().Where(i => i.TestOrderId == id);
        var analyses = db.TestAnalyses.AsNoTracking().Where(a => a.TestOrderId == id);
        var hplcRuns = db.HplcRunSamples.AsNoTracking().Where(s => s.TestOrderId == id && s.RemovedAt == null)
            .Join(db.HplcRuns.AsNoTracking(), s => s.HplcRunId, r => r.Id, (_, run) => run);

        // The use records store equipment identity and time. No calibration
        // status at time of use is stored on them.
        var equipment = await analyses.Where(a => a.EquipmentId.HasValue)
            .Select(a => new { a.EquipmentId, UsedAt = a.AnalysedAt })
            .Concat(incubations.Where(i => i.IncubatorEquipmentId.HasValue)
                .Select(i => new { EquipmentId = i.IncubatorEquipmentId, UsedAt = i.StartedAt }))
            .Concat(hplcRuns.Select(r => new { EquipmentId = (int?)r.EquipmentId, UsedAt = r.StartedAt }))
            .Distinct().OrderBy(e => e.UsedAt).ThenBy(e => e.EquipmentId)
            .Select(e => new OosEquipmentFact(e.EquipmentId.GetValueOrDefault(), e.UsedAt, null, null, null)).ToListAsync(ct);

        var icpSamples = db.IcpRunSamples.AsNoTracking().Where(s => s.TestOrderId == id && s.RemovedAt == null);
        var icpEquipment = await icpSamples.Join(db.IcpRuns.AsNoTracking(), s => s.IcpRunId, r => r.Id,
            (sample, run) => new OosEquipmentFact(run.EquipmentId, null,
                run.Calibration == null ? null : run.Calibration.Status, run.Calibration == null ? null : run.Calibration.ConfirmedAt,
                sample.AssignedAt)).ToListAsync(ct);
        equipment.AddRange(icpEquipment);

        var sampleIds = db.TestOrders.AsNoTracking().Where(o => o.Id == id).Select(o => o.SampleId);
        var preparations = db.SamplePreparations.AsNoTracking().Where(p => sampleIds.Contains(p.SampleId));
        var mediaIds = db.MediaUsages.AsNoTracking().Where(u => u.TestOrderId == id).Select(u => u.MediaId)
            .Union(incubations.Where(i => i.MediaId.HasValue).Select(i => i.MediaId.GetValueOrDefault()))
            .Union(preparations.Where(p => p.DiluentMediaId.HasValue).Select(p => p.DiluentMediaId.GetValueOrDefault()))
            .Union(preparations.Where(p => p.NeutralizerMediaId.HasValue).Select(p => p.NeutralizerMediaId.GetValueOrDefault()));
        var media = await db.Media.AsNoTracking().Where(m => mediaIds.Contains(m.Id)).OrderBy(m => m.Id)
            .Select(m => new OosMediaFact(m.Id, m.LotNumber, m.IsReleasedForUse, m.ExpiryDate)).ToListAsync(ct);

        var titration = await analyses.Where(a => a.AnalysisType == WorkflowType.Titration && a.ConditionsJson != null)
            .Select(a => a.ConditionsJson).ToListAsync(ct);
        var titrationSolutions = new HashSet<int>();
        var titrationStandards = new HashSet<OosStandardFact>();
        foreach (var snapshot in titration.OfType<string>())
            ReadTitrationLots(snapshot, titrationSolutions, titrationStandards);

        var runIds = hplcRuns.Select(r => r.Id);
        var solutionIds = db.HplcRunMobilePhases.AsNoTracking().Where(p => runIds.Contains(p.HplcRunId))
            .Select(p => p.SolutionPreparationId)
            .Union(preparations.Where(p => p.DiluentSolutionPreparationId.HasValue).Select(p => p.DiluentSolutionPreparationId.GetValueOrDefault()))
            .Union(preparations.Where(p => p.NeutralizerSolutionPreparationId.HasValue).Select(p => p.NeutralizerSolutionPreparationId.GetValueOrDefault()));
        var solutions = await db.SolutionPreparations.AsNoTracking().Where(s => solutionIds.Contains(s.Id) || titrationSolutions.Contains(s.Id)).OrderBy(s => s.Id)
            .Select(s => new OosSolutionFact(s.Id, s.Code, s.ApprovalStatus, s.ReleasedAt, s.ExpiresAt)).ToListAsync(ct);

        var standards = await db.HplcSstAnalytes.AsNoTracking()
            .Where(a => a.StandardMaterialId.HasValue && a.HplcSstRecord != null && runIds.Contains(a.HplcSstRecord.HplcRunId))
            .Select(a => new { MaterialId = a.StandardMaterialId.GetValueOrDefault(), a.StandardPurityPercent, a.StandardMoisturePercent })
            .Distinct().OrderBy(s => s.MaterialId)
            .Select(a => new OosStandardFact(a.MaterialId, a.StandardPurityPercent, a.StandardMoisturePercent)).ToListAsync(ct);
        var icpRunIds = icpSamples.Select(s => s.IcpRunId);
        var calibrationStandards = await db.IcpCalibrations.AsNoTracking()
            .Where(c => icpRunIds.Contains(c.IcpRunId) && c.CalibrationStandardMaterialId.HasValue)
            .Select(c => c.CalibrationStandardMaterialId.GetValueOrDefault()).Distinct().OrderBy(id => id).ToListAsync(ct);
        standards.AddRange(calibrationStandards.Select(materialId => new OosStandardFact(materialId, null, null)));
        standards.AddRange(titrationStandards);

        var steps = db.TestWorkflowSteps.AsNoTracking().Where(s => s.TestDefinition != null &&
            s.TestDefinition.Code == testCode && s.TestDefinition.SectionId == sectionId);
        var windows = await incubations.OrderBy(i => i.Id).Select(i => new OosIncubationFact(i.StepNumber,
            i.StageNumber, i.StartedAt, i.CompletedAt, i.IncubationStartUtc, i.IncubationEndUtc,
            i.StageNumber == 1
                ? steps.Where(s => s.StepOrder == i.StepNumber).Select(s => (int?)s.IncubationMinHours).FirstOrDefault()
                : steps.Where(s => s.StepOrder == i.StepNumber).SelectMany(s => s.IncubationStages)
                    .Where(s => s.StageNumber == i.StageNumber).Select(s => (int?)s.IncubationMinHours).FirstOrDefault(),
            i.StageNumber == 1
                ? steps.Where(s => s.StepOrder == i.StepNumber).Select(s => (int?)s.IncubationMaxHours).FirstOrDefault()
                : steps.Where(s => s.StepOrder == i.StepNumber).SelectMany(s => s.IncubationStages)
                    .Where(s => s.StageNumber == i.StageNumber).Select(s => (int?)s.IncubationMaxHours).FirstOrDefault()))
            .ToListAsync(ct);

        var resultIds = db.Results.AsNoTracking().Where(r => r.TestOrderId == id).Select(r => r.Id.ToString());
        var parameterIds = db.ParameterResults.AsNoTracking().Where(r => r.TestOrderId == id).Select(r => r.Id.ToString());
        var auditCount = await db.AuditLogs.AsNoTracking().CountAsync(a =>
            (a.EntityName == nameof(Result) && resultIds.Contains(a.EntityId)) ||
            (a.EntityName == nameof(ParameterResult) && parameterIds.Contains(a.EntityId)), ct);
        return new(id, auditCount, equipment, media, solutions, standards, windows);
    }

    private static void ReadTitrationLots(string snapshot, HashSet<int> solutions, HashSet<OosStandardFact> standards)
    {
        using var document = JsonDocument.Parse(snapshot);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return;
        foreach (var key in new[] { "titrant", "excess" })
        {
            if (root.TryGetProperty(key, out var lot) && lot.ValueKind == JsonValueKind.Object &&
                lot.TryGetProperty("preparationId", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var preparationId))
                solutions.Add(preparationId);
        }
        if (root.TryGetProperty("standard", out var standard) && standard.ValueKind == JsonValueKind.Object &&
            standard.TryGetProperty("lotId", out var lotId) && lotId.ValueKind == JsonValueKind.Number && lotId.TryGetInt32(out var materialId))
            standards.Add(new(materialId, ReadDecimal(standard, "purityPercent"), ReadDecimal(standard, "moisturePercent")));
    }

    private static decimal? ReadDecimal(JsonElement item, string key) =>
        item.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetDecimal(out var value)
            ? value : null;
}
