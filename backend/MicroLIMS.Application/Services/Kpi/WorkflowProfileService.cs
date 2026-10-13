using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Kpi;

// Workflow profiles (KPI remodel S1, spec section 2): validation, e-signature and
// audit for the append-only WorkflowProfileVersions table, plus the read models
// behind the configuration panel. The legacy WorkloadWeight maths are untouched.
public class WorkflowProfileService
{
    public const string VersionAddedActionCode = "WorkflowProfileVersionAdded";
    public const string DraftSignedActionCode = "WorkflowProfileDraftSigned";

    // Retired types (HplcAssay, ElementalAssay, HplcMultiAnalyte, StandardComparison) are
    // deliberately absent: they cannot carry a profile.
    public static readonly WorkflowType[] LiveTypes =
    {
        WorkflowType.CountTest, WorkflowType.Observation, WorkflowType.Measurement, WorkflowType.Gravimetric,
        WorkflowType.Qualitative, WorkflowType.Dissolution, WorkflowType.Disintegration, WorkflowType.WeightVariation,
        WorkflowType.HplcMethodAssay, WorkflowType.Titration, WorkflowType.IcpMethodAssay
    };

    private const decimal MaxWorkUnits = 999999.99m; // numeric(8,2)
    private const decimal MaxTargetDays = 9999.99m;  // numeric(6,2)

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _sig;
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public WorkflowProfileService(
        IMicroLimsDbContext db,
        IElectronicSignatureService sig,
        IAuditEventService audit,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _sig = sig;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    // ---- Writes ----------------------------------------------------------

    public Task<WorkflowProfileVersionDto> AddVersionAsync(
        CreateWorkflowProfileVersionRequest r, int userId, string? ip, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => AddVersionCoreAsync(r, userId, ip, ct));

    private async Task<WorkflowProfileVersionDto> AddVersionCoreAsync(
        CreateWorkflowProfileVersionRequest r, int userId, string? ip, CancellationToken ct)
    {
        var (type, code, reason) = await ValidateAsync(
            r.WorkflowType, r.TestCode, r.WorkUnits, r.TargetAnalystDays, r.ReasonForChange, ct);
        await EnsureEffectiveFromAsync(type, code, r.EffectiveFrom, ct);

        _db.CurrentUserId = userId;
        var row = new WorkflowProfileVersion
        {
            WorkflowType = type,
            TestCode = code,
            WorkUnits = r.WorkUnits,
            TargetAnalystDays = r.TargetAnalystDays,
            EffectiveFrom = r.EffectiveFrom,
            // Inserted as Draft: the append-only trigger (Task 6) rejects any UPDATE of an Effective row,
            // and attaching the signature is an update. Draft -> Effective is the one permitted mutation.
            Status = WorkflowProfileStatus.Draft,
            ReasonForChange = reason,
            CreatedByUserId = userId,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };
        _db.WorkflowProfileVersions.Add(row);
        await _db.SaveChangesAsync(ct); // the signature needs the row id; the transaction undoes this on a bad password

        row.ElectronicSignature = await _sig.SignAsync(
            userId, r.Password, SignatureMeaning.MasterDataChanged, ReviewEntityTypes.WorkflowProfile, row.Id,
            SignatureComment(type, code, r.WorkUnits, r.TargetAnalystDays, r.EffectiveFrom, reason), ip);
        row.Status = WorkflowProfileStatus.Effective; // flipped before the audit event's save, in the same update

        await _audit.RecordUserEventAsync(
            VersionAddedActionCode, AuditActionCategory.Configuration, ReviewEntityTypes.WorkflowProfile,
            reason: reason, changes: Changes(null, null, null, row), entityId: row.Id.ToString(),
            cancellationToken: ct);

        return await ToDtoAsync(row, ct);
    }

    public Task<WorkflowProfileVersionDto> SignDraftAsync(
        int id, SignWorkflowProfileDraftRequest r, int userId, string? ip, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => SignDraftCoreAsync(id, r, userId, ip, ct));

    private async Task<WorkflowProfileVersionDto> SignDraftCoreAsync(
        int id, SignWorkflowProfileDraftRequest r, int userId, string? ip, CancellationToken ct)
    {
        var draft = await _db.WorkflowProfileVersions.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException($"Workflow profile version {id} was not found.");
        if (draft.Status != WorkflowProfileStatus.Draft)
            throw new InvalidOperationException("This version is already Effective and cannot be signed again.");
        RecordVersion.EnsureCurrent(_db, draft);

        var (type, code, reason) = await ValidateAsync(
            draft.WorkflowType, draft.TestCode, r.WorkUnits, r.TargetAnalystDays, r.ReasonForChange, ct);
        await EnsureEffectiveFromAsync(type, code, r.EffectiveFrom, ct);

        var previousUnits = draft.WorkUnits;
        var previousTarget = draft.TargetAnalystDays;
        var previousFrom = draft.EffectiveFrom;

        // Sign first: a wrong password throws before any field of the draft changes.
        _db.CurrentUserId = userId;
        var signature = await _sig.SignAsync(
            userId, r.Password, SignatureMeaning.MasterDataChanged, ReviewEntityTypes.WorkflowProfile, draft.Id,
            SignatureComment(type, code, r.WorkUnits, r.TargetAnalystDays, r.EffectiveFrom, reason), ip);

        draft.WorkflowType = type;
        draft.WorkUnits = r.WorkUnits;
        draft.TargetAnalystDays = r.TargetAnalystDays;
        draft.EffectiveFrom = r.EffectiveFrom;
        draft.ReasonForChange = reason;
        draft.Status = WorkflowProfileStatus.Effective;
        draft.ElectronicSignature = signature;

        await _audit.RecordUserEventAsync(
            DraftSignedActionCode, AuditActionCategory.Configuration, ReviewEntityTypes.WorkflowProfile,
            reason: reason, changes: Changes(previousUnits, previousTarget, previousFrom, draft),
            entityId: draft.Id.ToString(), cancellationToken: ct);

        return await ToDtoAsync(draft, ct);
    }

    // ---- Validation ------------------------------------------------------

    private async Task<(WorkflowType Type, string? TestCode, string Reason)> ValidateAsync(
        WorkflowType requestedType, string? testCode, decimal? units, decimal? target, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for the change is required.");
        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > 500)
            throw new InvalidOperationException("The reason must be 500 characters or fewer.");

        if (units is null && target is null)
            throw new InvalidOperationException("Enter work units, a target, or both.");
        CheckValue("Work units", units, MaxWorkUnits);
        CheckValue("Target analyst days", target, MaxTargetDays);

        var code = string.IsNullOrWhiteSpace(testCode) ? null : testCode.Trim();
        var type = requestedType;
        if (code is not null)
        {
            var definition = await _db.TestDefinitions.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code, ct)
                ?? throw new InvalidOperationException($"Test '{code}' does not exist in the test master.");
            type = definition.WorkflowType; // the definition decides, not the request
        }

        if (!LiveTypes.Contains(type))
            throw new InvalidOperationException($"Workflow type {type} is retired and cannot carry a profile.");

        return (type, code, trimmedReason);
    }

    private static void CheckValue(string label, decimal? value, decimal max)
    {
        if (value is null) return;
        if (value <= 0) throw new InvalidOperationException($"{label} must be greater than 0.");
        if (value > max) throw new InvalidOperationException($"{label} must not exceed {max.ToString(CultureInfo.InvariantCulture)}.");
        if (decimal.Round(value.Value, 2) != value.Value)
            throw new InvalidOperationException($"{label} allows at most 2 decimal places.");
    }

    private async Task EnsureEffectiveFromAsync(WorkflowType type, string? code, DateOnly effectiveFrom, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        if (effectiveFrom >= today) return;

        // Baseline exception: the first Effective version of a scope may reach back, once.
        // ponytail: no lock between this check and the insert; the UnitOfWork transaction is
        // enough for one Section Head, add a unique partial index if concurrent baselines appear.
        var scopeHasEffective = await _db.WorkflowProfileVersions.AnyAsync(
            v => v.Status == WorkflowProfileStatus.Effective && v.WorkflowType == type && v.TestCode == code, ct);
        if (scopeHasEffective)
            throw new InvalidOperationException(
                $"The effective date cannot be in the past ({effectiveFrom:yyyy-MM-dd}). Only the first version of a profile may be back-dated; " +
                $"add this version with an effective date of {today:yyyy-MM-dd} or later.");
    }

    // ---- Mapping ---------------------------------------------------------

    private static string ScopeLabel(WorkflowType type, string? code) =>
        code is null ? $"{type} type default" : $"Test {code} override";

    private static string SignatureComment(WorkflowType type, string? code, decimal? units, decimal? target, DateOnly from, string reason) =>
        $"{ScopeLabel(type, code)}: units {Fmt(units) ?? "inherit"}, target days {Fmt(target) ?? "inherit"}, effective {from:yyyy-MM-dd}: {reason}";

    private static string? Fmt(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static List<AuditFieldChange> Changes(decimal? previousUnits, decimal? previousTarget, DateOnly? previousFrom, WorkflowProfileVersion now) => new()
    {
        new("Scope", null, ScopeLabel(now.WorkflowType, now.TestCode)),
        new("WorkUnits", Fmt(previousUnits), Fmt(now.WorkUnits)),
        new("TargetAnalystDays", Fmt(previousTarget), Fmt(now.TargetAnalystDays)),
        new("EffectiveFrom", previousFrom?.ToString("yyyy-MM-dd"), now.EffectiveFrom.ToString("yyyy-MM-dd"))
    };

    private async Task<WorkflowProfileVersionDto> ToDtoAsync(WorkflowProfileVersion row, CancellationToken ct) =>
        (await MapAsync(new[] { row }, ct))[0];

    private async Task<List<WorkflowProfileVersionDto>> MapAsync(IReadOnlyCollection<WorkflowProfileVersion> rows, CancellationToken ct)
    {
        var codes = rows.Where(v => v.TestCode != null).Select(v => v.TestCode!).Distinct().ToList();
        var testNames = await _db.TestDefinitions.AsNoTracking().Where(t => codes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code, t => t.DisplayName, ct);
        var creatorIds = rows.Select(v => v.CreatedByUserId).Distinct().ToList();
        var creators = await _db.Users.AsNoTracking().Where(u => creatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var signatures = await LoadSignaturesAsync(rows, ct);

        return rows.Select(v =>
        {
            string? testName = null;
            if (v.TestCode is not null) testNames.TryGetValue(v.TestCode, out testName);
            DateTime? signedAt = null;
            if (v.ElectronicSignatureId is int sid && signatures.TryGetValue(sid, out var s)) signedAt = s.SignedAt;
            return new WorkflowProfileVersionDto(
                v.Id, v.WorkflowType, v.TestCode, testName, v.WorkUnits, v.TargetAnalystDays, v.EffectiveFrom,
                v.Status, v.ReasonForChange, v.ElectronicSignatureId, v.CreatedByUserId,
                CreatorName(v.CreatedByUserId, creators), v.CreatedAt, signedAt, v.Version);
        }).ToList();
    }

    private static string CreatorName(int userId, IReadOnlyDictionary<int, string> users) =>
        userId == 0 ? "System (legacy import)" : users.TryGetValue(userId, out var name) ? name : "Unknown user";

    private async Task<Dictionary<int, (DateTime SignedAt, string SignerName)>> LoadSignaturesAsync(
        IEnumerable<WorkflowProfileVersion> rows, CancellationToken ct)
    {
        var ids = rows.Where(v => v.ElectronicSignatureId.HasValue).Select(v => v.ElectronicSignatureId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, (DateTime, string)>();
        return await _db.ElectronicSignatures.AsNoTracking().Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (SignedAt: s.SignedAt, SignerName: s.UserFullNameSnapshot), ct);
    }
}
