using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.MyWork;

// Runs every applicable source, merges their items into the fixed groups and orders them.
// Sources run one after another because they share the request DbContext.
public sealed class MyWorkService : IMyWorkService
{
    public const int GroupCap = 50;

    // Exact keys and titles, in display order.
    private static readonly (string Key, string Title)[] GroupDefinitions =
    {
        ("readyToRead", "Ready to read now"),
        ("returned", "Returned to me"),
        ("endingToday", "Incubation ending today"),
        ("next24Hours", "Incubation ending in the next 24 hours"),
        ("toPerform", "Tests to perform"),
        ("review", "Awaiting my review"),
        ("approval", "Awaiting my approval"),
        ("available", "Available in my section"),
        ("unassigned", "Unassigned tests"),
        ("documents", "Documents"),
        ("equipment", "Equipment"),
        ("materials", "Media, solutions and strains"),
    };

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly ILabClock _clock;
    private readonly ISystemSettingsReader _settings;
    private readonly IErrorCaptureService _errors;
    private readonly IReadOnlyList<IMyWorkSource> _sources;

    public MyWorkService(IMicroLimsDbContext db, IUserSectionScopeService scope, ILabClock clock,
        ISystemSettingsReader settings, IErrorCaptureService errors, IEnumerable<IMyWorkSource> sources)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _settings = settings;
        _errors = errors;
        _sources = sources.ToList();
    }

    public async Task<MyWorkResponse> GetAsync(int userId, DashboardLabScope? lab, CancellationToken ct = default)
    {
        var ctx = await BuildContextAsync(userId, lab, ct);
        var items = new List<MyWorkItem>();
        var totals = new Dictionary<string, int>();
        var locked = new Dictionary<string, int>();
        var unavailable = new List<string>();

        foreach (var source in _sources.Where(s => s.AppliesTo(ctx)))
        {
            try
            {
                var result = await source.GetAsync(ctx, GroupCap, ct);
                var unknown = result.Items.FirstOrDefault(i => GroupDefinitions.All(g => g.Key != i.GroupKey));
                if (unknown is not null)
                    throw new BusinessRuleException(
                        MessageCodes.SampleMyWorkUnknownGroup,
                        $"Source '{source.Key}' returned an item for unknown group '{unknown.GroupKey}'.",
                        new Dictionary<string, string> { ["sourceKey"] = source.Key, ["groupKey"] = unknown.GroupKey });

                items.AddRange(result.Items);
                foreach (var (group, count) in result.TotalsByGroup)
                    totals[group] = totals.GetValueOrDefault(group) + count;
                foreach (var (group, count) in result.LockedByGroup)
                    locked[group] = locked.GetValueOrDefault(group) + count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                unavailable.Add(source.Key);
                await CaptureAsync(source, ex, userId, "api/my-work", ct);
            }
        }

        var groups = new List<MyWorkGroup>();
        foreach (var (key, title) in GroupDefinitions)
        {
            var groupItems = items.Where(i => i.GroupKey == key)
                .OrderBy(i => i.Locked)
                .ThenBy(i => i.Urgency)
                .ThenBy(i => i.SortAtUtc)
                .ToList();
            // Count is what the user can act on: locked items (Segregation of Duties) are listed but never counted.
            var count = Math.Max(totals.GetValueOrDefault(key), groupItems.Count(i => !i.Locked));
            var lockedCount = Math.Max(locked.GetValueOrDefault(key), groupItems.Count(i => i.Locked));
            if (count == 0 && lockedCount == 0) continue;

            var shown = groupItems.Take(GroupCap).ToList();
            groups.Add(new MyWorkGroup(key, title, count, lockedCount, shown, HasMore: count + lockedCount > shown.Count));
        }

        return new MyWorkResponse(ctx.NowUtc, _clock.LabTimeZone.Id, groups, unavailable);
    }

    public async Task<MyWorkSummary> GetSummaryAsync(int userId, CancellationToken ct = default)
    {
        var ctx = await BuildContextAsync(userId, null, ct);
        var total = 0;
        var overdue = 0;
        var now = 0;
        var complete = true;

        foreach (var source in _sources.Where(s => s.AppliesTo(ctx)))
        {
            try
            {
                var counts = await source.CountAsync(ctx, ct);
                total += counts.Values.Sum();
                overdue += counts.GetValueOrDefault(MyWorkUrgencyLevel.Overdue);
                now += counts.GetValueOrDefault(MyWorkUrgencyLevel.Now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                complete = false;
                await CaptureAsync(source, ex, userId, "api/my-work/summary", ct);
            }
        }

        // A failed source makes every number a lower bound, so none is reported.
        return complete ? new MyWorkSummary(total, overdue, now, true) : new MyWorkSummary(null, null, null, false);
    }

    private async Task<MyWorkContext> BuildContextAsync(int userId, DashboardLabScope? lab, CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow.UtcDateTime;
        var readable = await _scope.GetReadableSectionIdsAsync(userId, ct);
        var members = await _scope.GetMemberSectionIdsAsync(userId, ct);
        if (lab?.SectionIds is { } labSections)
        {
            // A laboratory view never reaches outside the chosen laboratory sections.
            readable = labSections;
            members = members.Where(labSections.Contains).ToList();
        }

        var permissionCodes = await (
                from u in _db.Users.AsNoTracking()
                join rp in _db.RolePermissions.AsNoTracking() on u.RoleId equals rp.RoleId
                join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                where u.Id == userId && u.IsActive
                select p.Code)
            .ToListAsync(ct);

        return new MyWorkContext(
            userId, permissionCodes.ToHashSet(StringComparer.Ordinal), readable, members, lab, nowUtc, EndOfLabDayUtc(nowUtc),
            await _settings.GetIntAsync(SystemSettingKeys.MyWorkLookAheadDays, ct),
            await _settings.GetIntAsync(SystemSettingKeys.MyWorkReadOverdueHours, ct));
    }

    // The next local midnight in the laboratory time zone, expressed in UTC.
    private DateTime EndOfLabDayUtc(DateTime nowUtc)
    {
        var zone = _clock.LabTimeZone;
        var nextMidnight = DateTime.SpecifyKind(_clock.ToLabLocal(nowUtc).Date.AddDays(1), DateTimeKind.Unspecified);
        // A zone that skips local midnight (daylight saving at 00:00) starts the day at the first valid hour.
        while (zone.IsInvalidTime(nextMidnight))
            nextMidnight = nextMidnight.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(nextMidnight, zone);
    }

    private Task CaptureAsync(IMyWorkSource source, Exception ex, int userId, string path, CancellationToken ct) =>
        _errors.CaptureAsync(new ErrorCaptureRequest(
            ErrorSource.Backend,
            ErrorSeverity.Error,
            Guid.NewGuid().ToString("N"),
            ex.GetType().FullName ?? ex.GetType().Name,
            ex.Message,
            ex.StackTrace,
            RequestPath: path,
            HttpMethod: "GET",
            UserId: userId,
            Summary: $"My Work source '{source.Key}' failed"), ct);
}
