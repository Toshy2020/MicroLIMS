using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

// Cross-laboratory tracking board (GET api/samples/tracking) - every
// sample with at least one non-superseded TestOrder, its OverallStatus
// (SampleSectionRollup.Overall) and each laboratory's own stage.
// Deliberately not scoped to the caller's own section: SamplesTrackAll is
// a whole-lab view, unlike the Testing Workspace's per-analyst queues.
public class SampleTrackingService
{
    private readonly MicroLimsDbContext _db;

    public SampleTrackingService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SampleTrackingRowDto>> GetTrackingAsync(SampleTrackingFilterDto filter)
    {
        int page = filter.Page <= 0 ? 1 : filter.Page;
        // Default 50, hard server-side max 200 - same cap as TestingWorkspaceService.
        int pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, 200);

        var sectionNames = await _db.DocumentSections.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Name);

        var query = _db.Samples.AsNoTracking()
            // A sample only belongs on the board once at least one of its
            // TestOrders is still live - a sample whose every TestOrder
            // moved to a retest (fully superseded) has nothing left to track.
            .Where(s => s.TestOrders.Any(t => !t.IsSuperseded));

        if (filter.LabSectionId is int labSectionId)
            query = query.Where(s => s.TestOrders.Any(t => t.SectionId == labSectionId && !t.IsSuperseded));

        if (filter.ItemId is int itemId)
            query = query.Where(s => s.ItemId == itemId);

        if (!string.IsNullOrWhiteSpace(filter.From) &&
            DateTime.TryParse(filter.From, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var fromDt))
        {
            var fromUtc = DateTime.SpecifyKind(fromDt.Date, DateTimeKind.Utc);
            query = query.Where(s => s.ReceivedAt >= fromUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.To) &&
            DateTime.TryParse(filter.To, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var toDt))
        {
            var toUtcInclusiveEnd = DateTime.SpecifyKind(toDt.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(s => s.ReceivedAt < toUtcInclusiveEnd);
        }

        query = query
            .Include(s => s.Item)
            .Include(s => s.Machine)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.WaterDepartment)
            .Include(s => s.Department)
            .Include(s => s.TestOrders)
            .Include(s => s.SectionSignoffs)
            .OrderByDescending(s => s.ReceivedAt);

        int totalCount;
        List<Sample> pageSamples;

        OverallSampleStatus parsedOverall = default;
        bool hasOverallFilter = !string.IsNullOrWhiteSpace(filter.Overall) &&
            Enum.TryParse(filter.Overall, true, out parsedOverall);

        if (hasOverallFilter)
        {
            // Overall() is computed in memory from TestOrders/SectionSignoffs
            // (SampleSectionRollup) and can't be translated to SQL - load
            // every date/section/item-filtered candidate, compute Overall,
            // filter, then page in memory. Acceptable at this data size;
            // revisit (denormalise Overall onto Sample) if the tracking
            // board's candidate set grows large.
            var candidates = await query.ToListAsync();
            var matching = candidates.Where(s => SampleSectionRollup.Overall(s) == parsedOverall).ToList();
            totalCount = matching.Count;
            pageSamples = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            totalCount = await query.CountAsync();
            pageSamples = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }

        var items = pageSamples.Select(s => ToRow(s, sectionNames)).ToList();

        return new PagedResult<SampleTrackingRowDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static SampleTrackingRowDto ToRow(Sample s, Dictionary<int, string> sectionNames)
    {
        // Same category -> display text mapping as TestingWorkspaceService's
        // DisplayName, named "Product" here for the tracking board.
        string product = s.Category switch
        {
            SampleCategory.AfterCleaning => s.Machine?.Name ?? string.Empty,
            SampleCategory.Water => s.WaterSamplingPoint?.Code ?? s.WaterDepartment?.Name ?? string.Empty,
            SampleCategory.EnvironmentalMonitoring => s.Department?.Name ?? string.Empty,
            _ => s.Item?.Name ?? string.Empty
        };

        var labs = SampleSectionRollup.SectionIds(s).Select(id => new SampleTrackingLabDto(
            id,
            sectionNames.TryGetValue(id, out var name) ? name : string.Empty,
            StageText(SampleSectionRollup.StatusOf(s, id))
        )).ToList();

        return new SampleTrackingRowDto(
            s.Id, s.ReferenceNumber, product, s.BatchNumber, s.ReceivedAt,
            SampleSectionRollup.Overall(s).ToString(), labs);
    }

    private static string StageText(SectionSignoffStatus status) => status switch
    {
        SectionSignoffStatus.InTesting => "Testing",
        SectionSignoffStatus.Cancelled => "Closed",
        _ => status.ToString()
    };
}
