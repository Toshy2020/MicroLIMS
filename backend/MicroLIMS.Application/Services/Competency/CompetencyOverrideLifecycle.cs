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
            var assignments = orders.ToDictionary(order => order.Id, order => order.AssignedAnalystId);
            active.RemoveAll(row => assignments[row.TestOrderId] is not int assignee || assignee == row.AnalystUserId);
        }

        foreach (var row in active)
        {
            row.EndedAtUtc = now;
            row.EndReason = reason;
        }
    }
}
