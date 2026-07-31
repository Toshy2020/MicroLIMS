using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record AnalystKpiDto(int UserId, string Username, int CompletedTests, int PendingTests, double AverageTurnaroundHours);
public record CompletionStatsDto(int TotalTestOrders, int Approved, int Rejected, int Pending, double ApprovalRatePercent);
public record DelayTrackingDto(int DelayedCount, double AverageDelayHours);

// Analyst KPIs, delay tracking, completion statistics - gap analysis
// "Missing Laboratory Modules - KPI".
public class KpiService
{
    private static readonly TimeSpan DelayThreshold = TimeSpan.FromHours(24);

    private readonly MicroLimsDbContext _db;

    public KpiService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<AnalystKpiDto>> GetAnalystKpisAsync()
    {
        var analysts = await _db.Users.Include(u => u.Role)
            .Where(u => u.Role!.Type == RoleType.Analyst)
            .ToListAsync();

        var result = new List<AnalystKpiDto>();
        foreach (var analyst in analysts)
        {
            var completed = await _db.TestOrders.CountAsync(t => t.AssignedAnalystId == analyst.Id && t.Status == ApprovalStatus.Approved);
            var pending = await _db.TestOrders.CountAsync(t => t.AssignedAnalystId == analyst.Id &&
                (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress));

            // Average turnaround: time from sample received to result entered,
            // for this analyst's completed test orders.
            var turnaroundHours = await _db.TestOrders
                .Where(t => t.AssignedAnalystId == analyst.Id && t.Status == ApprovalStatus.Approved)
                .Join(_db.Samples, t => t.SampleId, s => s.Id, (t, s) => new { t.Id, s.ReceivedAt })
                .Join(_db.Results, x => x.Id, r => r.TestOrderId, (x, r) => new { x.ReceivedAt, r.EnteredAt })
                .ToListAsync();

            var avgHours = turnaroundHours.Count > 0
                ? turnaroundHours.Average(x => (x.EnteredAt - x.ReceivedAt).TotalHours)
                : 0;

            result.Add(new AnalystKpiDto(analyst.Id, analyst.Username, completed, pending, Math.Round(avgHours, 1)));
        }

        return result;
    }

    public async Task<CompletionStatsDto> GetCompletionStatsAsync()
    {
        var total = await _db.TestOrders.CountAsync();
        var approved = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Approved);
        var rejected = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Rejected);
        var pending = await _db.TestOrders.CountAsync(t => t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress);

        var decided = approved + rejected;
        var approvalRate = decided > 0 ? Math.Round(approved * 100.0 / decided, 1) : 0;

        return new CompletionStatsDto(total, approved, rejected, pending, approvalRate);
    }

    public async Task<DelayTrackingDto> GetDelayTrackingAsync()
    {
        var cutoff = DateTime.UtcNow.Subtract(DelayThreshold);

        var delayed = await _db.TestOrders
            .Where(t => t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)
            .Join(_db.Samples, t => t.SampleId, s => s.Id, (t, s) => s.ReceivedAt)
            .Where(receivedAt => receivedAt < cutoff)
            .ToListAsync();

        var avgDelayHours = delayed.Count > 0
            ? delayed.Average(receivedAt => (DateTime.UtcNow - receivedAt).TotalHours)
            : 0;

        return new DelayTrackingDto(delayed.Count, Math.Round(avgDelayHours, 1));
    }
}
