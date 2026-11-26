using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.MyWork.Sources;

public sealed class CompetencyMyWorkSource(CompetencyReminderService reminders) : IMyWorkSource
{
    public string Key => "competency";

    public bool AppliesTo(MyWorkContext ctx) => ctx.Permissions.Contains(PermissionConstants.CompetencyView);

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var rows = await reminders.GetExpiringAsync(ctx.UserId, ct, includeExpired: true);
        var items = rows
            .Select(r =>
            {
                var dueUtc = r.ExpiresOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var urgency = r.DaysRemaining < 0 ? MyWorkUrgencyLevel.Overdue
                    : r.DaysRemaining == 0 ? MyWorkUrgencyLevel.Today
                    : MyWorkUrgencyLevel.Later;
                var context = r.DaysRemaining < 0
                    ? $"Qualification expired on {r.ExpiresOn:d MMM yyyy}"
                    : $"Qualification expires on {r.ExpiresOn:d MMM yyyy}";
                return new MyWorkItem(
                    Key: $"competency:{r.QualificationId}",
                    GroupKey: "competency",
                    Identifier: r.TestCode,
                    Title: r.TestCode,
                    Context: context,
                    DueAtUtc: dueUtc,
                    Urgency: urgency,
                    SortAtUtc: dueUtc,
                    Locked: false,
                    LockReason: null,
                    Target: new MyWorkTarget("competency-qualification"),
                    Actions: Array.Empty<string>(),
                    SectionId: r.SectionId,
                    SectionName: null,
                    Lab: null);
            })
            .OrderBy(i => i.Urgency)
            .ThenBy(i => i.SortAtUtc)
            .Take(take)
            .ToList();

        var totals = new Dictionary<string, int>();
        if (rows.Count > 0) totals["competency"] = rows.Count;
        return new MyWorkSourceResult(items, totals, new Dictionary<string, int>());
    }

    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var rows = await reminders.GetExpiringAsync(ctx.UserId, ct, includeExpired: true);
        var counts = new Dictionary<MyWorkUrgencyLevel, int>();
        foreach (var r in rows)
        {
            var urgency = r.DaysRemaining < 0 ? MyWorkUrgencyLevel.Overdue
                : r.DaysRemaining == 0 ? MyWorkUrgencyLevel.Today
                : MyWorkUrgencyLevel.Later;
            counts[urgency] = counts.GetValueOrDefault(urgency) + 1;
        }
        return counts;
    }
}
