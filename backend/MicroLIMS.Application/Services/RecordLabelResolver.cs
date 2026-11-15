using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;

namespace MicroLIMS.Application.Services;

// Labels for the recently-opened list, for the records the user may read right now. Each type applies the
// same read rule as the record's own GET service (see the comment on each method), but in one query per
// type instead of one guard call per row. The scope is looked up once.
public sealed class RecordLabelResolver : IRecordLabelResolver
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IDocumentAuthorizationService _documents;

    public RecordLabelResolver(IMicroLimsDbContext db, IUserSectionScopeService scope, IDocumentAuthorizationService documents)
    {
        _db = db;
        _scope = scope;
        _documents = documents;
    }

    public async Task<IReadOnlyDictionary<(string Type, int Id), (string Identifier, string Title)>> ResolveAsync(
        int userId, IReadOnlyCollection<(string Type, int Id)> records, CancellationToken ct = default)
    {
        var result = new Dictionary<(string Type, int Id), (string Identifier, string Title)>();
        if (records.Count == 0) return result;

        List<int> IdsOf(string type) => records.Where(r => r.Type == type).Select(r => r.Id).Distinct().ToList();
        var sampleIds = IdsOf(RecentRecordTypes.Sample);
        var testOrderIds = IdsOf(RecentRecordTypes.TestOrder);
        var itemIds = IdsOf(RecentRecordTypes.Item);
        var documentIds = IdsOf(RecentRecordTypes.Document);
        var equipmentIds = IdsOf(RecentRecordTypes.Equipment);
        var materialIds = IdsOf(RecentRecordTypes.Material);
        var mediaIds = IdsOf(RecentRecordTypes.Media);
        var solutionIds = IdsOf(RecentRecordTypes.Solution);

        // READ scope, as every Ensure*Access(..., SectionAccess.Read) guard uses it: null = every section
        // (Sections.ViewAll), else the sections the user belongs to. Only needed for the section-scoped types.
        IReadOnlyList<int>? scope = null;
        var sectionScoped = sampleIds.Count + testOrderIds.Count + equipmentIds.Count + materialIds.Count + mediaIds.Count + solutionIds.Count > 0;
        if (sectionScoped) scope = await _scope.GetReadableSectionIdsAsync(userId, ct);
        bool InScope(int sectionId) => scope is null || scope.Contains(sectionId);

        if (sampleIds.Count > 0) await AddSamplesAsync(sampleIds, InScope, result, ct);
        if (testOrderIds.Count > 0) await AddTestOrdersAsync(testOrderIds, InScope, result, ct);
        if (itemIds.Count > 0) await AddItemsAsync(itemIds, result, ct);
        if (documentIds.Count > 0) await AddDocumentsAsync(userId, documentIds, result, ct);
        if (equipmentIds.Count > 0) await AddEquipmentAsync(equipmentIds, InScope, result, ct);
        if (materialIds.Count > 0) await AddMaterialsAsync(materialIds, InScope, result, ct);
        if (mediaIds.Count > 0) await AddMediaAsync(mediaIds, InScope, result, ct);
        if (solutionIds.Count > 0) await AddSolutionsAsync(solutionIds, InScope, result, ct);
        return result;
    }

    // Same rule as EnsureSampleAccessAsync: readable when any non-superseded test order section, pending target
    // section (periodic retest awaiting its tests) or the lab section is in scope.
    private async Task AddSamplesAsync(List<int> ids, Func<int, bool> inScope,
        Dictionary<(string Type, int Id), (string Identifier, string Title)> result, CancellationToken ct)
    {
        var rows = await _db.Samples.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.ReferenceNumber,
                ItemName = s.Item != null ? s.Item.Name : null,
                s.Category,
                s.AwaitingTestSelection,
                s.PendingTargetSectionIds,
                s.LabSectionId,
                OrderSectionIds = s.TestOrders.Where(t => !t.IsSuperseded).Select(t => t.SectionId).ToList()
            })
            .ToListAsync(ct);

        foreach (var s in rows)
        {
            var sections = s.OrderSectionIds.Concat(SampleSectionRollup.PendingSectionIds(s.AwaitingTestSelection, s.PendingTargetSectionIds));
            if (s.LabSectionId.HasValue) sections = sections.Concat(new[] { s.LabSectionId.Value });
            if (!sections.Any(inScope)) continue;
            result[(RecentRecordTypes.Sample, s.Id)] = (s.ReferenceNumber, s.ItemName ?? s.Category.ToString());
        }
    }

    // Same rule as EnsureTestOrderAccessAsync: the order's own section.
    private async Task AddTestOrdersAsync(List<int> ids, Func<int, bool> inScope,
        Dictionary<(string Type, int Id), (string Identifier, string Title)> result, CancellationToken ct)
    {
        var rows = await _db.TestOrders.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.SectionId,
                Reference = t.Sample!.ReferenceNumber,
                t.TestCode,
                DisplayName = _db.TestDefinitions.Where(d => d.Code == t.TestCode).Select(d => d.DisplayName).FirstOrDefault()
            })
            .ToListAsync(ct);

        foreach (var t in rows.Where(t => inScope(t.SectionId)))
            result[(RecentRecordTypes.TestOrder, t.Id)] = (t.Reference, t.DisplayName ?? t.TestCode);
    }

    // No read-scope rule: ItemController.GetAll serves every item to every signed-in user (item master data is not
    // section-scoped), so every signed-in user may read every item.
    private async Task AddItemsAsync(List<int> ids,
        Dictionary<(string Type, int Id), (string Identifier, string Title)> result, CancellationToken ct)
    {
        var rows = await _db.Items.AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.Name })
            .ToListAsync(ct);
        foreach (var i in rows) result[(RecentRecordTypes.Item, i.Id)] = (i.Code, i.Name);
    }

    // Same rule as DocumentMasterService.GetByIdAsync: IDocumentAuthorizationService.CanViewDocumentAsync
    // (voided documents only for Section Head or audit oversight), applied to the whole list.
    private async Task AddDocumentsAsync(int userId, List<int> ids,
        Dictionary<(string Type, int Id), (string Identifier, string Title)> result, CancellationToken ct)
    {
        var viewable = await _documents.FilterViewableDocumentIdsAsync(ids, userId, ct);
        if (viewable.Count == 0) return;
        var viewableIds = viewable.ToList();
        var rows = await _db.DocumentMasters.AsNoTracking()
            .Where(d => viewableIds.Contains(d.Id))
            .Select(d => new { d.Id, d.CompanyDocumentCode, d.MicroLimsDocumentId, d.Title })
            .ToListAsync(ct);
        foreach (var d in rows)
            result[(RecentRecordTypes.Document, d.Id)] =
                (string.IsNullOrWhiteSpace(d.CompanyDocumentCode) ? d.MicroLimsDocumentId : d.CompanyDocumentCode, d.Title);
    }

    // Same rule as EnsureEquipmentAccessAsync: the equipment's section.
    private async Task AddEquipmentAsync(List<int> ids, Func<int, bool> inScope,
        Dictionary<(string Type, int Id), (string Identifier, string Title)> result, CancellationToken ct)
    {
        var rows = await _db.Equipment.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.SectionId, e.Code, e.Name })
            .ToListAsync(ct);
        foreach (var e in rows.Where(e => inScope(e.SectionId)))
            result[(RecentRecordTypes.Equipment, e.Id)] = (e.Code, e.Name);
    }

    // Same rule as EnsureMaterialAccessAsync: the material's section.
    private async Task AddMaterialsAsync(List<int> ids, Func<int, bool> inScope,
        Dictionary<(string Type, int Id), (string Identifier, string Title)> result, CancellationToken ct)
    {
        var rows = await _db.Materials.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.SectionId, m.Code, m.StockId, m.BatchNumber, m.MaterialName })
            .ToListAsync(ct);
        foreach (var m in rows.Where(m => inScope(m.SectionId)))
            result[(RecentRecordTypes.Material, m.Id)] = (m.Code ?? m.StockId ?? m.BatchNumber, m.MaterialName);
    }

    // Same rule as EnsureMediaAccessAsync: the section of the Material the lot was made from.
    private async Task AddMediaAsync(List<int> ids, Func<int, bool> inScope,
        Dictionary<(string Type, int Id), (string Identifier, string Title)> result, CancellationToken ct)
    {
        var rows = await _db.Media.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.LotNumber, SectionId = m.Material!.SectionId, Name = m.Material.MaterialName })
            .ToListAsync(ct);
        foreach (var m in rows.Where(m => inScope(m.SectionId)))
            result[(RecentRecordTypes.Media, m.Id)] = (m.LotNumber, m.Name);
    }

    // Same rule as SolutionPreparationService.GetByIdAsync (EnsureAccessAsync, Read): the preparation's section.
    private async Task AddSolutionsAsync(List<int> ids, Func<int, bool> inScope,
        Dictionary<(string Type, int Id), (string Identifier, string Title)> result, CancellationToken ct)
    {
        var rows = await _db.SolutionPreparations.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.SectionId, p.Code, Name = p.SolutionMaster!.Name })
            .ToListAsync(ct);
        foreach (var p in rows.Where(p => inScope(p.SectionId)))
            result[(RecentRecordTypes.Solution, p.Id)] = (p.Code ?? $"#{p.Id}", p.Name);
    }
}
