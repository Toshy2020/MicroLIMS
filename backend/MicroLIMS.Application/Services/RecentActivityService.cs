using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record ActivityEntryDto(string Type, string Description, int UserId, DateTime Timestamp);

// Merges AuditLog (create/update/delete on any entity) and
// WorkflowHistory (test order step transitions, including approval
// decisions) into a single chronological feed for the dashboard.
public class RecentActivityService
{
    private readonly MicroLimsDbContext _db;

    public RecentActivityService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<ActivityEntryDto>> GetRecentAsync(int take = 25)
    {
        var auditEntries = await _db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .Select(a => new ActivityEntryDto(
                "Audit",
                $"{a.Action} {a.EntityName} #{a.EntityId}",
                a.UserId,
                a.Timestamp))
            .ToListAsync();

        var workflowEntries = await _db.WorkflowHistories
            .OrderByDescending(w => w.Timestamp)
            .Take(take)
            .Select(w => new ActivityEntryDto(
                "Workflow",
                $"TestOrder #{w.TestOrderId}: {w.FromStep} -> {w.ToStep}" + (w.Note != null ? $" ({w.Note})" : ""),
                w.PerformedByUserId,
                w.Timestamp))
            .ToListAsync();

        return auditEntries.Concat(workflowEntries)
            .OrderByDescending(e => e.Timestamp)
            .Take(take)
            .ToList();
    }
}
