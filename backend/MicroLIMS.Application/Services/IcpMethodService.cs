using System.Text.Json;
using System.Text.RegularExpressions;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record IcpElementInput(int? Id, string Symbol, decimal WavelengthNm, AnalyteView View, decimal ConversionFactor = 1m);

public record SaveIcpMethodRequest(
    string Name, string Abbreviation, DateTime EffectiveDate, IcpMethodMode Mode,
    string StandardLevelsMgPerL, int CalibrationStandardEntryId, decimal MinCorrelation,
    decimal SampleVolumeMl, List<IcpElementInput> Elements,
    decimal DilutionFactor = 1m, int MaxCalibrationAgeHours = 24,
    bool RequireBlank = false, decimal? BlankMaxMgPerL = null,
    bool RequireIcv = false, int? IcvStandardEntryId = null, decimal? IcvNominalMgPerL = null,
    decimal? IcvRecoveryLowPercent = null, decimal? IcvRecoveryHighPercent = null,
    bool RequireCcv = false, decimal? CcvNominalMgPerL = null,
    decimal? CcvRecoveryLowPercent = null, decimal? CcvRecoveryHighPercent = null,
    int? SectionId = null, string? Reason = null);   // Reason required on update, ignored on create

public record IcpMethodListItem(int Id, string Name, string Abbreviation, IcpMethodMode Mode, bool IsActive,
    int ElementCount, string SectionName, DateTime LastModifiedAt);
public record IcpMethodHistoryEntry(DateTime At, string UserName, string Action, string? Reason, string? BeforeJson, string? AfterJson);

// ICP-OES method master (I1, spec 2026-10-03 section 4.1). Same lifecycle and
// audit shape as HplcMethodService: edit in place with a required reason, one
// audit event per change whose single change is the whole method as JSON.
public class IcpMethodService
{
    private const string CreatedActionCode = "IcpMethod.Created";
    private const string UpdatedActionCode = "IcpMethod.Updated";
    private const string DeactivatedActionCode = "IcpMethod.Deactivated";
    private const string ActivatedActionCode = "IcpMethod.Activated";
    private const string RecordType = "IcpMethod";

    private static readonly JsonSerializerOptions JsonOptions = SnapshotJson.Options;

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IAuditEventService _auditEventService;
    private readonly TimeProvider _time;

    public IcpMethodService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        IAuditEventService auditEventService,
        TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _scope = scope;
        _auditEventService = auditEventService;
    }

    public async Task<List<IcpMethodListItem>> GetAllAsync(int currentUserId, bool activeOnly = false, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        var query = _db.IcpMethods.Include(m => m.Section).Include(m => m.Elements).AsNoTracking().AsQueryable();

        if (scope != null)
            query = query.Where(m => scope.Contains(m.SectionId));
        if (activeOnly)
            query = query.Where(m => m.IsActive);

        return (await query.OrderBy(m => m.Name).ToListAsync(ct))
            .Select(m => new IcpMethodListItem(m.Id, m.Name, m.Abbreviation, m.Mode, m.IsActive, m.Elements.Count, m.Section?.Name ?? string.Empty, m.LastModifiedAt))
            .ToList();
    }

    public async Task<IcpMethodResponse> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        var method = await QueryWithIncludes().AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"ICP method {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(method.SectionId))
            throw new NotFoundException($"ICP method {id} not found.");

        return IcpMethodResponse.From(method);
    }

    public async Task<IcpMethodResponse> CreateAsync(SaveIcpMethodRequest r, int currentUserId, CancellationToken ct = default)
    {
        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId, ct);

        var (abbr, levels) = await ValidateAsync(r, sectionId, null, null, ct);

        if (await _db.IcpMethods.AnyAsync(m => m.SectionId == sectionId && m.Abbreviation == abbr, ct))
            throw new InvalidOperationException($"An ICP method abbreviated \"{abbr}\" already exists.");

        var now = _time.GetUtcNow().UtcDateTime;
        var method = new IcpMethod
        {
            SectionId = sectionId,
            IsActive = true,
            Mode = r.Mode,
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = now,
        };
        ApplyFields(method, r, abbr, levels);
        ApplyElements(method, r);

        _db.IcpMethods.Add(method);
        await _db.SaveChangesAsync(ct);

        var response = await GetByIdAsync(method.Id, currentUserId, ct);
        var after = JsonSerializer.Serialize(response, JsonOptions);

        await _auditEventService.RecordUserEventAsync(
            CreatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            entityId: method.Id.ToString(),
            changes: new[] { new AuditFieldChange("Method", null, after) },
            cancellationToken: ct);

        return response;
    }

    public async Task<IcpMethodResponse> UpdateAsync(int id, SaveIcpMethodRequest r, int currentUserId, CancellationToken ct = default)
    {
        var reason = ValidateReason(r.Reason);

        var before = JsonSerializer.Serialize(await GetByIdAsync(id, currentUserId, ct), JsonOptions);

        var method = await _db.IcpMethods
            .Include(m => m.Elements)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"ICP method {id} not found.");

        RecordVersion.EnsureCurrent(_db, method);

        if (r.Mode != method.Mode)
            throw new InvalidOperationException("The mode can't be changed after the method is created.");

        // A standard the method already points to may stay even if since deactivated.
        var alreadyUsedStandardIds = new HashSet<int> { method.CalibrationStandardEntryId };
        if (method.IcvStandardEntryId.HasValue)
            alreadyUsedStandardIds.Add(method.IcvStandardEntryId.Value);

        var existingElementIds = method.Elements.Select(e => e.Id).ToHashSet();
        var (abbr, levels) = await ValidateAsync(r, method.SectionId, alreadyUsedStandardIds, existingElementIds, ct);

        if (await _db.IcpMethods.AnyAsync(m => m.SectionId == method.SectionId && m.Abbreviation == abbr && m.Id != id, ct))
            throw new InvalidOperationException($"An ICP method abbreviated \"{abbr}\" already exists.");

        // Never orphan a specification row - refuse before any child is touched.
        var incomingIds = r.Elements.Where(e => e.Id.HasValue).Select(e => e.Id!.Value).ToHashSet();
        foreach (var dropped in method.Elements.Where(e => !incomingIds.Contains(e.Id)))
        {
            if (await _db.Specifications.AnyAsync(s => s.IcpMethodElementId == dropped.Id, ct))
                throw new InvalidOperationException($"Element \"{dropped.Symbol}\" is used by specifications; it can't be removed.");
        }

        ApplyFields(method, r, abbr, levels);
        ApplyElements(method, r);
        method.LastModifiedByUserId = currentUserId;
        method.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);

        var response = await GetByIdAsync(id, currentUserId, ct);
        var after = JsonSerializer.Serialize(response, JsonOptions);

        await _auditEventService.RecordUserEventAsync(
            UpdatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            reason: reason,
            entityId: id.ToString(),
            changes: new[] { new AuditFieldChange("Method", before, after) },
            cancellationToken: ct);

        return response;
    }

    public async Task<IcpMethodResponse> SetActiveAsync(int id, bool isActive, string reason, int currentUserId, CancellationToken ct = default)
    {
        var trimmedReason = ValidateReason(reason);

        var before = JsonSerializer.Serialize(await GetByIdAsync(id, currentUserId, ct), JsonOptions);

        var method = await _db.IcpMethods.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"ICP method {id} not found.");

        method.IsActive = isActive;
        method.LastModifiedByUserId = currentUserId;
        method.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);

        var response = await GetByIdAsync(id, currentUserId, ct);
        var after = JsonSerializer.Serialize(response, JsonOptions);

        await _auditEventService.RecordUserEventAsync(
            isActive ? ActivatedActionCode : DeactivatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            reason: trimmedReason,
            entityId: id.ToString(),
            changes: new[] { new AuditFieldChange("Method", before, after) },
            cancellationToken: ct);

        return response;
    }

    public async Task<List<IcpMethodHistoryEntry>> GetHistoryAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        var method = await _db.IcpMethods.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"ICP method {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(method.SectionId))
            throw new NotFoundException($"ICP method {id} not found.");

        var logs = await _db.AuditLogs
            .Include(a => a.Changes)
            .Where(a => a.EntityName == RecordType && a.EntityId == id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(ct);

        var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var userNames = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return logs.Select(l =>
        {
            var change = l.Changes.FirstOrDefault();
            var userName = l.UserId.HasValue && userNames.TryGetValue(l.UserId.Value, out var name) ? name : "Unknown";
            return new IcpMethodHistoryEntry(l.Timestamp, userName, l.ActionCode ?? l.Action, l.Reason, change?.PreviousValue, change?.NewValue);
        }).ToList();
    }

    private IQueryable<IcpMethod> QueryWithIncludes() =>
        _db.IcpMethods
            .Include(m => m.Section)
            .Include(m => m.CalibrationStandardEntry)
            .Include(m => m.IcvStandardEntry)
            .Include(m => m.Elements);

    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required.");
        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
            throw new InvalidOperationException("Reason cannot exceed 500 characters.");
        return trimmed;
    }

    // "zn" / "ZN" -> "Zn"
    private static string NormaliseSymbol(string symbol)
    {
        var s = symbol.Trim();
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }

    private static void ApplyFields(IcpMethod method, SaveIcpMethodRequest r, string abbreviation, string normalisedLevels)
    {
        method.Name = r.Name.Trim();
        method.Abbreviation = abbreviation;
        method.EffectiveDate = r.EffectiveDate;
        method.StandardLevelsMgPerL = normalisedLevels;
        method.CalibrationStandardEntryId = r.CalibrationStandardEntryId;
        method.MinCorrelation = r.MinCorrelation;
        method.MaxCalibrationAgeHours = r.MaxCalibrationAgeHours;
        method.SampleVolumeMl = r.SampleVolumeMl;
        method.DilutionFactor = r.DilutionFactor;
        method.RequireBlank = r.RequireBlank;
        method.BlankMaxMgPerL = r.BlankMaxMgPerL;
        method.RequireIcv = r.RequireIcv;
        method.IcvStandardEntryId = r.IcvStandardEntryId;
        method.IcvNominalMgPerL = r.IcvNominalMgPerL;
        method.IcvRecoveryLowPercent = r.IcvRecoveryLowPercent;
        method.IcvRecoveryHighPercent = r.IcvRecoveryHighPercent;
        method.RequireCcv = r.RequireCcv;
        method.CcvNominalMgPerL = r.CcvNominalMgPerL;
        method.CcvRecoveryLowPercent = r.CcvRecoveryLowPercent;
        method.CcvRecoveryHighPercent = r.CcvRecoveryHighPercent;
    }

    // Matched by Id so unchanged elements keep their Id (specification rows
    // stay linked); rows without an Id are added; rows missing from the
    // request are removed (the caller has already refused this when a
    // specification references them).
    private void ApplyElements(IcpMethod method, SaveIcpMethodRequest r)
    {
        var existingById = method.Elements.Where(e => e.Id != 0).ToDictionary(e => e.Id);
        var keepIds = new HashSet<int>();
        var order = 1;
        foreach (var input in r.Elements)
        {
            IcpMethodElement element;
            if (input.Id.HasValue && existingById.TryGetValue(input.Id.Value, out var existing))
            {
                element = existing;
                keepIds.Add(existing.Id);
            }
            else
            {
                element = new IcpMethodElement();
                method.Elements.Add(element);
            }

            element.DisplayOrder = order++;
            element.Symbol = NormaliseSymbol(input.Symbol);
            element.WavelengthNm = input.WavelengthNm;
            element.View = input.View;
            element.ConversionFactor = input.ConversionFactor;
        }

        var toRemove = method.Elements.Where(e => e.Id != 0 && !keepIds.Contains(e.Id)).ToList();
        if (toRemove.Count > 0)
        {
            _db.IcpMethodElements.RemoveRange(toRemove);
            foreach (var rem in toRemove)
                method.Elements.Remove(rem);
        }
    }

    private async Task<(string Abbreviation, string Levels)> ValidateAsync(
        SaveIcpMethodRequest r, int sectionId,
        HashSet<int>? alreadyUsedStandardIds, HashSet<int>? existingElementIds,
        CancellationToken ct)
    {
        alreadyUsedStandardIds ??= new HashSet<int>();
        existingElementIds ??= new HashSet<int>();

        if (string.IsNullOrWhiteSpace(r.Name))
            throw new InvalidOperationException("Method name is required.");
        if (r.Name.Trim().Length > 200)
            throw new InvalidOperationException("Method name cannot exceed 200 characters.");

        if (string.IsNullOrWhiteSpace(r.Abbreviation))
            throw new InvalidOperationException("Abbreviation is required.");
        var abbr = r.Abbreviation.Trim().ToUpperInvariant();
        if (abbr.Length < 2 || abbr.Length > 20 || !Regex.IsMatch(abbr, "^[A-Z0-9-]+$"))
            throw new InvalidOperationException("Abbreviation must be 2-20 uppercase letters, digits or hyphens.");

        var (_, levels) = CalibrationStandardLevelsHelper.ParseAndValidate(r.StandardLevelsMgPerL);

        if (r.MinCorrelation <= 0m || r.MinCorrelation > 1m)
            throw new InvalidOperationException("Minimum correlation must be greater than 0 and at most 1.");
        if (r.SampleVolumeMl <= 0m)
            throw new InvalidOperationException("Sample volume must be greater than zero.");
        if (r.DilutionFactor < 1m)
            throw new InvalidOperationException("Dilution factor must be 1 or more.");
        if (r.MaxCalibrationAgeHours < 1 || r.MaxCalibrationAgeHours > 168)
            throw new InvalidOperationException("Calibration age must be between 1 and 168 hours.");

        await CheckStandardAsync(r.CalibrationStandardEntryId, "Calibration standard", sectionId, alreadyUsedStandardIds, ct);

        if (r.RequireBlank && !(r.BlankMaxMgPerL > 0m))
            throw new InvalidOperationException("Blank limit (mg/L) is required when the blank check is on.");

        if (r.RequireIcv)
        {
            if (!r.IcvStandardEntryId.HasValue || !(r.IcvNominalMgPerL > 0m)
                || !(r.IcvRecoveryLowPercent > 0m) || !(r.IcvRecoveryHighPercent > 0m)
                || r.IcvRecoveryLowPercent >= r.IcvRecoveryHighPercent)
                throw new InvalidOperationException("ICV standard, nominal (mg/L) and recovery limits are required when the ICV check is on.");
            if (r.IcvStandardEntryId.Value == r.CalibrationStandardEntryId)
                throw new InvalidOperationException("The ICV standard must be a second source, not the calibration standard.");
            await CheckStandardAsync(r.IcvStandardEntryId.Value, "ICV standard", sectionId, alreadyUsedStandardIds, ct);
        }

        if (r.RequireCcv
            && (!(r.CcvNominalMgPerL > 0m) || !(r.CcvRecoveryLowPercent > 0m) || !(r.CcvRecoveryHighPercent > 0m)
                || r.CcvRecoveryLowPercent >= r.CcvRecoveryHighPercent))
            throw new InvalidOperationException("CCV nominal (mg/L) and recovery limits are required when the CCV check is on.");

        if ((!r.RequireBlank && r.BlankMaxMgPerL.HasValue)
            || (!r.RequireIcv && (r.IcvStandardEntryId.HasValue || r.IcvNominalMgPerL.HasValue
                || r.IcvRecoveryLowPercent.HasValue || r.IcvRecoveryHighPercent.HasValue))
            || (!r.RequireCcv && (r.CcvNominalMgPerL.HasValue
                || r.CcvRecoveryLowPercent.HasValue || r.CcvRecoveryHighPercent.HasValue)))
            throw new InvalidOperationException("Blank, ICV and CCV values are only used when that check is on.");

        if (r.Elements.Count < 1)
            throw new InvalidOperationException("At least one element is required.");

        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in r.Elements)
        {
            var raw = e.Symbol?.Trim() ?? string.Empty;
            if (raw.Length < 1 || raw.Length > 3)
                throw new InvalidOperationException("Element symbol must be 1-3 letters.");
            var symbol = NormaliseSymbol(raw);
            if (!symbols.Add(symbol))
                throw new InvalidOperationException($"Element \"{symbol}\" is listed more than once.");
            if (e.WavelengthNm <= 0m)
                throw new InvalidOperationException($"{symbol}: wavelength must be greater than zero.");
            if (e.ConversionFactor <= 0m)
                throw new InvalidOperationException($"{symbol}: conversion factor must be greater than zero.");
            if (e.Id.HasValue && !existingElementIds.Contains(e.Id.Value))
                throw new InvalidOperationException($"Element {e.Id.Value} does not belong to this method.");
        }

        return (abbr, levels);
    }

    private async Task CheckStandardAsync(int entryId, string label, int sectionId, HashSet<int> alreadyUsedIds, CancellationToken ct)
    {
        var entry = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct)
            ?? throw new InvalidOperationException($"{label} not found.");
        if (entry.SectionId != sectionId)
            throw new InvalidOperationException($"{label} \"{entry.Code}\" belongs to another laboratory.");
        if (entry.Category != MaterialMasterCategory.ReferenceStandard)
            throw new InvalidOperationException($"{label} \"{entry.Code}\" must be a reference standard.");
        if (!entry.IsActive && !alreadyUsedIds.Contains(entry.Id))
            throw new InvalidOperationException($"{label} \"{entry.Code}\" is inactive.");
    }
}
