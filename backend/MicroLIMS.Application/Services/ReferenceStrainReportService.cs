using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class ReferenceStrainReportService
{
    private readonly MicroLimsDbContext _db;

    public ReferenceStrainReportService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<ReferenceStrainSearchResult> SearchAsync(ReferenceStrainSearchRequest request)
    {
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 200);
        var page = request.Page <= 0 ? 1 : request.Page;

        var baseQuery = BuildFilteredQuery(request);
        var totalCount = await baseQuery.CountAsync();

        var sortedQuery = ApplySort(baseQuery, request);
        var cryovials = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Organism)
            .Include(c => c.Material)
            .ToListAsync();

        var cryovialIds = cryovials.Select(c => c.Id).ToList();

        var usageCounts = await _db.MediaEvaluationChallenges
            .Where(ch => ch.CryovialId.HasValue && cryovialIds.Contains(ch.CryovialId.Value))
            .GroupBy(ch => ch.CryovialId!.Value)
            .Select(g => new { CryovialId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CryovialId, g => g.Count);

        var userIds = new HashSet<int>();
        foreach (var c in cryovials)
        {
            if (c.PreparedByUserId > 0) userIds.Add(c.PreparedByUserId);
            if (c.ApprovedByUserId.HasValue) userIds.Add(c.ApprovedByUserId.Value);
        }

        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        string NameOf(int id) => names.TryGetValue(id, out var n) ? n : (id == 0 ? "Not recorded" : "Unknown");

        var items = cryovials.Select(c =>
        {
            usageCounts.TryGetValue(c.Id, out var usageCount);
            var strainName = c.Organism?.ScientificName ?? c.OrganismNameSnapshot;

            return new ReferenceStrainListDto(
                Id: c.Id,
                StrainName: strainName,
                AtccNumber: c.Organism?.AtccNumber,
                CryovialCode: c.Code,
                ManufacturerName: c.ManufacturerName,
                SourceMaterialName: c.Material?.MaterialName ?? string.Empty,
                SourceMaterialBatchNumber: c.Material?.BatchNumber ?? string.Empty,
                ReceiptDate: c.Material?.ReceivingDate ?? DateTime.MinValue,
                PreparedAt: c.PreparedAt,
                ExpiryDate: c.ExpiryDate,
                NumberOfVialsPrepared: c.NumberOfVialsPrepared,
                VialsRemaining: c.VialsRemaining,
                StorageCondition: c.StorageCondition,
                ApprovalStatus: c.ApprovalStatus.ToString(),
                IsDestroyed: c.IsDestroyed,
                PreparedByName: NameOf(c.PreparedByUserId),
                ApprovedByName: c.ApprovedByUserId.HasValue ? NameOf(c.ApprovedByUserId.Value) : null,
                ApprovedAt: c.ApprovedAt,
                DirectUsageCount: usageCount
            );
        }).ToList();

        return new ReferenceStrainSearchResult(items, totalCount, page, pageSize);
    }

    public async Task<ReferenceStrainDetailDto?> GetDetailAsync(int cryovialId)
    {
        var cryovial = await _db.Cryovials
            .Include(c => c.Material)
            .Include(c => c.Organism)
            .Include(c => c.IdentityConfirmations).ThenInclude(i => i.Media).ThenInclude(m => m!.Material)
            .Include(c => c.IdentityConfirmations).ThenInclude(i => i.IncubatorEquipment)
            .Include(c => c.ThawHistory)
            .FirstOrDefaultAsync(c => c.Id == cryovialId);

        if (cryovial is null) return null;

        var directChallenges = await _db.MediaEvaluationChallenges
            .Where(ch => ch.CryovialId == cryovialId)
            .Include(ch => ch.MediaEvaluation).ThenInclude(e => e!.Media).ThenInclude(m => m!.Material)
            .OrderByDescending(ch => ch.ReadAt ?? DateTime.MinValue)
            .ToListAsync();

        var userIds = new HashSet<int>();
        if (cryovial.PreparedByUserId > 0) userIds.Add(cryovial.PreparedByUserId);
        if (cryovial.ApprovedByUserId.HasValue) userIds.Add(cryovial.ApprovedByUserId.Value);
        foreach (var t in cryovial.ThawHistory)
        {
            if (t.ThawedByUserId > 0) userIds.Add(t.ThawedByUserId);
        }
        foreach (var ch in directChallenges)
        {
            if (ch.ReadByUserId.HasValue) userIds.Add(ch.ReadByUserId.Value);
        }

        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        string NameOf(int id) => names.TryGetValue(id, out var n) ? n : (id == 0 ? "Not recorded" : "Unknown");

        // Primary Direct Usage Log
        var directUsageLog = directChallenges.Select(ch => new ReferenceStrainDirectUsageDto(
            ChallengeId: ch.Id,
            MediaId: ch.MediaEvaluation?.MediaId ?? 0,
            MediaLotNumber: ch.MediaEvaluation?.Media?.LotNumber ?? "-",
            MediaType: ch.MediaEvaluation?.Media?.Material?.MaterialName ?? "-",
            EvaluationType: ch.MediaEvaluation?.EvaluationType.ToString() ?? EvaluationType.GrowthPromotion.ToString(),
            ChallengeRole: ch.ChallengeRole?.ToString(),
            Outcome: ch.Outcome?.ToString(),
            ReadByName: ch.ReadByUserId.HasValue ? NameOf(ch.ReadByUserId.Value) : null,
            ReadAt: ch.ReadAt,
            EvaluationStatus: ch.MediaEvaluation?.Status.ToString() ?? "-"
        )).ToList();

        // Secondary Indirect Usage Chain:
        // Cryovial -> direct challenges -> Media lots -> MediaUsage -> TestOrders
        var qualifiedMediaIds = directChallenges
            .Where(ch => ch.MediaEvaluation != null)
            .Select(ch => ch.MediaEvaluation!.MediaId)
            .Distinct()
            .ToList();

        var indirectTestOrdersCount = 0;
        if (qualifiedMediaIds.Count > 0)
        {
            indirectTestOrdersCount = await _db.MediaUsages
                .Where(mu => qualifiedMediaIds.Contains(mu.MediaId))
                .Select(mu => mu.TestOrderId)
                .Distinct()
                .CountAsync();
        }

        var indirectUsageSummary = $"{indirectTestOrdersCount} test orders used media qualified with this strain batch (indirect, via GPT-qualified media lots)";

        var identityConfirmations = cryovial.IdentityConfirmations.Select(i => new ReferenceStrainIdentityConfirmationDto(
            Id: i.Id,
            MediaLotNumber: i.Media?.LotNumber,
            MediaName: i.Media?.Material?.MaterialName,
            IncubatorName: i.IncubatorEquipment?.Name,
            IncubationStart: i.IncubationStart,
            IncubationEnd: i.IncubationEnd,
            ObservationText: i.ObservationText
        )).ToList();

        var thawHistory = cryovial.ThawHistory
            .OrderBy(t => t.ThawedAt)
            .Select(t => new ReferenceStrainThawEventDto(
                Id: t.Id,
                ThawedAt: t.ThawedAt,
                ThawedByName: NameOf(t.ThawedByUserId),
                Notes: t.Notes
            )).ToList();

        return new ReferenceStrainDetailDto(
            Id: cryovial.Id,
            CryovialCode: cryovial.Code,
            StrainName: cryovial.Organism?.ScientificName ?? cryovial.OrganismNameSnapshot,
            AtccNumber: cryovial.Organism?.AtccNumber,
            ManufacturerName: cryovial.ManufacturerName,
            SourceMaterialName: cryovial.Material?.MaterialName ?? string.Empty,
            SourceMaterialBatchNumber: cryovial.Material?.BatchNumber ?? string.Empty,
            SourceMaterialReceivingDate: cryovial.Material?.ReceivingDate ?? DateTime.MinValue,
            SourceMaterialQuantityReceived: cryovial.Material?.QuantityReceived ?? 0m,
            PreparedAt: cryovial.PreparedAt,
            ExpiryDate: cryovial.ExpiryDate,
            NumberOfVialsPrepared: cryovial.NumberOfVialsPrepared,
            VialsRemaining: cryovial.VialsRemaining,
            StorageCondition: cryovial.StorageCondition,
            PhysicalCheckConfirmed: cryovial.PhysicalCheckConfirmed,
            PhysicalCheckText: cryovial.PhysicalCheckText,
            ApprovalStatus: cryovial.ApprovalStatus.ToString(),
            IsDestroyed: cryovial.IsDestroyed,
            PreparedByName: NameOf(cryovial.PreparedByUserId),
            ApprovedByName: cryovial.ApprovedByUserId.HasValue ? NameOf(cryovial.ApprovedByUserId.Value) : null,
            ApprovedAt: cryovial.ApprovedAt,
            IdentityConfirmations: identityConfirmations,
            ThawHistory: thawHistory,
            DirectUsageLog: directUsageLog,
            DistinctQualifiedMediaLotsCount: qualifiedMediaIds.Count,
            IndirectTestOrdersCount: indirectTestOrdersCount,
            IndirectUsageSummary: indirectUsageSummary
        );
    }

    public async Task<ReferenceStrainExportResult> GetForExportAsync(ReferenceStrainSearchRequest request, int maxRows)
    {
        var baseQuery = ApplySort(BuildFilteredQuery(request), request);
        var cryovials = await baseQuery
            .Include(c => c.Organism)
            .Include(c => c.Material)
            .Include(c => c.IdentityConfirmations)
            .Include(c => c.ThawHistory)
            .ToListAsync();

        var cryovialIds = cryovials.Select(c => c.Id).ToList();

        var allChallenges = await _db.MediaEvaluationChallenges
            .Where(ch => ch.CryovialId.HasValue && cryovialIds.Contains(ch.CryovialId.Value))
            .Include(ch => ch.MediaEvaluation)
            .ToListAsync();

        var challengesByCryovial = allChallenges
            .GroupBy(ch => ch.CryovialId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allMediaIds = allChallenges
            .Where(ch => ch.MediaEvaluation != null)
            .Select(ch => ch.MediaEvaluation!.MediaId)
            .Distinct()
            .ToList();

        var mediaUsages = await _db.MediaUsages
            .Where(mu => allMediaIds.Contains(mu.MediaId))
            .Select(mu => new { mu.MediaId, mu.TestOrderId })
            .ToListAsync();

        var userIds = new HashSet<int>();
        foreach (var c in cryovials)
        {
            if (c.PreparedByUserId > 0) userIds.Add(c.PreparedByUserId);
            if (c.ApprovedByUserId.HasValue) userIds.Add(c.ApprovedByUserId.Value);
        }

        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        string NameOf(int id) => names.TryGetValue(id, out var n) ? n : (id == 0 ? "Not recorded" : "Unknown");

        var rows = new List<ReferenceStrainExportRowDto>();

        foreach (var c in cryovials)
        {
            challengesByCryovial.TryGetValue(c.Id, out var directChs);
            var directUsageCount = directChs?.Count ?? 0;

            var linkedMediaIds = (directChs ?? new List<MediaEvaluationChallenge>())
                .Where(ch => ch.MediaEvaluation != null)
                .Select(ch => ch.MediaEvaluation!.MediaId)
                .Distinct()
                .ToHashSet();

            var indirectTestOrdersCount = mediaUsages
                .Where(mu => linkedMediaIds.Contains(mu.MediaId))
                .Select(mu => mu.TestOrderId)
                .Distinct()
                .Count();

            rows.Add(new ReferenceStrainExportRowDto(
                StrainName: c.Organism?.ScientificName ?? c.OrganismNameSnapshot,
                AtccNumber: c.Organism?.AtccNumber,
                CryovialCode: c.Code,
                ManufacturerName: c.ManufacturerName,
                SourceMaterialName: c.Material?.MaterialName ?? string.Empty,
                SourceMaterialBatchNumber: c.Material?.BatchNumber ?? string.Empty,
                ReceiptDate: c.Material?.ReceivingDate ?? DateTime.MinValue,
                PreparedAt: c.PreparedAt,
                ExpiryDate: c.ExpiryDate,
                NumberOfVialsPrepared: c.NumberOfVialsPrepared,
                VialsRemaining: c.VialsRemaining,
                StorageCondition: c.StorageCondition,
                ApprovalStatus: c.ApprovalStatus.ToString(),
                IsDestroyed: c.IsDestroyed,
                PreparedByName: NameOf(c.PreparedByUserId),
                ApprovedByName: c.ApprovedByUserId.HasValue ? NameOf(c.ApprovedByUserId.Value) : null,
                ApprovedAt: c.ApprovedAt,
                IdentityConfirmationsCount: c.IdentityConfirmations.Count,
                ThawEventsCount: c.ThawHistory.Count,
                DirectGptUsageCount: directUsageCount,
                IndirectTestOrdersCount: indirectTestOrdersCount
            ));
        }

        if (rows.Count > maxRows)
        {
            return new ReferenceStrainExportResult(new List<ReferenceStrainExportRowDto>(), rows.Count, Exceeded: true);
        }

        return new ReferenceStrainExportResult(rows, rows.Count, Exceeded: false);
    }

    public async Task<ReferenceStrainFilterOptionsDto> GetFilterOptionsAsync()
    {
        var organisms = await _db.Cryovials
            .Where(c => c.Organism != null)
            .Select(c => new OrganismOptionDto(c.Organism!.Id, c.Organism.ScientificName, c.Organism.AtccNumber))
            .Distinct()
            .OrderBy(o => o.ScientificName)
            .ToListAsync();

        return new ReferenceStrainFilterOptionsDto(organisms);
    }

    private IQueryable<Cryovial> BuildFilteredQuery(ReferenceStrainSearchRequest request)
    {
        var query = _db.Cryovials.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c =>
                c.Code.ToLower().Contains(term) ||
                (c.Organism != null && c.Organism.ScientificName.ToLower().Contains(term)) ||
                (c.Organism != null && c.Organism.AtccNumber != null && c.Organism.AtccNumber.ToLower().Contains(term)) ||
                c.ManufacturerName.ToLower().Contains(term) ||
                (c.Material != null && c.Material.BatchNumber.ToLower().Contains(term)));
        }

        if (request.OrganismId.HasValue)
        {
            query = query.Where(c => c.OrganismId == request.OrganismId.Value);
        }

        if (request.ApprovalStatus.HasValue)
        {
            query = query.Where(c => c.ApprovalStatus == request.ApprovalStatus.Value);
        }

        if (request.IsDestroyed.HasValue)
        {
            query = query.Where(c => c.IsDestroyed == request.IsDestroyed.Value);
        }

        if (request.ReceiptFromDate.HasValue)
        {
            query = query.Where(c => c.Material != null && c.Material.ReceivingDate >= request.ReceiptFromDate.Value);
        }

        if (request.ReceiptToDate.HasValue)
        {
            query = query.Where(c => c.Material != null && c.Material.ReceivingDate <= request.ReceiptToDate.Value);
        }

        if (request.UsageFromDate.HasValue || request.UsageToDate.HasValue)
        {
            query = query.Where(c => _db.MediaEvaluationChallenges.Any(ch =>
                ch.CryovialId == c.Id &&
                (!request.UsageFromDate.HasValue || (ch.ReadAt.HasValue && ch.ReadAt.Value >= request.UsageFromDate.Value)) &&
                (!request.UsageToDate.HasValue || (ch.ReadAt.HasValue && ch.ReadAt.Value <= request.UsageToDate.Value))
            ));
        }

        return query;
    }

    private static IQueryable<Cryovial> ApplySort(IQueryable<Cryovial> query, ReferenceStrainSearchRequest request) =>
        request.SortBy switch
        {
            "Code" => request.SortDescending ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code),
            "StrainName" => request.SortDescending ? query.OrderByDescending(c => c.Organism != null ? c.Organism.ScientificName : c.OrganismNameSnapshot) : query.OrderBy(c => c.Organism != null ? c.Organism.ScientificName : c.OrganismNameSnapshot),
            "ExpiryDate" => request.SortDescending ? query.OrderByDescending(c => c.ExpiryDate) : query.OrderBy(c => c.ExpiryDate),
            "VialsRemaining" => request.SortDescending ? query.OrderByDescending(c => c.VialsRemaining) : query.OrderBy(c => c.VialsRemaining),
            "ApprovalStatus" => request.SortDescending ? query.OrderByDescending(c => c.ApprovalStatus) : query.OrderBy(c => c.ApprovalStatus),
            _ => request.SortDescending ? query.OrderByDescending(c => c.PreparedAt) : query.OrderBy(c => c.PreparedAt)
        };
}
