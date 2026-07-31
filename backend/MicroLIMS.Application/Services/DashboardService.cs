using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

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
        var cutoff = DateTime.UtcNow.Subtract(DelayThreshold);
        var todayStart = DateTime.UtcNow.Date;

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

        return new
        {
            pendingTests,
            delayedTests,
            samplesToday,
            reviewerQueue,
            approvalQueue
        };
    }

    // Samples lodged vs test requests (TestOrders) lodged, per month,
    // for the last N months - powers the trend bar chart.
    public async Task<List<object>> GetMonthlyTrendAsync(int months = 6)
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));

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
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
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
