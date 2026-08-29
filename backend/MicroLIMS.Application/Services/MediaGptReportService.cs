using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class MediaGptReportService
{
    private readonly MicroLimsDbContext _db;

    public MediaGptReportService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<MediaGptSearchResult> SearchAsync(MediaGptSearchRequest request)
    {
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 200);
        var page = request.Page <= 0 ? 1 : request.Page;

        var baseQuery = BuildFilteredQuery(request);
        var totalCount = await baseQuery.CountAsync();

        var sortedQuery = ApplySort(baseQuery, request);
        var mediaLots = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(m => m.Material)
            .ToListAsync();

        var mediaIds = mediaLots.Select(m => m.Id).ToList();
        var evaluations = await _db.MediaEvaluations
            .Where(e => mediaIds.Contains(e.MediaId))
            .Include(e => e.Challenges)
            .ToListAsync();

        var evalMap = evaluations.ToDictionary(e => e.MediaId);

        var userIds = new HashSet<int>();
        foreach (var m in mediaLots)
        {
            if (m.PreparedByUserId > 0) userIds.Add(m.PreparedByUserId);
            if (m.ApprovedByUserId.HasValue) userIds.Add(m.ApprovedByUserId.Value);
        }

        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        string NameOf(int id) => names.TryGetValue(id, out var n) ? n : (id == 0 ? "Not recorded" : "Unknown");

        var items = mediaLots.Select(m =>
        {
            evalMap.TryGetValue(m.Id, out var eval);
            var challengeCount = eval?.Challenges.Count ?? 0;
            var conformedCount = eval?.Challenges.Count(c => c.Outcome == EvaluationOutcome.Conform) ?? 0;

            return new MediaGptListDto(
                Id: m.Id,
                LotNumber: m.LotNumber,
                MediaType: m.Material?.MaterialName ?? string.Empty,
                PreparedAt: m.PreparedAt,
                ExpiryDate: m.ExpiryDate,
                EvaluationType: eval?.EvaluationType.ToString() ?? EvaluationType.GrowthPromotion.ToString(),
                EvaluationStatus: eval?.Status.ToString() ?? MediaEvaluationStatus.Assigned.ToString(),
                EvaluationOutcome: eval?.Outcome?.ToString(),
                ApprovalStatus: m.ApprovalStatus.ToString(),
                IsReleasedForUse: m.IsReleasedForUse,
                PreparedByName: NameOf(m.PreparedByUserId),
                ApprovedByName: m.ApprovedByUserId.HasValue ? NameOf(m.ApprovedByUserId.Value) : null,
                ApprovedAt: m.ApprovedAt,
                ChallengeCount: challengeCount,
                ConformedChallengeCount: conformedCount
            );
        }).ToList();

        return new MediaGptSearchResult(items, totalCount, page, pageSize);
    }

    public async Task<MediaGptDetailDto?> GetDetailAsync(int mediaId)
    {
        var media = await _db.Media
            .Include(m => m.Material)
            .Include(m => m.AutoclaveEquipment)
            .FirstOrDefaultAsync(m => m.Id == mediaId);

        if (media is null) return null;

        var evaluation = await _db.MediaEvaluations
            .Include(e => e.Challenges).ThenInclude(c => c.Organism)
            .Include(e => e.Challenges).ThenInclude(c => c.Cryovial)
            .Include(e => e.Challenges).ThenInclude(c => c.LyophilizedDisk)
            .Include(e => e.Challenges).ThenInclude(c => c.ReferenceMedia)
            .FirstOrDefaultAsync(e => e.MediaId == mediaId);

        var configRanges = await GetMediaConfigurationRangesAsync();
        var materialName = media.Material?.MaterialName ?? string.Empty;
        configRanges.TryGetValue(materialName, out var range);

        var userIds = new HashSet<int>();
        if (media.PreparedByUserId > 0) userIds.Add(media.PreparedByUserId);
        if (media.ApprovedByUserId.HasValue) userIds.Add(media.ApprovedByUserId.Value);
        if (evaluation?.CompletedByUserId.HasValue == true) userIds.Add(evaluation.CompletedByUserId.Value);

        if (evaluation != null)
        {
            foreach (var c in evaluation.Challenges)
            {
                if (c.ReadByUserId.HasValue) userIds.Add(c.ReadByUserId.Value);
            }
        }

        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        string NameOf(int id) => names.TryGetValue(id, out var n) ? n : (id == 0 ? "Not recorded" : "Unknown");

        var challenges = (evaluation?.Challenges ?? new List<MediaEvaluationChallenge>()).Select(c =>
        {
            var strainSource = c.Cryovial != null
                ? $"Cryovial {c.Cryovial.Code}"
                : c.LyophilizedDisk != null
                    ? $"{c.LyophilizedDisk.MaterialName} (batch {c.LyophilizedDisk.BatchNumber})"
                    : null;

            return new MediaGptChallengeDetailDto(
                Id: c.Id,
                OrganismName: c.Organism?.ScientificName ?? string.Empty,
                AtccNumber: c.Organism?.AtccNumber,
                ChallengeRole: c.ChallengeRole?.ToString(),
                StrainSource: strainSource,
                InitialInoculum: c.InitialInoculum,
                OldMediaCount: c.OldMediaCount,
                NewMediaCount: c.NewMediaCount,
                RecoveryPercent: c.RecoveryPercent,
                ExpectedMinRecoveryPercent: range.Min,
                ExpectedMaxRecoveryPercent: range.Max,
                ReferenceMediaLot: c.ReferenceMedia?.LotNumber ?? c.ReferenceMediaLabel,
                GrowthObserved: c.GrowthObserved,
                ObservedDescription: c.ObservedDescription,
                ExpectedDescription: c.ExpectedDescription,
                IsTurbid: c.IsTurbid,
                Outcome: c.Outcome?.ToString(),
                ReadByName: c.ReadByUserId.HasValue ? NameOf(c.ReadByUserId.Value) : null,
                ReadAt: c.ReadAt
            );
        }).ToList();

        return new MediaGptDetailDto(
            Id: media.Id,
            LotNumber: media.LotNumber,
            MediaType: media.Material?.MaterialName ?? string.Empty,
            ManufacturerName: media.ManufacturerName,
            ManufacturerLot: media.ManufacturerLot,
            TotalWeight: media.TotalWeight,
            TotalVolume: media.TotalVolume,
            AutoclaveName: media.AutoclaveEquipment?.Name,
            AutoclaveProgram: media.AutoclaveProgram,
            LoadType: media.LoadType,
            Temperature: media.Temperature,
            CycleTime: media.CycleTime,
            CycleNumber: media.CycleNumber,
            Ph: media.Ph,
            PreparedAt: media.PreparedAt,
            ExpiryDate: media.ExpiryDate,
            PreparedByName: NameOf(media.PreparedByUserId),
            ApprovalStatus: media.ApprovalStatus.ToString(),
            IsReleasedForUse: media.IsReleasedForUse,
            ApprovedByName: media.ApprovedByUserId.HasValue ? NameOf(media.ApprovedByUserId.Value) : null,
            ApprovedAt: media.ApprovedAt,
            EvaluationType: evaluation?.EvaluationType.ToString() ?? EvaluationType.GrowthPromotion.ToString(),
            EvaluationStatus: evaluation?.Status.ToString() ?? MediaEvaluationStatus.Assigned.ToString(),
            EvaluationOutcome: evaluation?.Outcome?.ToString(),
            EvaluationCompletedAt: evaluation?.CompletedAt,
            EvaluationCompletedByName: evaluation?.CompletedByUserId.HasValue == true ? NameOf(evaluation.CompletedByUserId.Value) : null,
            Challenges: challenges
        );
    }

    public async Task<MediaGptSummaryDto> GetSummaryAsync(DateTime? fromDate, DateTime? toDate, string? mediaType)
    {
        var query = _db.Media.Include(m => m.Material).AsQueryable();

        if (fromDate.HasValue) query = query.Where(m => m.PreparedAt >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(m => m.PreparedAt <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(mediaType))
            query = query.Where(m => m.Material != null && m.Material.MaterialName == mediaType);

        var mediaList = await query.ToListAsync();
        var mediaIds = mediaList.Select(m => m.Id).ToList();

        var evaluations = await _db.MediaEvaluations
            .Where(e => mediaIds.Contains(e.MediaId))
            .ToListAsync();

        var evalMap = evaluations.ToDictionary(e => e.MediaId);

        var grouped = mediaList
            .GroupBy(m => m.Material?.MaterialName ?? "Unknown Media")
            .Select(g =>
            {
                var typeName = g.Key;
                var total = g.Count();
                var conformed = g.Count(m => evalMap.TryGetValue(m.Id, out var ev) && ev.Outcome == EvaluationOutcome.Conform);
                var nonConformed = g.Count(m => evalMap.TryGetValue(m.Id, out var ev) && ev.Outcome == EvaluationOutcome.NonConform);
                var pending = total - conformed - nonConformed;
                var completed = conformed + nonConformed;
                var passRate = completed > 0 ? Math.Round((double)conformed / completed * 100, 1) : 0.0;

                return new MediaGptSummaryItemDto(
                    MediaType: typeName,
                    TotalLots: total,
                    ConformedLots: conformed,
                    NonConformedLots: nonConformed,
                    PendingLots: pending,
                    PassRatePercent: passRate
                );
            })
            .OrderByDescending(s => s.TotalLots)
            .ToList();

        var totalLots = mediaList.Count;
        var totalConformed = mediaList.Count(m => evalMap.TryGetValue(m.Id, out var ev) && ev.Outcome == EvaluationOutcome.Conform);
        var totalNonConformed = mediaList.Count(m => evalMap.TryGetValue(m.Id, out var ev) && ev.Outcome == EvaluationOutcome.NonConform);
        var totalPending = totalLots - totalConformed - totalNonConformed;
        var totalCompleted = totalConformed + totalNonConformed;
        var overallPassRate = totalCompleted > 0 ? Math.Round((double)totalConformed / totalCompleted * 100, 1) : 0.0;

        return new MediaGptSummaryDto(
            TotalLots: totalLots,
            TotalConformed: totalConformed,
            TotalNonConformed: totalNonConformed,
            TotalPending: totalPending,
            OverallPassRatePercent: overallPassRate,
            MediaTypes: grouped
        );
    }

    public async Task<MediaGptExportResult> GetForExportAsync(MediaGptSearchRequest request, int maxRows)
    {
        var baseQuery = ApplySort(BuildFilteredQuery(request), request);
        var mediaLots = await baseQuery
            .Include(m => m.Material)
            .ToListAsync();

        var mediaIds = mediaLots.Select(m => m.Id).ToList();
        var evaluations = await _db.MediaEvaluations
            .Where(e => mediaIds.Contains(e.MediaId))
            .Include(e => e.Challenges).ThenInclude(c => c.Organism)
            .Include(e => e.Challenges).ThenInclude(c => c.Cryovial)
            .Include(e => e.Challenges).ThenInclude(c => c.LyophilizedDisk)
            .Include(e => e.Challenges).ThenInclude(c => c.ReferenceMedia)
            .ToListAsync();

        var evalMap = evaluations.ToDictionary(e => e.MediaId);
        var configRanges = await GetMediaConfigurationRangesAsync();

        // Collect user names
        var userIds = new HashSet<int>();
        foreach (var m in mediaLots)
        {
            if (m.PreparedByUserId > 0) userIds.Add(m.PreparedByUserId);
            if (m.ApprovedByUserId.HasValue) userIds.Add(m.ApprovedByUserId.Value);
        }
        foreach (var ev in evaluations)
        {
            if (ev.CompletedByUserId.HasValue) userIds.Add(ev.CompletedByUserId.Value);
            foreach (var c in ev.Challenges)
            {
                if (c.ReadByUserId.HasValue) userIds.Add(c.ReadByUserId.Value);
            }
        }

        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        string NameOf(int id) => names.TryGetValue(id, out var n) ? n : (id == 0 ? "Not recorded" : "Unknown");

        var rows = new List<MediaGptExportRowDto>();

        foreach (var m in mediaLots)
        {
            evalMap.TryGetValue(m.Id, out var eval);
            var materialName = m.Material?.MaterialName ?? string.Empty;
            configRanges.TryGetValue(materialName, out var range);
            var expectedRangeStr = (range.Min.HasValue && range.Max.HasValue)
                ? $"{range.Min:0.#}% - {range.Max:0.#}%"
                : "-";

            var challenges = eval?.Challenges ?? new List<MediaEvaluationChallenge>();

            if (challenges.Count == 0)
            {
                rows.Add(new MediaGptExportRowDto(
                    LotNumber: m.LotNumber,
                    MediaType: materialName,
                    PreparedAt: m.PreparedAt,
                    ExpiryDate: m.ExpiryDate,
                    ApprovalStatus: m.ApprovalStatus.ToString(),
                    IsReleasedForUse: m.IsReleasedForUse,
                    PreparedByName: NameOf(m.PreparedByUserId),
                    ApprovedByName: m.ApprovedByUserId.HasValue ? NameOf(m.ApprovedByUserId.Value) : null,
                    ApprovedAt: m.ApprovedAt,
                    EvaluationType: eval?.EvaluationType.ToString() ?? EvaluationType.GrowthPromotion.ToString(),
                    EvaluationStatus: eval?.Status.ToString() ?? MediaEvaluationStatus.Assigned.ToString(),
                    EvaluationOutcome: eval?.Outcome?.ToString(),
                    EvaluationCompletedAt: eval?.CompletedAt,
                    OrganismName: "-",
                    AtccNumber: null,
                    ChallengeRole: null,
                    StrainSource: null,
                    InitialInoculum: "-",
                    ReferenceMediaLot: null,
                    OldMediaCount: null,
                    NewMediaCount: null,
                    RecoveryPercent: null,
                    ExpectedRecoveryRange: expectedRangeStr,
                    GrowthObserved: null,
                    ObservedDescription: null,
                    ExpectedDescription: null,
                    IsTurbid: null,
                    ChallengeOutcome: null,
                    ReadByName: null,
                    ReadAt: null
                ));
            }
            else
            {
                foreach (var c in challenges)
                {
                    var strainSource = c.Cryovial != null
                        ? $"Cryovial {c.Cryovial.Code}"
                        : c.LyophilizedDisk != null
                            ? $"{c.LyophilizedDisk.MaterialName} (batch {c.LyophilizedDisk.BatchNumber})"
                            : null;

                    rows.Add(new MediaGptExportRowDto(
                        LotNumber: m.LotNumber,
                        MediaType: materialName,
                        PreparedAt: m.PreparedAt,
                        ExpiryDate: m.ExpiryDate,
                        ApprovalStatus: m.ApprovalStatus.ToString(),
                        IsReleasedForUse: m.IsReleasedForUse,
                        PreparedByName: NameOf(m.PreparedByUserId),
                        ApprovedByName: m.ApprovedByUserId.HasValue ? NameOf(m.ApprovedByUserId.Value) : null,
                        ApprovedAt: m.ApprovedAt,
                        EvaluationType: eval?.EvaluationType.ToString() ?? EvaluationType.GrowthPromotion.ToString(),
                        EvaluationStatus: eval?.Status.ToString() ?? MediaEvaluationStatus.Assigned.ToString(),
                        EvaluationOutcome: eval?.Outcome?.ToString(),
                        EvaluationCompletedAt: eval?.CompletedAt,
                        OrganismName: c.Organism?.ScientificName ?? "-",
                        AtccNumber: c.Organism?.AtccNumber,
                        ChallengeRole: c.ChallengeRole?.ToString(),
                        StrainSource: strainSource,
                        InitialInoculum: c.InitialInoculum,
                        ReferenceMediaLot: c.ReferenceMedia?.LotNumber ?? c.ReferenceMediaLabel,
                        OldMediaCount: c.OldMediaCount,
                        NewMediaCount: c.NewMediaCount,
                        RecoveryPercent: c.RecoveryPercent,
                        ExpectedRecoveryRange: expectedRangeStr,
                        GrowthObserved: c.GrowthObserved,
                        ObservedDescription: c.ObservedDescription,
                        ExpectedDescription: c.ExpectedDescription,
                        IsTurbid: c.IsTurbid,
                        ChallengeOutcome: c.Outcome?.ToString(),
                        ReadByName: c.ReadByUserId.HasValue ? NameOf(c.ReadByUserId.Value) : null,
                        ReadAt: c.ReadAt
                    ));
                }
            }
        }

        if (rows.Count > maxRows)
        {
            return new MediaGptExportResult(new List<MediaGptExportRowDto>(), rows.Count, Exceeded: true);
        }

        return new MediaGptExportResult(rows, rows.Count, Exceeded: false);
    }

    public async Task<MediaGptFilterOptionsDto> GetFilterOptionsAsync()
    {
        var mediaTypes = await _db.Media
            .Where(m => m.Material != null)
            .Select(m => m.Material!.MaterialName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        var evalTypes = Enum.GetNames(typeof(EvaluationType)).ToList();

        return new MediaGptFilterOptionsDto(mediaTypes, evalTypes);
    }

    private IQueryable<Media> BuildFilteredQuery(MediaGptSearchRequest request)
    {
        var query = _db.Media.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(m =>
                m.LotNumber.ToLower().Contains(term) ||
                (m.Material != null && m.Material.MaterialName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.MediaType))
        {
            query = query.Where(m => m.Material != null && m.Material.MaterialName == request.MediaType);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(m => m.PreparedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(m => m.PreparedAt <= request.ToDate.Value);
        }

        if (request.ApprovalStatus.HasValue)
        {
            query = query.Where(m => m.ApprovalStatus == request.ApprovalStatus.Value);
        }

        if (request.EvaluationType.HasValue)
        {
            var evalType = request.EvaluationType.Value;
            query = query.Where(m => _db.MediaEvaluations.Any(e => e.MediaId == m.Id && e.EvaluationType == evalType));
        }

        if (request.Outcome.HasValue)
        {
            var outcome = request.Outcome.Value;
            query = query.Where(m => _db.MediaEvaluations.Any(e => e.MediaId == m.Id && e.Outcome == outcome));
        }

        return query;
    }

    private static IQueryable<Media> ApplySort(IQueryable<Media> query, MediaGptSearchRequest request) =>
        request.SortBy switch
        {
            "LotNumber" => request.SortDescending ? query.OrderByDescending(m => m.LotNumber) : query.OrderBy(m => m.LotNumber),
            "MediaType" => request.SortDescending ? query.OrderByDescending(m => m.Material != null ? m.Material.MaterialName : "") : query.OrderBy(m => m.Material != null ? m.Material.MaterialName : ""),
            "ExpiryDate" => request.SortDescending ? query.OrderByDescending(m => m.ExpiryDate) : query.OrderBy(m => m.ExpiryDate),
            "ApprovalStatus" => request.SortDescending ? query.OrderByDescending(m => m.ApprovalStatus) : query.OrderBy(m => m.ApprovalStatus),
            _ => request.SortDescending ? query.OrderByDescending(m => m.PreparedAt) : query.OrderBy(m => m.PreparedAt)
        };

    private async Task<Dictionary<string, (decimal? Min, decimal? Max)>> GetMediaConfigurationRangesAsync()
    {
        var configs = await _db.MediaConfigurations
            .OrderBy(c => c.Id)
            .ToListAsync();

        var dict = new Dictionary<string, (decimal? Min, decimal? Max)>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in configs)
        {
            if (!dict.ContainsKey(c.Name))
            {
                dict[c.Name] = (c.RecoveryPercentMin, c.RecoveryPercentMax);
            }
        }
        return dict;
    }
}
