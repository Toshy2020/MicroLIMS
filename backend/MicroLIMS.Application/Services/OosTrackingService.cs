using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Read-only query behind the OOS tracking page - groups all retest spin-off samples
// and root samples by OosGroupCode.
public class OosTrackingService
{
    private readonly MicroLimsDbContext _db;

    public OosTrackingService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<OosGroupDto>> GetOosGroupsAsync()
    {
        var samples = await _db.Samples
            .Where(s => s.OosGroupCode != null)
            .Include(s => s.OriginSample)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.TestOrders)
            .ToListAsync();

        if (samples.Count == 0)
            return new List<OosGroupDto>();

        var groupsWithDocs = (await _db.OosInvestigationDocuments
            .Where(d => d.Status != MaterialDocumentStatus.Voided)
            .Select(d => d.OosGroupCode)
            .Distinct()
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var analystIds = samples
            .SelectMany(s => s.TestOrders.Select(t => t.AssignedAnalystId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var analystNames = await _db.Users
            .Where(u => analystIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var grouped = samples
            .GroupBy(s => s.OosGroupCode!)
            .Select(g =>
            {
                var groupSamples = g.ToList();
                // The root sample is the ancestor with no OriginSampleId (or lowest Id if missing)
                var root = groupSamples.FirstOrDefault(s => s.OriginSampleId == null)
                    ?? groupSamples.OrderBy(s => s.Id).First();

                var retestSamples = groupSamples
                    .Where(s => s.Id != root.Id)
                    .OrderBy(s => s.ReceivedAt)
                    .Select(s => new OosTrackingEntryDto
                    {
                        NewSampleId = s.Id,
                        NewReferenceNumber = s.ReferenceNumber,
                        NewSampleStatus = s.Status.ToString(),
                        OriginSampleId = s.OriginSampleId ?? root.Id,
                        OriginReferenceNumber = s.OriginSample?.ReferenceNumber ?? root.ReferenceNumber,
                        OriginSampleStatus = s.OriginSample?.Status.ToString() ?? root.Status.ToString(),
                        DisplayName = ResolveDisplayName(s),
                        Category = s.Category.ToString(),
                        BatchNumber = s.BatchNumber,
                        RetestType = s.OriginSample?.ApprovalDecision?.ToString() ?? string.Empty,
                        TestCodes = s.TestOrders.Select(t => t.TestCode).Distinct().ToList(),
                        AnalystNames = s.TestOrders
                            .Where(t => t.AssignedAnalystId.HasValue)
                            .Select(t => analystNames.TryGetValue(t.AssignedAnalystId!.Value, out var name) ? name : $"User #{t.AssignedAnalystId}")
                            .Distinct()
                            .ToList(),
                        OpenedAt = s.ReceivedAt
                    })
                    .ToList();

                return new OosGroupDto
                {
                    OosGroupCode = g.Key,
                    OriginSampleId = root.Id,
                    OriginReferenceNumber = root.ReferenceNumber,
                    OriginSampleStatus = root.Status.ToString(),
                    DisplayName = ResolveDisplayName(root),
                    Category = root.Category.ToString(),
                    BatchNumber = root.BatchNumber,
                    OpenedAt = root.ReceivedAt,
                    HasInvestigationDocument = groupsWithDocs.Contains(g.Key),
                    RetestSamples = retestSamples
                };
            })
            .OrderByDescending(g => g.OpenedAt)
            .ToList();

        return grouped;
    }

    private static string ResolveDisplayName(Sample s) => s.Category switch
    {
        SampleCategory.AfterCleaning => s.Machine?.Name ?? string.Empty,
        SampleCategory.Water => s.WaterSamplingPoint?.Code ?? string.Empty,
        SampleCategory.EnvironmentalMonitoring => s.Department?.Name ?? string.Empty,
        _ => s.Item?.Name ?? string.Empty
    };
}
