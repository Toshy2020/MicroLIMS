using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record SaveReferenceOrganismRequest(
    string Code, int OrganismId, CultureCollection CultureCollection, string? StrainNumber, int? OwnerSectionId = null);

// Reference organism master (Master Testing Materials phase 3): reference
// strains (culture collection + strain number) under an Organism species.
// Lyophilized stock lots and cryovials reference a strain; the species list
// (Organism) stays the unit for media challenge specs and test-step targets.
//
// App-wide: every lab sees and uses every strain, only the owner lab edits it.
// Codes are unique across the app and start with ORG- (enforced on create and
// on code change). Once a stock lot references a strain its code and identity
// are frozen: the code changes only through ChangeCodeAsync (reason +
// electronic signature) and a wrong strain is deactivated, not edited or deleted.
public class ReferenceOrganismService
{
    public const string CodeChangedActionCode = "ReferenceOrganismCodeChanged";

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IElectronicSignatureService _signatureService;
    private readonly IAuditEventService _auditEventService;
    private readonly TimeProvider _time;

    public ReferenceOrganismService(
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

    public async Task<List<ReferenceOrganismResponse>> GetAllAsync(int currentUserId, bool activeOnly = false, int? organismId = null, CancellationToken ct = default)
    {
        // Every lab sees every strain; the owner lab (configurable scope) decides CanEdit.
        var query = _db.ReferenceOrganisms.AsNoTracking().Include(r => r.Organism).Include(r => r.OwnerSection).AsQueryable();
        if (activeOnly) query = query.Where(r => r.IsActive);
        if (organismId.HasValue) query = query.Where(r => r.OrganismId == organismId.Value);

        var strains = await query.OrderBy(r => r.Code).ToListAsync(ct);
        var editable = await _scope.GetConfigurableSectionIdsAsync(currentUserId, ct);
        var ids = strains.Select(r => r.Id).ToList();
        var stocked = (await _db.Materials
            .Where(m => m.ReferenceOrganismId != null && ids.Contains(m.ReferenceOrganismId.Value))
            .Select(m => m.ReferenceOrganismId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

        return strains
            .Select(r => ReferenceOrganismResponse.From(r, editable == null || editable.Contains(r.OwnerSectionId), stocked.Contains(r.Id)))
            .ToList();
    }

    private async Task<ReferenceOrganismResponse> ToResponseAsync(ReferenceOrganism strain, CancellationToken ct)
    {
        // Owner-lab access was already checked by LoadWithAccessAsync / ResolveSectionForCreateAsync.
        strain.Organism ??= await _db.Organisms.AsNoTracking().FirstOrDefaultAsync(o => o.Id == strain.OrganismId, ct);
        strain.OwnerSection ??= await _db.DocumentSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == strain.OwnerSectionId, ct);
        return ReferenceOrganismResponse.From(strain, canEdit: true, await HasStockAsync(strain.Id, ct));
    }

    public async Task<ReferenceOrganismResponse> CreateAsync(SaveReferenceOrganismRequest r, int currentUserId, CancellationToken ct = default)
    {
        var strainNumber = ValidateAndNormalize(r, out var normalizedCode);
        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.OwnerSectionId, ct, SectionAccess.Configure);
        var organism = await _db.Organisms.FirstOrDefaultAsync(o => o.Id == r.OrganismId, ct)
            ?? throw new InvalidOperationException($"Organism {r.OrganismId} not found.");

        await EnsureUniqueAsync(normalizedCode, r.CultureCollection, strainNumber, excludeId: null, ct);

        var now = _time.GetUtcNow().UtcDateTime;
        var strain = new ReferenceOrganism
        {
            Code = normalizedCode,
            OrganismId = organism.Id,
            Organism = organism,
            CultureCollection = r.CultureCollection,
            StrainNumber = strainNumber,
            OwnerSectionId = sectionId,
            IsActive = true,
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = now
        };
        _db.ReferenceOrganisms.Add(strain);
        await _db.SaveChangesAsync(ct);
        return await ToResponseAsync(strain, ct);
    }

    public async Task<ReferenceOrganismResponse> UpdateAsync(int id, SaveReferenceOrganismRequest r, int currentUserId, CancellationToken ct = default)
    {
        var strainNumber = ValidateAndNormalize(r, out var normalizedCode);
        var strain = await LoadWithAccessAsync(id, currentUserId, ct);
        RecordVersion.EnsureCurrent(_db, strain);

        var hasStock = await HasStockAsync(id, ct);
        if (hasStock)
        {
            if (normalizedCode != strain.Code)
                throw new InvalidOperationException("This reference organism has stock; change its code with a reason and an electronic signature.");
            if (r.OrganismId != strain.OrganismId || r.CultureCollection != strain.CultureCollection
                || !string.Equals(strainNumber, strain.StrainNumber, StringComparison.Ordinal))
                throw new InvalidOperationException("This reference organism has stock lots; its organism, culture collection and strain number can't change. Deactivate it and add the correct strain.");
        }

        var organism = r.OrganismId == strain.OrganismId
            ? strain.Organism ?? await _db.Organisms.FirstAsync(o => o.Id == strain.OrganismId, ct)
            : await _db.Organisms.FirstOrDefaultAsync(o => o.Id == r.OrganismId, ct)
                ?? throw new InvalidOperationException($"Organism {r.OrganismId} not found.");

        await EnsureUniqueAsync(normalizedCode, r.CultureCollection, strainNumber, excludeId: id, ct);

        strain.Code = normalizedCode;
        strain.OrganismId = organism.Id;
        strain.Organism = organism;
        strain.CultureCollection = r.CultureCollection;
        strain.StrainNumber = strainNumber;
        strain.LastModifiedByUserId = currentUserId;
        strain.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return await ToResponseAsync(strain, ct);
    }

    public Task<ReferenceOrganismResponse> ChangeCodeAsync(
        int id, string newCode, string reason, string password, int currentUserId, string? ipAddress, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, async () => await ToResponseAsync(
            await ChangeCodeCoreAsync(id, newCode, reason, password, currentUserId, ipAddress, ct), ct));

    private async Task<ReferenceOrganism> ChangeCodeCoreAsync(
        int id, string newCode, string reason, string password, int currentUserId, string? ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for the code change is required.");
        var trimmedReason = reason.Trim();

        if (string.IsNullOrWhiteSpace(newCode)) throw new InvalidOperationException("Code is required.");
        var normalizedCode = newCode.Trim().ToUpper();

        var strain = await LoadWithAccessAsync(id, currentUserId, ct);

        if (normalizedCode == strain.Code)
            throw new InvalidOperationException("The new code must be different from the current code.");
        EnsurePrefix(normalizedCode);
        if (await _db.ReferenceOrganisms.AnyAsync(e => e.Code == normalizedCode && e.Id != id, ct))
            throw new InvalidOperationException($"Reference organism code \"{normalizedCode}\" already exists.");

        var oldCode = strain.Code;

        // Signs first - a wrong password throws SignatureVerificationException and
        // nothing may change (same ordering as MaterialMasterService.ChangeCodeAsync).
        _db.CurrentUserId = currentUserId;
        await _signatureService.SignAsync(
            currentUserId,
            password,
            SignatureMeaning.MasterDataChanged,
            ReviewEntityTypes.ReferenceOrganism,
            id,
            $"Code {oldCode} -> {normalizedCode}: {trimmedReason}",
            ipAddress);

        strain.Code = normalizedCode;
        strain.LastModifiedByUserId = currentUserId;
        strain.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        // Received lots keep the Material.Code snapshot and cryovials their codes.
        await _auditEventService.RecordUserEventAsync(
            CodeChangedActionCode,
            AuditActionCategory.Configuration,
            ReviewEntityTypes.ReferenceOrganism,
            reason: trimmedReason,
            changes: new[] { new AuditFieldChange("Code", oldCode, normalizedCode) },
            entityId: id.ToString());

        return strain;
    }

    public async Task<ReferenceOrganismResponse> SetActiveAsync(int id, bool isActive, int currentUserId, CancellationToken ct = default)
    {
        var strain = await LoadWithAccessAsync(id, currentUserId, ct);

        strain.IsActive = isActive;
        strain.LastModifiedByUserId = currentUserId;
        strain.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return await ToResponseAsync(strain, ct);
    }

    // For stock receipt: the strain must exist and be active (allowInactive only when the lot already points at it).
    public async Task<ReferenceOrganism> ResolveForLotAsync(int id, bool allowInactive, CancellationToken ct = default)
    {
        var strain = await _db.ReferenceOrganisms.Include(r => r.Organism).FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException($"Reference organism {id} not found.");
        if (!strain.IsActive && !allowInactive)
            throw new InvalidOperationException($"Reference organism \"{strain.Code}\" ({ReferenceOrganismNaming.NameOf(strain)}) is inactive.");
        return strain;
    }

    private async Task<bool> HasStockAsync(int id, CancellationToken ct) =>
        await _db.Materials.AnyAsync(m => m.ReferenceOrganismId == id, ct)
        || await _db.Cryovials.AnyAsync(c => c.ReferenceOrganismId == id, ct);

    private async Task<ReferenceOrganism> LoadWithAccessAsync(int id, int currentUserId, CancellationToken ct)
    {
        var strain = await _db.ReferenceOrganisms.Include(r => r.Organism).FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Reference organism {id} not found.");

        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(strain.OwnerSectionId))
            throw new InvalidOperationException("Only the owner lab of a reference organism can change it.");

        return strain;
    }

    private async Task EnsureUniqueAsync(string code, CultureCollection collection, string? strainNumber, int? excludeId, CancellationToken ct)
    {
        if (await _db.ReferenceOrganisms.AnyAsync(e => e.Code == code && e.Id != excludeId, ct))
            throw new InvalidOperationException($"Reference organism code \"{code}\" already exists.");

        if (strainNumber != null)
        {
            var upper = strainNumber.ToUpper();
            var clash = await _db.ReferenceOrganisms.FirstOrDefaultAsync(
                e => e.CultureCollection == collection && e.StrainNumber != null && e.StrainNumber.ToUpper() == upper && e.Id != excludeId, ct);
            if (clash != null)
                throw new InvalidOperationException($"{collection} {strainNumber} is already listed as {clash.Code}.");
        }
    }

    private static string? ValidateAndNormalize(SaveReferenceOrganismRequest r, out string normalizedCode)
    {
        if (string.IsNullOrWhiteSpace(r.Code)) throw new InvalidOperationException("Code is required.");
        normalizedCode = r.Code.Trim().ToUpper();
        EnsurePrefix(normalizedCode);
        if (!Enum.IsDefined(r.CultureCollection)) throw new InvalidOperationException("Choose the culture collection.");

        var strainNumber = string.IsNullOrWhiteSpace(r.StrainNumber) ? null : r.StrainNumber.Trim();
        if (strainNumber == null && r.CultureCollection != CultureCollection.Other)
            throw new InvalidOperationException("Strain number is required unless the culture collection is Other.");
        if (strainNumber is { Length: > 50 }) throw new InvalidOperationException("Strain number can be at most 50 characters.");
        return strainNumber;
    }

    private static void EnsurePrefix(string normalizedCode)
    {
        var prefix = ReferenceOrganismNaming.CodePrefix;
        if (!normalizedCode.StartsWith(prefix, StringComparison.Ordinal) || normalizedCode.Length == prefix.Length)
            throw new InvalidOperationException($"Reference organism codes must start with \"{prefix}\".");
        if (normalizedCode.Length > 50) throw new InvalidOperationException("Code can be at most 50 characters.");
    }
}
