using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record SaveMaterialMasterEntryRequest(
    string Code, string Name, MaterialMasterCategory Category, string? Grade, string? Source,
    MaterialUnit BaseUnit, int? SectionId = null,
    string? WorkingConcentration = null, string? Solvent = null,
    decimal? TransitionRangeFrom = null, decimal? TransitionRangeTo = null,
    string? ColourChange = null, string? IndicatorUse = null);

// Reagent / Indicator / Reference Standard master (HPLC chain S1, spec 3.1).
// Chemical stock lots (Material) reference an entry here; lots snapshot the
// entry's name/code at receipt so a later edit here doesn't retroactively
// change a lot's record.
//
// App-wide: every lab sees and uses every entry, only the owner lab (SectionId)
// edits it. Codes are unique across the app and carry a category prefix
// (enforced on create and on code change; older codes are grandfathered).
// Once an entry has stock its code is frozen and changes only through
// ChangeCodeAsync (reason + electronic signature).
public class MaterialMasterService
{
    public const string CodeChangedActionCode = "MaterialMasterEntryCodeChanged";

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IElectronicSignatureService _signatureService;
    private readonly IAuditEventService _auditEventService;
    private readonly TimeProvider _time;

    public MaterialMasterService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        IElectronicSignatureService signatureService,
        IAuditEventService auditEventService,
        TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _scope = scope;
        _signatureService = signatureService;
        _auditEventService = auditEventService;
    }

    public static string PrefixFor(MaterialMasterCategory category) => category switch
    {
        MaterialMasterCategory.Indicator => "IND-",
        MaterialMasterCategory.ReferenceStandard => "RS-",
        MaterialMasterCategory.PrimaryStandard => "PS-",
        _ => "RG-"
    };

    public async Task<List<MaterialMasterEntryResponse>> GetAllAsync(int currentUserId, MaterialMasterCategory? category = null, bool activeOnly = false, CancellationToken ct = default)
    {
        // Every lab sees every entry; the owner lab (configurable scope) decides CanEdit.
        var query = _db.MaterialMasterEntries.AsNoTracking().Include(e => e.Section).AsQueryable();

        if (category.HasValue)
        {
            query = query.Where(e => e.Category == category.Value);
        }

        if (activeOnly)
        {
            query = query.Where(e => e.IsActive);
        }

        var entries = await query.OrderBy(e => e.Code).ToListAsync(ct);
        var editable = await _scope.GetConfigurableSectionIdsAsync(currentUserId, ct);
        var ids = entries.Select(e => e.Id).ToList();
        var stocked = (await _db.Materials
            .Where(m => m.MaterialMasterEntryId != null && ids.Contains(m.MaterialMasterEntryId.Value))
            .Select(m => m.MaterialMasterEntryId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

        return entries
            .Select(e => MaterialMasterEntryResponse.From(e, editable == null || editable.Contains(e.SectionId), stocked.Contains(e.Id)))
            .ToList();
    }

    private async Task<MaterialMasterEntryResponse> ToResponseAsync(MaterialMasterEntry entry, int currentUserId, CancellationToken ct)
    {
        // Owner-lab access was already checked by LoadWithAccessAsync / ResolveSectionForCreateAsync.
        entry.Section ??= await _db.DocumentSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == entry.SectionId, ct);
        var hasStock = await _db.Materials.AnyAsync(m => m.MaterialMasterEntryId == entry.Id, ct);
        return MaterialMasterEntryResponse.From(entry, canEdit: true, hasStock);
    }

    public async Task<MaterialMasterEntryResponse> CreateAsync(SaveMaterialMasterEntryRequest r, int currentUserId, CancellationToken ct = default)
    {
        Validate(r);

        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId, ct, SectionAccess.Configure);
        var normalizedCode = r.Code.Trim().ToUpper();
        EnsurePrefix(normalizedCode, r.Category);

        if (await _db.MaterialMasterEntries.AnyAsync(e => e.Code == normalizedCode, ct))
            throw new InvalidOperationException($"Material master entry code \"{normalizedCode}\" already exists.");

        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId,
            Code = normalizedCode,
            Name = r.Name.Trim(),
            Category = r.Category,
            Grade = r.Grade?.Trim(),
            Source = r.Source?.Trim(),
            BaseUnit = r.BaseUnit,
            IsActive = true,
            WorkingConcentration = r.WorkingConcentration?.Trim(),
            Solvent = r.Solvent?.Trim(),
            TransitionRangeFrom = r.TransitionRangeFrom,
            TransitionRangeTo = r.TransitionRangeTo,
            ColourChange = r.ColourChange?.Trim(),
            IndicatorUse = r.IndicatorUse?.Trim(),
            CreatedByUserId = currentUserId,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = _time.GetUtcNow().UtcDateTime
        };

        _db.MaterialMasterEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        return await ToResponseAsync(entry, currentUserId, ct);
    }

    public async Task<MaterialMasterEntryResponse> UpdateAsync(int id, SaveMaterialMasterEntryRequest r, int currentUserId, CancellationToken ct = default)
    {
        Validate(r);

        var entry = await LoadWithAccessAsync(id, currentUserId, ct);
        RecordVersion.EnsureCurrent(_db, entry);

        var normalizedCode = r.Code.Trim().ToUpper();
        var hasStock = await _db.Materials.AnyAsync(m => m.MaterialMasterEntryId == id, ct);

        // Only a changed code is checked: existing codes without a prefix are grandfathered.
        if (normalizedCode != entry.Code)
        {
            if (hasStock)
                throw new InvalidOperationException("This entry has stock; change its code with a reason and an electronic signature.");
            EnsurePrefix(normalizedCode, r.Category);
            if (await _db.MaterialMasterEntries.AnyAsync(e => e.Code == normalizedCode && e.Id != id, ct))
                throw new InvalidOperationException($"Material master entry code \"{normalizedCode}\" already exists.");
        }

        if (r.Category != entry.Category && hasStock)
            throw new InvalidOperationException("This entry has linked stock lots; its category can't change.");

        entry.Code = normalizedCode;
        entry.Name = r.Name.Trim();
        entry.Category = r.Category;
        entry.Grade = r.Grade?.Trim();
        entry.Source = r.Source?.Trim();
        entry.BaseUnit = r.BaseUnit;
        entry.WorkingConcentration = r.WorkingConcentration?.Trim();
        entry.Solvent = r.Solvent?.Trim();
        entry.TransitionRangeFrom = r.TransitionRangeFrom;
        entry.TransitionRangeTo = r.TransitionRangeTo;
        entry.ColourChange = r.ColourChange?.Trim();
        entry.IndicatorUse = r.IndicatorUse?.Trim();
        entry.LastModifiedByUserId = currentUserId;
        entry.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return await ToResponseAsync(entry, currentUserId, ct);
    }

    public Task<MaterialMasterEntryResponse> ChangeCodeAsync(
        int id, string newCode, string reason, string password, int currentUserId, string? ipAddress, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, async () => await ToResponseAsync(
            await ChangeCodeCoreAsync(id, newCode, reason, password, currentUserId, ipAddress, ct), currentUserId, ct));

    private async Task<MaterialMasterEntry> ChangeCodeCoreAsync(
        int id, string newCode, string reason, string password, int currentUserId, string? ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for the code change is required.");
        var trimmedReason = reason.Trim();

        if (string.IsNullOrWhiteSpace(newCode)) throw new InvalidOperationException("Code is required.");
        var normalizedCode = newCode.Trim().ToUpper();

        var entry = await LoadWithAccessAsync(id, currentUserId, ct);

        if (normalizedCode == entry.Code)
            throw new InvalidOperationException("The new code must be different from the current code.");
        EnsurePrefix(normalizedCode, entry.Category);
        if (await _db.MaterialMasterEntries.AnyAsync(e => e.Code == normalizedCode && e.Id != id, ct))
            throw new InvalidOperationException($"Material master entry code \"{normalizedCode}\" already exists.");

        var oldCode = entry.Code;

        // Signs first - a wrong password throws SignatureVerificationException and
        // nothing may change (same ordering as MediaProductService.ChangeCodeAsync).
        _db.CurrentUserId = currentUserId;
        await _signatureService.SignAsync(
            currentUserId,
            password,
            SignatureMeaning.MasterDataChanged,
            ReviewEntityTypes.MaterialMasterEntry,
            id,
            $"Code {oldCode} -> {normalizedCode}: {trimmedReason}",
            ipAddress);

        entry.Code = normalizedCode;
        entry.LastModifiedByUserId = currentUserId;
        entry.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        // Received lots keep the Material.Code snapshot they were received with.
        await _auditEventService.RecordUserEventAsync(
            CodeChangedActionCode,
            AuditActionCategory.Configuration,
            ReviewEntityTypes.MaterialMasterEntry,
            reason: trimmedReason,
            changes: new[] { new AuditFieldChange("Code", oldCode, normalizedCode) },
            entityId: id.ToString());

        return entry;
    }

    public async Task<MaterialMasterEntryResponse> SetActiveAsync(int id, bool isActive, int currentUserId, CancellationToken ct = default)
    {
        var entry = await LoadWithAccessAsync(id, currentUserId, ct);

        entry.IsActive = isActive;
        entry.LastModifiedByUserId = currentUserId;
        entry.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return await ToResponseAsync(entry, currentUserId, ct);
    }

    private async Task<MaterialMasterEntry> LoadWithAccessAsync(int id, int currentUserId, CancellationToken ct)
    {
        var entry = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"Material master entry {id} not found.");

        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(entry.SectionId))
            throw new InvalidOperationException("Only the owner lab of a material master entry can change it.");

        return entry;
    }

    private static void EnsurePrefix(string normalizedCode, MaterialMasterCategory category)
    {
        var prefix = PrefixFor(category);
        if (!normalizedCode.StartsWith(prefix, StringComparison.Ordinal) || normalizedCode.Length == prefix.Length)
            throw new InvalidOperationException($"{CategoryLabel(category)} codes must start with \"{prefix}\".");
    }

    private static string CategoryLabel(MaterialMasterCategory category) => category switch
    {
        MaterialMasterCategory.ReferenceStandard => "Reference standard",
        MaterialMasterCategory.PrimaryStandard => "Primary standard",
        _ => category.ToString()
    };

    private static void Validate(SaveMaterialMasterEntryRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Code)) throw new InvalidOperationException("Code is required.");
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Name is required.");
        var hasIndicatorFields = r.WorkingConcentration != null || r.Solvent != null || r.TransitionRangeFrom != null
            || r.TransitionRangeTo != null || r.ColourChange != null || r.IndicatorUse != null;
        if (hasIndicatorFields && r.Category != MaterialMasterCategory.Indicator)
            throw new InvalidOperationException("Indicator fields are only allowed for indicators.");
        if (r.TransitionRangeFrom > r.TransitionRangeTo)
            throw new InvalidOperationException("Transition range 'from' must not be above 'to'.");
    }
}
