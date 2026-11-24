using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services.Competency;

internal static class CompetencyOverrideLifecycle
{
    internal static async Task EndAsync(IMicroLimsDbContext db, IEnumerable<TestOrder> orders,
        DateTime now, CompetencyOverrideEndReason reason, CancellationToken ct = default)
    {
        var ids = orders.Select(order => order.Id).ToArray();
        if (ids.Length == 0)
            return;

        var active = await db.CompetencyOverrides
            .Where(row => ids.Contains(row.TestOrderId) && row.EndedAtUtc == null).ToListAsync(ct);
        if (reason == CompetencyOverrideEndReason.Reassigned)
        {
            var assignments = new Dictionary<int, int?>();
            foreach (var order in orders)
                assignments[order.Id] = order.AssignedAnalystId;
            active.RemoveAll(row => !assignments.TryGetValue(row.TestOrderId, out var assignee) || assignee is null || assignee == row.AnalystUserId);
        }

        foreach (var row in active)
        {
            row.EndedAtUtc = now;
            row.EndReason = reason;
        }
    }
}
