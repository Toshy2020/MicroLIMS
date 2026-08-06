using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record TodaysWorkTestDto(int TestOrderId, string TestCode, string Status, string? TimeRemaining);
public record TodaysWorkItemDto(int SampleId, string ReferenceNumber, string Category, string DisplayName, DateTime ReceivedAt, string OverallStatus, string NextAction, List<TodaysWorkTestDto> Tests);
public record IncubationOverviewDto(string TestCode, int ReadyToRead, int Incubating);

// Five widgets from the gap analysis, shown to every role (an Analyst's
// "Pending Tests" is their own queue; a Reviewer's is everyone's - see
// the per-role filtering below). Delayed = still not ready 24h+ after
// the sample was received.
public class DashboardService
{
    private static readonly TimeSpan DelayThreshold = TimeSpan.FromHours(24);

    private readonly MicroLimsDbContext _db;

    public DashboardService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<object> GetSummaryAsync(RoleType role, int userId)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.Subtract(DelayThreshold);
        var todayStart = now.Date;

        var pendingQuery = _db.TestOrders.Where(t => t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress);
        if (role == RoleType.Analyst)
            pendingQuery = pendingQuery.Where(t => t.AssignedAnalystId == userId);

        var pendingTests = await pendingQuery.CountAsync();

        var delayedTests = await _db.TestOrders
            .Where(t => (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress))
            .Where(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt < cutoff))
            .CountAsync();

        var samplesToday = await _db.Samples.CountAsync(s => s.ReceivedAt >= todayStart);
        var reviewerQueue = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.ResultEntered);
        var approvalQueue = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Reviewed);
        var preparationQueue = await _db.Samples.CountAsync(s => s.PreparationStatus == SamplePreparationStatus.NeedsPreparation);

        // Open incubations split ready-vs-still-incubating - feeds the KPI
        // strip's "Incubating" / "Ready to Read" tiles (same open-incubation
        // definition GetIncubationOverviewAsync groups by test code).
        var openIncubationReadings = await _db.Incubations
            .Where(i => i.CompletedAt == null && i.TestOrderId != null)
            .Select(i => i.ExpectedReadingAt)
            .ToListAsync();
        var readyToReadCount = openIncubationReadings.Count(r => r != null && r <= now);
        var incubatingCount = openIncubationReadings.Count(r => r == null || r > now);

        return new
        {
            pendingTests,
            delayedTests,
            samplesToday,
            reviewerQueue,
            approvalQueue,
            preparationQueue,
            incubatingCount,
            readyToReadCount
        };
    }

    // "Today's Laboratory Work" table - today's samples with a computed
    // NextAction per TestOrder status and TimeRemaining from the nearest
    // open Incubation. Analyst role scopes to their own assigned tests,
    // same filtering rule as GetSummaryAsync's pendingTests above.
    public async Task<List<TodaysWorkItemDto>> GetTodaysWorkAsync(RoleType role, int userId)
    {
        var todayStart = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        var query = _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.TestOrders).ThenInclude(t => t.Incubations)
            .Where(s => s.ReceivedAt >= todayStart)
            .AsQueryable();

        if (role == RoleType.Analyst)
            query = query.Where(s => s.TestOrders.Any(t => t.AssignedAnalystId == userId));

        var samples = await query.OrderByDescending(s => s.ReceivedAt).ToListAsync();

        return samples.Select(s =>
        {
            var tests = s.TestOrders.Select(t =>
            {
                var openIncubation = t.Incubations
                    .Where(i => i.CompletedAt == null && i.ExpectedReadingAt != null)
                    .OrderBy(i => i.ExpectedReadingAt)
                    .FirstOrDefault();
                var timeRemaining = openIncubation?.ExpectedReadingAt is { } readyAt ? FormatTimeRemaining(readyAt, now) : null;
                return new TodaysWorkTestDto(t.Id, t.TestCode, t.Status.ToString(), timeRemaining);
            }).ToList();

            var worst = s.TestOrders.OrderBy(t => StatusRank(t.Status)).FirstOrDefault();
            var overallStatus = worst?.Status.ToString() ?? s.Status.ToString();
            var nextAction = worst is null ? "View Results" : NextActionFor(worst.Status);
            var displayName = s.Item?.Name ?? s.WaterSamplingPoint?.Code ?? s.Department?.Name ?? s.Machine?.Name ?? string.Empty;

            return new TodaysWorkItemDto(s.Id, s.ReferenceNumber, s.Category.ToString(), displayName, s.ReceivedAt, overallStatus, nextAction, tests);
        }).ToList();
    }

    // Open incubations grouped by TestCode, split into ready-to-read vs
    // still-incubating - powers the Incubation Overview widget.
    public async Task<List<IncubationOverviewDto>> GetIncubationOverviewAsync()
    {
        var now = DateTime.UtcNow;
        var openIncubations = await _db.Incubations
            .Where(i => i.CompletedAt == null && i.TestOrderId != null)
            .Include(i => i.TestOrder)
            .ToListAsync();

        return openIncubations
            .Where(i => i.TestOrder != null)
            .GroupBy(i => i.TestOrder!.TestCode)
            .Select(g => new IncubationOverviewDto(
                g.Key,
                g.Count(i => i.ExpectedReadingAt != null && i.ExpectedReadingAt <= now),
                g.Count(i => i.ExpectedReadingAt == null || i.ExpectedReadingAt > now)))
            .OrderByDescending(x => x.ReadyToRead + x.Incubating)
            .ToList();
    }

    private static int StatusRank(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Pending => 0,
        ApprovalStatus.InProgress => 1,
        ApprovalStatus.RetestRequested => 2,
        ApprovalStatus.ResultEntered => 3,
        ApprovalStatus.Reviewed => 4,
        ApprovalStatus.Rejected => 5,
        ApprovalStatus.Approved => 6,
        _ => 7
    };

    private static string NextActionFor(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Pending or ApprovalStatus.InProgress => "Continue Testing",
        ApprovalStatus.RetestRequested => "Retest Required",
        ApprovalStatus.ResultEntered => "Send to Review",
        ApprovalStatus.Reviewed => "Awaiting Approval",
        _ => "View Results"
    };

    // Once ExpectedReadingAt has passed the plate is simply ready to read
    // - that's an expected, routine state, not the same "Overdue" concept
    // delayedTests uses (24h+ since the sample was received at all).
    private static string FormatTimeRemaining(DateTime readyAt, DateTime now)
    {
        var delta = readyAt - now;
        if (delta <= TimeSpan.Zero) return "Ready to read";
        return $"{(int)Math.Ceiling(delta.TotalHours)}h left";
    }

    // Samples lodged vs test requests (TestOrders) lodged, per month,
    // for the last N months - powers the trend bar chart.
    public async Task<List<object>> GetMonthlyTrendAsync(int months = 6)
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        var samples = await _db.Samples.Where(s => s.ReceivedAt >= start).Select(s => s.ReceivedAt).ToListAsync();
        var testOrders = await _db.TestOrders
            .Where(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt >= start))
            .Join(_db.Samples, t => t.SampleId, s => s.Id, (t, s) => s.ReceivedAt)
            .ToListAsync();

        var result = new List<object>();
        for (var i = 0; i < months; i++)
        {
            var monthStart = start.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);
            result.Add(new
            {
                month = monthStart.ToString("MMM"),
                samplesLodged = samples.Count(d => d >= monthStart && d < monthEnd),
                testsLodged = testOrders.Count(d => d >= monthStart && d < monthEnd)
            });
        }
        return result;
    }

    // Sample category breakdown (Product/RM/PM/Water/EM/AfterCleaning/GPT) - donut chart.
    public async Task<List<object>> GetCategoryDistributionAsync()
    {
        var total = await _db.Samples.CountAsync();
        if (total == 0) return new List<object>();

        var grouped = await _db.Samples.GroupBy(s => s.Category)
            .Select(g => new { category = g.Key.ToString(), count = g.Count() })
            .ToListAsync();

        return grouped.Select(g => (object)new { g.category, g.count, percent = Math.Round(g.count * 100.0 / total, 1) }).ToList();
    }

    // TestOrder status breakdown (Approved / Pending-or-InProgress / Rejected) - donut chart,
    // mirrors "inspected vs non-inspected" from the reference design.
    public async Task<List<object>> GetStatusDistributionAsync()
    {
        var total = await _db.TestOrders.CountAsync();
        if (total == 0) return new List<object>();

        var approved = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Approved);
        var rejected = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Rejected);
        var pending = total - approved - rejected;

        return new List<object>
        {
            new { status = "Approved", count = approved, percent = Math.Round(approved * 100.0 / total, 1) },
            new { status = "Pending", count = pending, percent = Math.Round(pending * 100.0 / total, 1) },
            new { status = "Rejected", count = rejected, percent = Math.Round(rejected * 100.0 / total, 1) }
        };
    }

    // Month-over-month deltas for the KPI cards.
    public async Task<object> GetKpiDeltasAsync()
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var samplesThisMonth = await _db.Samples.CountAsync(s => s.ReceivedAt >= thisMonthStart);
        var samplesLastMonth = await _db.Samples.CountAsync(s => s.ReceivedAt >= lastMonthStart && s.ReceivedAt < thisMonthStart);

        var testsThisMonth = await _db.TestOrders.CountAsync(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt >= thisMonthStart));
        var testsLastMonth = await _db.TestOrders.CountAsync(t => _db.Samples.Any(s => s.Id == t.SampleId && s.ReceivedAt >= lastMonthStart && s.ReceivedAt < thisMonthStart));

        return new
        {
            samplesThisMonth,
            samplesDeltaPercent = samplesLastMonth == 0 ? 0 : Math.Round((samplesThisMonth - samplesLastMonth) * 100.0 / samplesLastMonth, 1),
            testsThisMonth,
            testsDeltaPercent = testsLastMonth == 0 ? 0 : Math.Round((testsThisMonth - testsLastMonth) * 100.0 / testsLastMonth, 1),
            totalSamples = await _db.Samples.CountAsync(),
            totalTests = await _db.TestOrders.CountAsync()
        };
    }
}
