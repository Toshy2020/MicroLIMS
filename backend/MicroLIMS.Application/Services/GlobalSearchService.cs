using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Search;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public class GlobalSearchService : IGlobalSearchService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public GlobalSearchService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<GlobalSearchResultDto> SearchAsync(GlobalSearchRequest request, int userId, CancellationToken ct = default)
    {
        var rawQuery = request.Query?.Trim() ?? string.Empty;
        if (rawQuery.Length < 2)
        {
            return new GlobalSearchResultDto(rawQuery, 0, 0, new List<GlobalSearchCategoryDto>());
        }

        var sw = Stopwatch.StartNew();
        var q = rawQuery.ToLower();
        var limit = Math.Clamp(request.Limit <= 0 ? 5 : request.Limit, 1, 20);
        var targetCategory = request.Category?.Trim().ToLower();
        var searchAll = string.IsNullOrWhiteSpace(targetCategory) || targetCategory == "all";

        var scope = await _scope.GetReadableSectionIdsAsync(userId, ct);
        var categories = new List<GlobalSearchCategoryDto>();

        // If user has a non-null but empty scope, they are assigned to no laboratory sections
        var hasNoAccessibleSections = scope != null && scope.Count == 0;

        // 1. SAMPLES
        if (!hasNoAccessibleSections && (searchAll || targetCategory == "samples" || targetCategory == "sample"))
        {
            var sampleItems = await SearchSamplesAsync(q, rawQuery, scope, limit, ct);
            if (sampleItems.Count > 0)
            {
                categories.Add(new GlobalSearchCategoryDto("Samples", sampleItems.Count, sampleItems));
            }
        }

        // 2. TEST ORDERS
        if (!hasNoAccessibleSections && (searchAll || targetCategory == "testorders" || targetCategory == "tests" || targetCategory == "test"))
        {
            var testItems = await SearchTestOrdersAsync(q, rawQuery, scope, limit, ct);
            if (testItems.Count > 0)
            {
                categories.Add(new GlobalSearchCategoryDto("Test Orders", testItems.Count, testItems));
            }
        }

        // 3. MATERIALS / INVENTORY STOCK
        if (!hasNoAccessibleSections && (searchAll || targetCategory == "materials" || targetCategory == "material"))
        {
            var materialItems = await SearchMaterialsAsync(q, rawQuery, scope, limit, ct);
            if (materialItems.Count > 0)
            {
                categories.Add(new GlobalSearchCategoryDto("Materials", materialItems.Count, materialItems));
            }
        }

        // 4. EQUIPMENT INVENTORY
        if (!hasNoAccessibleSections && (searchAll || targetCategory == "equipment"))
        {
            var equipItems = await SearchEquipmentAsync(q, rawQuery, scope, limit, ct);
            if (equipItems.Count > 0)
            {
                categories.Add(new GlobalSearchCategoryDto("Equipment", equipItems.Count, equipItems));
            }
        }

        // 5. MEDIA PREPARATION / LOTS
        if (!hasNoAccessibleSections && (searchAll || targetCategory == "media"))
        {
            var mediaItems = await SearchMediaAsync(q, rawQuery, scope, limit, ct);
            if (mediaItems.Count > 0)
            {
                categories.Add(new GlobalSearchCategoryDto("Media", mediaItems.Count, mediaItems));
            }
        }

        // 6. CONTROLLED DOCUMENTS
        if (!hasNoAccessibleSections && (searchAll || targetCategory == "documents" || targetCategory == "docs" || targetCategory == "document"))
        {
            var docItems = await SearchDocumentsAsync(q, rawQuery, scope, limit, ct);
            if (docItems.Count > 0)
            {
                categories.Add(new GlobalSearchCategoryDto("Documents", docItems.Count, docItems));
            }
        }

        // 7. SOLUTION PREPARATIONS
        if (!hasNoAccessibleSections && (searchAll || targetCategory == "solutions" || targetCategory == "preparation"))
        {
            var solutionItems = await SearchSolutionsAsync(q, rawQuery, scope, limit, ct);
            if (solutionItems.Count > 0)
            {
                categories.Add(new GlobalSearchCategoryDto("Solutions", solutionItems.Count, solutionItems));
            }
        }

        // 8. MASTER DATA ITEMS (Products, Raw Materials, Packaging Materials)
        if (searchAll || targetCategory == "items" || targetCategory == "item" || targetCategory == "products")
        {
            var itemResults = await SearchItemsAsync(q, rawQuery, limit, ct);
            if (itemResults.Count > 0)
            {
                categories.Add(new GlobalSearchCategoryDto("Items", itemResults.Count, itemResults));
            }
        }

        sw.Stop();
        var totalMatches = categories.Sum(c => c.Count);
        return new GlobalSearchResultDto(rawQuery, totalMatches, sw.ElapsedMilliseconds, categories);
    }

    private async Task<List<GlobalSearchItemDto>> SearchSamplesAsync(string q, string rawQuery, IReadOnlyList<int>? scope, int limit, CancellationToken ct)
    {
        var query = _db.Samples
            .Include(s => s.Item)
            .Include(s => s.Machine)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .AsNoTracking();

        if (scope != null)
        {
            query = query.Where(s =>
                s.TestOrders.Any(t => !t.IsSuperseded && scope.Contains(t.SectionId)) ||
                s.SectionSignoffs.Any(ss => scope.Contains(ss.SectionId)));
        }

        query = query.Where(s =>
            s.ReferenceNumber.ToLower().Contains(q) ||
            (s.BatchNumber != null && s.BatchNumber.ToLower().Contains(q)) ||
            s.ControlNumber.ToLower().Contains(q) ||
            s.Id.ToString() == q ||
            (s.Item != null && s.Item.Name.ToLower().Contains(q)) ||
            (s.Machine != null && s.Machine.Name.ToLower().Contains(q)) ||
            (s.WaterSamplingPoint != null && s.WaterSamplingPoint.Code.ToLower().Contains(q)) ||
            (s.Department != null && s.Department.Name.ToLower().Contains(q))
        );

        // Fetch candidates with limit * 2 to allow in-memory deterministic ranking
        var matches = await query.Take(limit * 2).ToListAsync(ct);

        return matches
            .OrderByDescending(s => s.ReferenceNumber.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(s => s.BatchNumber != null && s.BatchNumber.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(s => s.ReferenceNumber.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(s => s.Id)
            .Take(limit)
            .Select(s =>
            {
                var subject = s.Item?.Name ?? s.Machine?.Name ?? s.WaterSamplingPoint?.Code ?? s.Department?.Name ?? $"{s.Category} Sample";
                var batchPart = !string.IsNullOrWhiteSpace(s.BatchNumber) ? $" • Batch: {s.BatchNumber}" : "";
                var matchedField = s.ReferenceNumber.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "ReferenceNumber"
                    : (s.BatchNumber != null && s.BatchNumber.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "BatchNumber"
                    : (s.ControlNumber.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "ControlNumber" : "Subject"));
                var matchedVal = matchedField == "ReferenceNumber" ? s.ReferenceNumber
                    : (matchedField == "BatchNumber" ? s.BatchNumber ?? ""
                    : (matchedField == "ControlNumber" ? s.ControlNumber : subject));

                return new GlobalSearchItemDto(
                    s.Id,
                    "Sample",
                    s.ReferenceNumber,
                    $"{subject}{batchPart}",
                    s.Status.ToString(),
                    matchedField,
                    matchedVal,
                    GetBadgeColor(s.Status.ToString()),
                    $"/receiving-testing?sampleId={s.Id}",
                    new Dictionary<string, string>
                    {
                        ["Category"] = s.Category.ToString(),
                        ["ControlNumber"] = s.ControlNumber
                    }
                );
            })
            .ToList();
    }

    private async Task<List<GlobalSearchItemDto>> SearchTestOrdersAsync(string q, string rawQuery, IReadOnlyList<int>? scope, int limit, CancellationToken ct)
    {
        var query = _db.TestOrders
            .Include(t => t.Sample)
            .AsNoTracking()
            .Where(t => !t.IsSuperseded);

        if (scope != null)
        {
            query = query.Where(t => scope.Contains(t.SectionId));
        }

        query = query.Where(t =>
            t.TestCode.ToLower().Contains(q) ||
            t.Id.ToString() == q ||
            (t.Sample != null && t.Sample.ReferenceNumber.ToLower().Contains(q))
        );

        var matches = await query.Take(limit * 2).ToListAsync(ct);

        return matches
            .OrderByDescending(t => t.TestCode.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(t => t.TestCode.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(t => t.Id)
            .Take(limit)
            .Select(t => new GlobalSearchItemDto(
                t.Id,
                "TestOrder",
                $"Test: {t.TestCode} (TO-{t.Id})",
                $"Sample: {(t.Sample != null ? t.Sample.ReferenceNumber : t.SampleId.ToString())} • Status: {t.Status}",
                t.Status.ToString(),
                t.TestCode.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "TestCode" : "Sample",
                t.TestCode,
                GetBadgeColor(t.Status.ToString()),
                $"/receiving-testing?sampleId={t.SampleId}&testOrderId={t.Id}",
                new Dictionary<string, string>
                {
                    ["SampleId"] = t.SampleId.ToString(),
                    ["TestCode"] = t.TestCode
                }
            ))
            .ToList();
    }

    private async Task<List<GlobalSearchItemDto>> SearchMaterialsAsync(string q, string rawQuery, IReadOnlyList<int>? scope, int limit, CancellationToken ct)
    {
        var query = _db.Materials.AsNoTracking();

        if (scope != null)
        {
            query = query.Where(m => scope.Contains(m.SectionId));
        }

        query = query.Where(m =>
            m.MaterialName.ToLower().Contains(q) ||
            m.BatchNumber.ToLower().Contains(q) ||
            (m.Code != null && m.Code.ToLower().Contains(q)) ||
            (m.StockId != null && m.StockId.ToLower().Contains(q)) ||
            m.ManufacturerName.ToLower().Contains(q)
        );

        // A scanned stock ID (exact match) always makes the cut, ahead of the general limit.
        var matches = await query
            .OrderByDescending(m => m.StockId != null && m.StockId.ToLower() == q)
            .Take(limit * 2).ToListAsync(ct);

        return matches
            .OrderByDescending(m => m.StockId != null && m.StockId.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.MaterialName.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.BatchNumber.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.MaterialName.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(m => new GlobalSearchItemDto(
                m.Id,
                "Material",
                m.MaterialName,
                $"{(m.StockId != null ? m.StockId + " • " : "")}Batch: {m.BatchNumber} • Location: {m.Location}",
                m.MaterialType.ToString(),
                m.StockId != null && m.StockId.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "StockId"
                    : m.BatchNumber.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "BatchNumber" : "MaterialName",
                m.StockId != null && m.StockId.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? m.StockId
                    : m.BatchNumber.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? m.BatchNumber : m.MaterialName,
                "primary",
                "/inventory/materials",
                new Dictionary<string, string>
                {
                    ["MaterialType"] = m.MaterialType.ToString(),
                    ["BatchNumber"] = m.BatchNumber
                }
            ))
            .ToList();
    }

    private async Task<List<GlobalSearchItemDto>> SearchEquipmentAsync(string q, string rawQuery, IReadOnlyList<int>? scope, int limit, CancellationToken ct)
    {
        var query = _db.EquipmentInventories.AsNoTracking();

        if (scope != null)
        {
            query = query.Where(e => e.SectionId != null && scope.Contains(e.SectionId.Value));
        }

        query = query.Where(e =>
            e.Code.ToLower().Contains(q) ||
            e.InstrumentType.ToLower().Contains(q) ||
            e.ManufacturerName.ToLower().Contains(q) ||
            (e.SerialNumber != null && e.SerialNumber.ToLower().Contains(q))
        );

        var matches = await query.Take(limit * 2).ToListAsync(ct);

        return matches
            .OrderByDescending(e => e.Code.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(e => e.Code.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(e => new GlobalSearchItemDto(
                e.Id,
                "Equipment",
                $"{e.Code} — {e.InstrumentType}",
                $"Manufacturer: {e.ManufacturerName} • Location: {e.Location}",
                e.Status.ToString(),
                e.Code.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "Code" : "InstrumentType",
                e.Code,
                GetBadgeColor(e.Status.ToString()),
                "/inventory/equipment",
                new Dictionary<string, string>
                {
                    ["Code"] = e.Code,
                    ["InstrumentType"] = e.InstrumentType
                }
            ))
            .ToList();
    }

    private async Task<List<GlobalSearchItemDto>> SearchMediaAsync(string q, string rawQuery, IReadOnlyList<int>? scope, int limit, CancellationToken ct)
    {
        var query = _db.Media
            .Include(m => m.Material)
            .AsNoTracking();

        if (scope != null)
        {
            query = query.Where(m => m.Material != null && scope.Contains(m.Material.SectionId));
        }

        query = query.Where(m =>
            m.LotNumber.ToLower().Contains(q) ||
            (m.Material != null && m.Material.MaterialName.ToLower().Contains(q)) ||
            m.ManufacturerLot.ToLower().Contains(q) ||
            m.ManufacturerName.ToLower().Contains(q)
        );

        var matches = await query.Take(limit * 2).ToListAsync(ct);

        return matches
            .OrderByDescending(m => m.LotNumber.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(m => m.LotNumber.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(m =>
            {
                var mediaName = m.Material?.MaterialName ?? "Prepared Media";
                return new GlobalSearchItemDto(
                    m.Id,
                    "Media",
                    $"Media: {mediaName} (Lot: {m.LotNumber})",
                    $"Status: {m.Status} • Exp: {m.ExpiryDate:dd-MMM-yyyy}",
                    m.Status.ToString(),
                    m.LotNumber.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "LotNumber" : "MaterialName",
                    m.LotNumber,
                    GetBadgeColor(m.Status.ToString()),
                    "/laboratory-configuration/media",
                    new Dictionary<string, string>
                    {
                        ["LotNumber"] = m.LotNumber,
                        ["IsReleased"] = m.IsReleasedForUse.ToString()
                    }
                );
            })
            .ToList();
    }

    private async Task<List<GlobalSearchItemDto>> SearchDocumentsAsync(string q, string rawQuery, IReadOnlyList<int>? scope, int limit, CancellationToken ct)
    {
        var query = _db.DocumentMasters
            .AsNoTracking()
            .Where(d => d.RecordStatus == DocumentRecordStatus.Active);

        if (scope != null)
        {
            query = query.Where(d => scope.Contains(d.SectionId));
        }

        query = query.Where(d =>
            d.CompanyDocumentCode.ToLower().Contains(q) ||
            d.MicroLimsDocumentId.ToLower().Contains(q) ||
            d.Title.ToLower().Contains(q)
        );

        var matches = await query.Take(limit * 2).ToListAsync(ct);

        return matches
            .OrderByDescending(d => d.CompanyDocumentCode.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(d => d.CompanyDocumentCode.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(d => new GlobalSearchItemDto(
                d.Id,
                "DocumentMaster",
                $"Doc: {d.CompanyDocumentCode}",
                d.Title,
                d.RecordStatus.ToString(),
                d.CompanyDocumentCode.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "CompanyDocumentCode" : "Title",
                d.CompanyDocumentCode,
                "info",
                $"/document-control/documents/{d.Id}",
                new Dictionary<string, string>
                {
                    ["CompanyDocumentCode"] = d.CompanyDocumentCode,
                    ["MicroLimsDocumentId"] = d.MicroLimsDocumentId
                }
            ))
            .ToList();
    }

    private async Task<List<GlobalSearchItemDto>> SearchSolutionsAsync(string q, string rawQuery, IReadOnlyList<int>? scope, int limit, CancellationToken ct)
    {
        var query = _db.SolutionPreparations
            .Include(sp => sp.SolutionMaster)
            .AsNoTracking();

        if (scope != null)
        {
            query = query.Where(sp => scope.Contains(sp.SectionId));
        }

        query = query.Where(sp =>
            (sp.Code != null && sp.Code.ToLower().Contains(q)) ||
            (sp.SolutionMaster != null && sp.SolutionMaster.Name.ToLower().Contains(q))
        );

        var matches = await query.Take(limit * 2).ToListAsync(ct);

        return matches
            .OrderByDescending(sp => sp.Code != null && sp.Code.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(sp => sp.Code != null && sp.Code.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(sp =>
            {
                var solName = sp.SolutionMaster?.Name ?? "Solution";
                var codeDisplay = sp.Code ?? $"PREP-{sp.Id}";
                return new GlobalSearchItemDto(
                    sp.Id,
                    "SolutionPreparation",
                    $"Solution: {solName} ({codeDisplay})",
                    $"Type: {sp.Type} • Status: {sp.Status}",
                    sp.Status.ToString(),
                    sp.Code != null && sp.Code.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "Code" : "Name",
                    codeDisplay,
                    GetBadgeColor(sp.Status.ToString()),
                    $"/preparation/{sp.Id}",
                    new Dictionary<string, string>
                    {
                        ["Code"] = codeDisplay,
                        ["Type"] = sp.Type.ToString()
                    }
                );
            })
            .ToList();
    }

    private async Task<List<GlobalSearchItemDto>> SearchItemsAsync(string q, string rawQuery, int limit, CancellationToken ct)
    {
        var query = _db.Items
            .AsNoTracking()
            .Where(i =>
                i.Code.ToLower().Contains(q) ||
                i.Name.ToLower().Contains(q)
            );

        var matches = await query.Take(limit * 2).ToListAsync(ct);

        return matches
            .OrderByDescending(i => i.Code.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(i => i.Name.Equals(rawQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(i => i.Code.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(i => new GlobalSearchItemDto(
                i.Id,
                "Item",
                $"Item: {i.Name} ({i.Code})",
                $"Category: {i.Category}",
                "Active",
                i.Code.Contains(rawQuery, StringComparison.OrdinalIgnoreCase) ? "Code" : "Name",
                i.Code,
                "success",
                "/laboratory-configuration/items",
                new Dictionary<string, string>
                {
                    ["Code"] = i.Code,
                    ["Category"] = i.Category.ToString()
                }
            ))
            .ToList();
    }

    private static string GetBadgeColor(string status) => status switch
    {
        "Approved" or "Effective" or "InService" or "Ready" or "Released" or "Completed" => "success",
        "InTesting" or "UnderReview" or "UnderApproval" or "Pending" or "Prepared" or "InProgress" => "warning",
        "Rejected" or "OutOfService" or "Overdue" or "Cancelled" or "Voided" => "error",
        _ => "primary"
    };
}
