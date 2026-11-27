using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services.Competency;

public sealed record CompetencyExpiryReminder(
    int QualificationId, int UserId, string UserName, int SectionId,
    int TestDefinitionId, string TestCode, DateOnly ExpiresOn, int DaysRemaining);

// Expiry reminders for My Work and the existing C5 notification scheduler. Only sections
// with a configured reminder window are included; "now" is always the lab date.
public sealed class CompetencyReminderService(IMicroLimsDbContext db, ILabClock clock)
{
    public async Task<IReadOnlyList<CompetencyExpiryReminder>> GetExpiringAsync(
        int? userId = null, CancellationToken cancellationToken = default, bool includeExpired = false)
    {
        var labToday = clock.LabToday;
        var windows = await db.CompetencySectionSettings.AsNoTracking()
            .Where(setting => setting.ReminderDaysBeforeExpiry != null)
            .ToDictionaryAsync(setting => setting.SectionId,
                setting => setting.ReminderDaysBeforeExpiry!.Value, cancellationToken);
        if (windows.Count == 0 && !includeExpired)
            return [];

        var sectionIds = windows.Keys.ToArray();
        var tests = await db.TestDefinitions.AsNoTracking()
            .Where(test => includeExpired || sectionIds.Contains(test.SectionId))
            .Select(test => new { test.Id, test.SectionId, test.Code })
            .ToListAsync(cancellationToken);
        if (tests.Count == 0)
            return [];

        var testIds = tests.Select(test => test.Id).ToArray();
        var qualifications = await db.AnalystQualifications.AsNoTracking()
            .Where(q => testIds.Contains(q.TestDefinitionId)
                && (!userId.HasValue || q.UserId == userId.Value))
            .Select(q => new { q.Id, q.UserId, q.TestDefinitionId, q.ExpiresOn, q.WithdrawnOn })
            .ToListAsync(cancellationToken);
        var latest = qualifications
            .GroupBy(q => new { q.UserId, q.TestDefinitionId })
            .ToDictionary(group => group.Key, group => group.MaxBy(q => q.Id)!);
        if (latest.Count == 0)
            return [];

        var analystIds = latest.Keys.Select(key => key.UserId).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(user => analystIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        var reminders = new List<CompetencyExpiryReminder>();
        foreach (var (key, qualification) in latest)
        {
            var test = tests.FirstOrDefault(candidate => candidate.Id == key.TestDefinitionId);
            if (test is null
                || qualification.WithdrawnOn is not null // the latest record governs: a withdrawn one never reminds
                || qualification.ExpiresOn is not { } expiresOn)
                continue;

            var isExpired = expiresOn < labToday;
            if (isExpired)
            {
                if (!includeExpired) continue;
            }
            else if (!windows.TryGetValue(test.SectionId, out var days) || expiresOn > labToday.AddDays(days))
            {
                continue;
            }

            reminders.Add(new CompetencyExpiryReminder(
                qualification.Id, key.UserId,
                names.GetValueOrDefault(key.UserId) ?? key.UserId.ToString(),
                test.SectionId, test.Id, test.Code, expiresOn, expiresOn.DayNumber - labToday.DayNumber));
        }
        return reminders.OrderBy(reminder => reminder.ExpiresOn).ThenBy(reminder => reminder.UserId).ToList();
    }
}
