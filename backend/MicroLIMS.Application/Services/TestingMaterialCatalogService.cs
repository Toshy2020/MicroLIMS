using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Read-only list behind the single Master Testing Materials page and the stock-receipt picker:
// the five groups in one shape. Every lab sees every row (Q3); CanEdit says whether the user's
// lab owns it. The groups are views over the existing records, nothing is copied.
public class TestingMaterialCatalogService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public TestingMaterialCatalogService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    // group: one of TestingMaterialGroups (null = all). search matches code or name. sectionId
    // narrows Other materials to the categories offered to that section.
    public async Task<List<TestingMaterialCatalogRow>> GetAsync(
        int currentUserId, string? group = null, string? search = null, bool activeOnly = false, int? sectionId = null,
        CancellationToken ct = default)
    {
        string? wanted = null;
        if (!string.IsNullOrWhiteSpace(group))
        {
            wanted = TestingMaterialGroups.All.FirstOrDefault(g => g.Equals(group.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Unknown group \"{group}\". Use one of: {string.Join(", ", TestingMaterialGroups.All)}.");
        }
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
        bool Wants(string g) => wanted == null || wanted == g;
        bool Matches(string code, string name) =>
            term == null || code.ToLower().Contains(term) || name.ToLower().Contains(term);

        var editable = await _scope.GetConfigurableSectionIdsAsync(currentUserId, ct);
        bool Owns(int ownerSectionId) => editable == null || editable.Contains(ownerSectionId);

        var rows = new List<TestingMaterialCatalogRow>();

        if (Wants(TestingMaterialGroups.ReagentsIndicators) || Wants(TestingMaterialGroups.Standards) || Wants(TestingMaterialGroups.OtherMaterials))
        {
            var entries = await _db.MaterialMasterEntries.AsNoTracking()
                .Include(e => e.Section).Include(e => e.MaterialCategory).ThenInclude(c => c!.Sections)
                .Where(e => !activeOnly || e.IsActive)
                .OrderBy(e => e.Code).ToListAsync(ct);
            var stocked = (await _db.Materials.Where(m => m.MaterialMasterEntryId != null)
                .Select(m => m.MaterialMasterEntryId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

            foreach (var e in entries)
            {
                var entryGroup = e.Category switch
                {
                    MaterialMasterCategory.Reagent or MaterialMasterCategory.Indicator => TestingMaterialGroups.ReagentsIndicators,
                    MaterialMasterCategory.ReferenceStandard or MaterialMasterCategory.PrimaryStandard => TestingMaterialGroups.Standards,
                    _ => TestingMaterialGroups.OtherMaterials
                };
                if (!Wants(entryGroup) || !Matches(e.Code, e.Name)) continue;
                if (entryGroup == TestingMaterialGroups.OtherMaterials && sectionId.HasValue
                    && e.MaterialCategory is { Sections.Count: > 0 } c && c.Sections.All(s => s.SectionId != sectionId.Value))
                    continue;
                var sub = entryGroup == TestingMaterialGroups.OtherMaterials
                    ? e.MaterialCategory?.Name
                    : e.Category switch
                    {
                        MaterialMasterCategory.ReferenceStandard => "Reference Standard",
                        MaterialMasterCategory.PrimaryStandard => "Primary Standard",
                        _ => e.Category.ToString()
                    };
                rows.Add(new(entryGroup, e.Id, TestingMaterialSourceTypes.MaterialMasterEntry, e.Code, e.Name, sub,
                    e.Section?.Code, e.IsActive, stocked.Contains(e.Id), Owns(e.SectionId), false));
            }
        }

        if (Wants(TestingMaterialGroups.Standards))
        {
            // Working standards: lots created by approving a qualification, listed for lookup only.
            var lots = await _db.Materials.AsNoTracking().Include(m => m.Section)
                .Where(m => m.MaterialType == MaterialType.WorkingStandard && m.Code != null)
                .OrderBy(m => m.Code).ToListAsync(ct);
            foreach (var m in lots)
            {
                var active = m.Status == StockStatus.InStock;
                if (activeOnly && !active) continue;
                if (!Matches(m.Code!, m.MaterialName)) continue;
                rows.Add(new(TestingMaterialGroups.Standards, m.Id, TestingMaterialSourceTypes.WorkingStandard, m.Code!, m.MaterialName,
                    "Working Standard", m.Section?.Code, active, m.QuantityRemaining > 0, false, true));
            }
        }

        if (Wants(TestingMaterialGroups.CultureMedia))
        {
            var products = await _db.MediaProducts.AsNoTracking()
                .Where(p => !activeOnly || p.IsActive).OrderBy(p => p.Code).ToListAsync(ct);
            var stocked = (await _db.Materials.Where(m => m.MediaProductId != null)
                .Select(m => m.MediaProductId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
            // A media product has no owner lab: any user who may configure master data in some lab can edit it.
            var canEdit = editable == null || editable.Count > 0;
            foreach (var p in products.Where(p => Matches(p.Code, p.Name)))
                rows.Add(new(TestingMaterialGroups.CultureMedia, p.Id, TestingMaterialSourceTypes.MediaProduct, p.Code, p.Name,
                    null, null, p.IsActive, stocked.Contains(p.Id), canEdit, false));
        }

        if (Wants(TestingMaterialGroups.ReferenceOrganisms))
        {
            var strains = await _db.ReferenceOrganisms.AsNoTracking().Include(r => r.Organism).Include(r => r.OwnerSection)
                .Where(r => !activeOnly || r.IsActive).OrderBy(r => r.Code).ToListAsync(ct);
            var stocked = (await _db.Materials.Where(m => m.ReferenceOrganismId != null)
                .Select(m => m.ReferenceOrganismId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
            foreach (var r in strains)
            {
                var name = ReferenceOrganismNaming.NameOf(r);
                if (!Matches(r.Code, name)) continue;
                rows.Add(new(TestingMaterialGroups.ReferenceOrganisms, r.Id, TestingMaterialSourceTypes.ReferenceOrganism, r.Code, name,
                    r.Organism?.ScientificName, r.OwnerSection?.Code, r.IsActive, stocked.Contains(r.Id), Owns(r.OwnerSectionId), false));
            }
        }

        // Group order of the page's tabs, then code.
        return rows.OrderBy(r => Array.IndexOf(TestingMaterialGroups.All, r.Group)).ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
