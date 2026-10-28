using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// SectionIds empty or null = available to every section.
public record SaveMaterialCategoryRequest(
    string Name, string CodePrefix, MaterialUnit DefaultUnit, bool CoaRequired, bool ExpiryRequired,
    bool QuantityTracked, string? DefaultStorageCondition, bool ApprovalBeforeUse, bool LabelRequired,
    IReadOnlyList<int>? SectionIds, string Reason, string Password);

// Categories of the "Other Materials" group (Master Testing Materials, phase 2).
// A category carries GMP rules (COA before use, release before use, expiry,
// quantity tracking), so it is controlled more tightly than an entry: a Section
// Head creates or changes it with a reason and an electronic signature. Rule
// changes apply to lots received afterwards (the release gate is set at receipt)
// and to consumption checks from then on. Deactivated, never deleted.
public class MaterialCategoryService
{
    public const string CreatedActionCode = "MaterialCategoryCreated";
    public const string UpdatedActionCode = "MaterialCategoryUpdated";

    // Prefixes already taken by the other master groups.
    private static readonly string[] ReservedPrefixes = { "RG", "IND", "RS", "PS", "ORG", "WS" };
    private static readonly Regex PrefixPattern = new("^[A-Z]{2,5}$", RegexOptions.Compiled);

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatureService;
    private readonly IAuditEventService _auditEventService;
    private readonly TimeProvider _time;

    public MaterialCategoryService(
        IMicroLimsDbContext db,
        IElectronicSignatureService signatureService,
        IAuditEventService auditEventService,
        TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _signatureService = signatureService;
        _auditEventService = auditEventService;
    }

    // sectionId: only the categories offered to that section (those with no section rows are offered to all).
    public async Task<List<MaterialCategoryResponse>> GetAllAsync(bool activeOnly = false, int? sectionId = null, CancellationToken ct = default)
    {
        var query = _db.MaterialCategories.AsNoTracking().Include(c => c.Sections).AsQueryable();
        if (activeOnly) query = query.Where(c => c.IsActive);
        if (sectionId.HasValue)
            query = query.Where(c => !c.Sections.Any() || c.Sections.Any(s => s.SectionId == sectionId.Value));

        var categories = await query.OrderBy(c => c.Name).ToListAsync(ct);
        var used = (await _db.MaterialMasterEntries
            .Where(e => e.MaterialCategoryId != null)
            .Select(e => e.MaterialCategoryId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
        return categories.Select(c => MaterialCategoryResponse.From(c, used.Contains(c.Id))).ToList();
    }

    public Task<MaterialCategoryResponse> CreateAsync(SaveMaterialCategoryRequest r, int currentUserId, string? ipAddress, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => CreateCoreAsync(r, currentUserId, ipAddress, ct));

    private async Task<MaterialCategoryResponse> CreateCoreAsync(SaveMaterialCategoryRequest r, int currentUserId, string? ipAddress, CancellationToken ct)
    {
        var reason = RequireReason(r);
        var (name, prefix, storage) = Validate(r, null);
        await EnsureUniqueAsync(name, prefix, null, ct);
        var sectionIds = await ResolveSectionsAsync(r.SectionIds, ct);

        var now = _time.GetUtcNow().UtcDateTime;
        var category = new MaterialCategory
        {
            Name = name,
            CodePrefix = prefix,
            DefaultUnit = r.DefaultUnit,
            CoaRequired = r.CoaRequired,
            ExpiryRequired = r.ExpiryRequired,
            QuantityTracked = r.QuantityTracked,
            DefaultStorageCondition = storage,
            ApprovalBeforeUse = r.ApprovalBeforeUse,
            LabelRequired = r.LabelRequired,
            IsActive = true,
            Sections = sectionIds.Select(id => new MaterialCategorySection { SectionId = id }).ToList(),
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = now
        };
        _db.MaterialCategories.Add(category);
        // Saved first so the signature can name the new record. A refused signature
        // throws inside the unit of work and the row is rolled back; where there is no
        // transaction (non-relational provider) the row is taken out again here.
        _db.CurrentUserId = currentUserId;
        await _db.SaveChangesAsync(ct);

        try
        {
            await _signatureService.SignAsync(
                currentUserId, r.Password, SignatureMeaning.MasterDataChanged,
                ReviewEntityTypes.MaterialCategory, category.Id, $"Create category {prefix} {name}: {reason}", ipAddress);
        }
        catch
        {
            if (!_db.Database.IsRelational())
            {
                _db.MaterialCategories.Remove(category);
                await _db.SaveChangesAsync(ct);
            }
            throw;
        }

        await _auditEventService.RecordUserEventAsync(
            CreatedActionCode, AuditActionCategory.Configuration, ReviewEntityTypes.MaterialCategory,
            reason: reason, changes: Describe(category), entityId: category.Id.ToString(), cancellationToken: ct);

        return MaterialCategoryResponse.From(category, hasEntries: false);
    }

    public Task<MaterialCategoryResponse> UpdateAsync(int id, SaveMaterialCategoryRequest r, int currentUserId, string? ipAddress, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => UpdateCoreAsync(id, r, currentUserId, ipAddress, ct));

    private async Task<MaterialCategoryResponse> UpdateCoreAsync(int id, SaveMaterialCategoryRequest r, int currentUserId, string? ipAddress, CancellationToken ct)
    {
        var reason = RequireReason(r);
        var category = await _db.MaterialCategories.Include(c => c.Sections).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Material category {id} not found.");
        var (name, prefix, storage) = Validate(r, category.CodePrefix);
        RecordVersion.EnsureCurrent(_db, category);

        var hasEntries = await _db.MaterialMasterEntries.AnyAsync(e => e.MaterialCategoryId == id, ct);
        if (prefix != category.CodePrefix && hasEntries)
            throw new InvalidOperationException("This category has master entries; its code prefix can't change.");
        await EnsureUniqueAsync(name, prefix, id, ct);
        var sectionIds = await ResolveSectionsAsync(r.SectionIds, ct);

        var before = Describe(category).ToList();

        // Signs first - a wrong password throws and nothing may change.
        _db.CurrentUserId = currentUserId;
        await _signatureService.SignAsync(
            currentUserId, r.Password, SignatureMeaning.MasterDataChanged,
            ReviewEntityTypes.MaterialCategory, id, $"Change category {category.CodePrefix} {category.Name}: {reason}", ipAddress);

        category.Name = name;
        category.CodePrefix = prefix;
        category.DefaultUnit = r.DefaultUnit;
        category.CoaRequired = r.CoaRequired;
        category.ExpiryRequired = r.ExpiryRequired;
        category.QuantityTracked = r.QuantityTracked;
        category.DefaultStorageCondition = storage;
        category.ApprovalBeforeUse = r.ApprovalBeforeUse;
        category.LabelRequired = r.LabelRequired;

        var current = category.Sections.Select(s => s.SectionId).ToHashSet();
        foreach (var removed in category.Sections.Where(s => !sectionIds.Contains(s.SectionId)).ToList())
        {
            category.Sections.Remove(removed);
            _db.MaterialCategorySections.Remove(removed);
        }
        foreach (var added in sectionIds.Where(s => !current.Contains(s)))
            category.Sections.Add(new MaterialCategorySection { MaterialCategoryId = id, SectionId = added });

        category.LastModifiedByUserId = currentUserId;
        category.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        var after = Describe(category).ToList();
        var changes = before.Zip(after).Where(p => p.First.NewValue != p.Second.NewValue)
            .Select(p => new AuditFieldChange(p.First.FieldName, p.First.NewValue, p.Second.NewValue)).ToList();

        await _auditEventService.RecordUserEventAsync(
            UpdatedActionCode, AuditActionCategory.Configuration, ReviewEntityTypes.MaterialCategory,
            reason: reason, changes: changes, entityId: id.ToString(), cancellationToken: ct);

        return MaterialCategoryResponse.From(category, hasEntries);
    }

    public async Task<MaterialCategoryResponse> SetActiveAsync(int id, bool isActive, int currentUserId, CancellationToken ct = default)
    {
        var category = await _db.MaterialCategories.Include(c => c.Sections).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Material category {id} not found.");

        category.IsActive = isActive;
        category.LastModifiedByUserId = currentUserId;
        category.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return MaterialCategoryResponse.From(category, await _db.MaterialMasterEntries.AnyAsync(e => e.MaterialCategoryId == id, ct));
    }

    private static string RequireReason(SaveMaterialCategoryRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Reason))
            throw new InvalidOperationException("A reason for the change is required.");
        if (string.IsNullOrEmpty(r.Password))
            throw new InvalidOperationException("Your password is required to sign the change.");
        return r.Reason.Trim();
    }

    // currentPrefix: the stored prefix on an update. An unchanged prefix is not re-checked, because the
    // backfill created some that carry a digit or a reserved-looking letter pair.
    private static (string Name, string Prefix, string? Storage) Validate(SaveMaterialCategoryRequest r, string? currentPrefix)
    {
        var name = r.Name?.Trim() ?? string.Empty;
        if (name.Length == 0) throw new InvalidOperationException("Name is required.");
        if (name.Length > 100) throw new InvalidOperationException("Name can be at most 100 characters.");

        var prefix = r.CodePrefix?.Trim().ToUpperInvariant() ?? string.Empty;
        if (prefix != currentPrefix)
        {
            if (!PrefixPattern.IsMatch(prefix))
                throw new InvalidOperationException("The code prefix must be 2 to 5 letters (A-Z), without a dash.");
            if (ReservedPrefixes.Contains(prefix))
                throw new InvalidOperationException($"The prefix \"{prefix}-\" belongs to another material group; choose a different one.");
        }

        var storage = string.IsNullOrWhiteSpace(r.DefaultStorageCondition) ? null : r.DefaultStorageCondition.Trim();
        if (storage is { Length: > 100 }) throw new InvalidOperationException("Storage condition can be at most 100 characters.");
        return (name, prefix, storage);
    }

    private async Task EnsureUniqueAsync(string name, string prefix, int? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        if (await _db.MaterialCategories.AnyAsync(c => c.Id != exceptId && c.Name.ToLower() == lower, ct))
            throw new InvalidOperationException($"A material category named \"{name}\" already exists.");
        if (await _db.MaterialCategories.AnyAsync(c => c.Id != exceptId && c.CodePrefix == prefix, ct))
            throw new InvalidOperationException($"The code prefix \"{prefix}\" is already used by another category.");
    }

    private async Task<HashSet<int>> ResolveSectionsAsync(IReadOnlyList<int>? requested, CancellationToken ct)
    {
        var ids = (requested ?? Array.Empty<int>()).ToHashSet();
        if (ids.Count == 0) return ids;
        var found = await _db.DocumentSections.Where(s => ids.Contains(s.Id) && s.IsActive).Select(s => s.Id).ToListAsync(ct);
        if (found.Count != ids.Count)
            throw new InvalidOperationException("One or more of the selected sections do not exist or are inactive.");
        return ids;
    }

    private static IEnumerable<AuditFieldChange> Describe(MaterialCategory c) => new[]
    {
        new AuditFieldChange("Name", null, c.Name),
        new AuditFieldChange("CodePrefix", null, c.CodePrefix),
        new AuditFieldChange("DefaultUnit", null, c.DefaultUnit.ToString()),
        new AuditFieldChange("CoaRequired", null, c.CoaRequired.ToString()),
        new AuditFieldChange("ExpiryRequired", null, c.ExpiryRequired.ToString()),
        new AuditFieldChange("QuantityTracked", null, c.QuantityTracked.ToString()),
        new AuditFieldChange("DefaultStorageCondition", null, c.DefaultStorageCondition),
        new AuditFieldChange("ApprovalBeforeUse", null, c.ApprovalBeforeUse.ToString()),
        new AuditFieldChange("LabelRequired", null, c.LabelRequired.ToString()),
        new AuditFieldChange("Sections", null, string.Join(",", c.Sections.Select(s => s.SectionId).OrderBy(i => i))),
    };
}
