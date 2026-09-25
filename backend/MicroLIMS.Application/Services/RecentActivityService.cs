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

    // sectionIds: the viewer's laboratory sections (null = unrestricted).
    // Entries about a sample or test follow its sections; general entries
    // (users, configuration) are shown to everyone as before.
    public async Task<List<ActivityEntryDto>> GetRecentAsync(int take = 25, IReadOnlyCollection<int>? sectionIds = null)
    {
        var audit = _db.AuditLogs.AsQueryable();
        var workflow = _db.WorkflowHistories.AsQueryable();
        if (sectionIds is not null)
        {
            audit = audit.Where(a => (a.TestOrderId == null && a.SampleId == null)
                || (a.TestOrderId != null
                    ? _db.TestOrders.Any(t => t.Id == a.TestOrderId && sectionIds.Contains(t.SectionId))
                    : _db.TestOrders.Any(t => t.SampleId == a.SampleId && sectionIds.Contains(t.SectionId))));
            workflow = workflow.Where(w => _db.TestOrders.Any(t => t.Id == w.TestOrderId && sectionIds.Contains(t.SectionId)));
        }

        var auditEntries = await audit
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .Select(a => new ActivityEntryDto(
                "Audit",
                $"{a.Action} {a.EntityName} #{a.EntityId}",
                a.UserId ?? 0,
                a.Timestamp))
            .ToListAsync();

        var workflowEntries = await workflow
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
