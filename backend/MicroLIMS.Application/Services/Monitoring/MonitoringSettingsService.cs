using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Monitoring;

public record MonitoringSettingsRequest(
    DayOfWeek LabWeekStartDay,
    int WorkingWeekdays,
    int WaterEarlyToleranceDays, int WaterLateToleranceDays,
    int EmEarlyToleranceDays, int EmLateToleranceDays,
    int AcEarlyToleranceDays, int AcLateToleranceDays,
    int WarningDayOfMonth, int GraceDays,
    int MonthlyMinGapDays, int MonthlyMaxGapDays,
    int WeeklyMinGapDays, int WeeklyMaxGapDays);

public record MonitoringSettingsResponse(
    int Id, uint Version,
    DayOfWeek LabWeekStartDay,
    int WorkingWeekdays,
    int WaterEarlyToleranceDays, int WaterLateToleranceDays,
    int EmEarlyToleranceDays, int EmLateToleranceDays,
    int AcEarlyToleranceDays, int AcLateToleranceDays,
    int WarningDayOfMonth, int GraceDays,
    int MonthlyMinGapDays, int MonthlyMaxGapDays,
    int WeeklyMinGapDays, int WeeklyMaxGapDays)
{
    public static MonitoringSettingsResponse From(MonitoringSettings s) => new(
        s.Id, s.Version, s.LabWeekStartDay, s.WorkingWeekdays,
        s.WaterEarlyToleranceDays, s.WaterLateToleranceDays,
        s.EmEarlyToleranceDays, s.EmLateToleranceDays,
        s.AcEarlyToleranceDays, s.AcLateToleranceDays,
        s.WarningDayOfMonth, s.GraceDays,
        s.MonthlyMinGapDays, s.MonthlyMaxGapDays,
        s.WeeklyMinGapDays, s.WeeklyMaxGapDays);
}

public record LabCalendarDateRequest(DateOnly Date, string Description);
public record LabCalendarDateResponse(int Id, DateOnly Date, string Description);
public record LegacyFrequencyImportResponse(int Converted, List<string> Skipped);

// Site-wide settings and the non-working dates of the Annual Monitoring Plan.
public class MonitoringSettingsService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;

    public MonitoringSettingsService(IMicroLimsDbContext db, ILabClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // With no saved row the defaults apply (Id 0).
    public async Task<MonitoringSettingsResponse> GetSettingsAsync() =>
        MonitoringSettingsResponse.From(await _db.MonitoringSettings.AsNoTracking().FirstOrDefaultAsync() ?? new MonitoringSettings());

    public async Task<MonitoringSettingsResponse> SaveSettingsAsync(MonitoringSettingsRequest r, int userId)
    {
        Validate(r);

        var row = await _db.MonitoringSettings.FirstOrDefaultAsync();
        if (row is null)
        {
            row = new MonitoringSettings();
            _db.MonitoringSettings.Add(row);
        }
        else
        {
            RecordVersion.EnsureCurrent(_db, row);
        }

        row.LabWeekStartDay = r.LabWeekStartDay;
        row.WorkingWeekdays = r.WorkingWeekdays;
        row.WaterEarlyToleranceDays = r.WaterEarlyToleranceDays;
        row.WaterLateToleranceDays = r.WaterLateToleranceDays;
        row.EmEarlyToleranceDays = r.EmEarlyToleranceDays;
        row.EmLateToleranceDays = r.EmLateToleranceDays;
        row.AcEarlyToleranceDays = r.AcEarlyToleranceDays;
        row.AcLateToleranceDays = r.AcLateToleranceDays;
        row.WarningDayOfMonth = r.WarningDayOfMonth;
        row.GraceDays = r.GraceDays;
        row.MonthlyMinGapDays = r.MonthlyMinGapDays;
        row.MonthlyMaxGapDays = r.MonthlyMaxGapDays;
        row.WeeklyMinGapDays = r.WeeklyMinGapDays;
        row.WeeklyMaxGapDays = r.WeeklyMaxGapDays;
        row.UpdatedByUserId = userId;
        row.UpdatedAt = _clock.UtcNow.UtcDateTime;
        await _db.SaveChangesAsync();
        return MonitoringSettingsResponse.From(row);
    }

    private static void Validate(MonitoringSettingsRequest r)
    {
        if (r.WorkingWeekdays is < 1 or > 127)
            throw new BusinessRuleException("Choose at least one working weekday.");
        if (!Enum.IsDefined(r.LabWeekStartDay))
            throw new BusinessRuleException("Invalid lab week start day.");
        var tolerances = new[]
        {
            r.WaterEarlyToleranceDays, r.WaterLateToleranceDays, r.EmEarlyToleranceDays,
            r.EmLateToleranceDays, r.AcEarlyToleranceDays, r.AcLateToleranceDays
        };
        if (tolerances.Any(t => t is < 0 or > 14))
            throw new BusinessRuleException("Tolerance days must be between 0 and 14.");
        if (r.WarningDayOfMonth is < 1 or > 28)
            throw new BusinessRuleException("The warning day of the month must be between 1 and 28.");
        if (r.GraceDays is < 0 or > 60)
            throw new BusinessRuleException("Grace days must be between 0 and 60.");
        if (r.MonthlyMinGapDays < 1 || r.MonthlyMinGapDays > r.MonthlyMaxGapDays
            || r.WeeklyMinGapDays < 1 || r.WeeklyMinGapDays > r.WeeklyMaxGapDays)
            throw new BusinessRuleException("A minimum spacing must be at least 1 day and not exceed its maximum.");
    }

    public async Task<List<LabCalendarDateResponse>> GetDatesAsync(int year) =>
        await _db.LabCalendarDates.AsNoTracking()
            .Where(d => d.Date >= new DateOnly(year, 1, 1) && d.Date <= new DateOnly(year, 12, 31))
            .OrderBy(d => d.Date)
            .Select(d => new LabCalendarDateResponse(d.Id, d.Date, d.Description))
            .ToListAsync();

    public async Task<LabCalendarDateResponse> AddDateAsync(LabCalendarDateRequest r)
    {
        if (await _db.LabCalendarDates.AnyAsync(d => d.Date == r.Date))
            throw new BusinessRuleException($"{r.Date:yyyy-MM-dd} is already a non-working date.");

        var row = new LabCalendarDate { Date = r.Date, Description = (r.Description ?? string.Empty).Trim() };
        _db.LabCalendarDates.Add(row);
        await _db.SaveChangesAsync();
        return new(row.Id, row.Date, row.Description);
    }

    public async Task RemoveDateAsync(int id)
    {
        var row = await _db.LabCalendarDates.FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new NotFoundException($"Calendar date {id} not found.");
        _db.LabCalendarDates.Remove(row);
        await _db.SaveChangesAsync();
    }

    // One-off: reads the free-text TestingFrequency of every target that has
    // no structured rule yet. Anything it cannot map is listed, not guessed.
    // Weekly Water points are skipped too - they need a weekday chosen by a person.
    public async Task<LegacyFrequencyImportResponse> ImportLegacyFrequenciesAsync()
    {
        var converted = 0;
        var skipped = new List<string>();

        void Convert(IHasFrequency target, string? text, string label, bool weekdayRequired)
        {
            if (target.FrequencyType is not null) return;
            var rule = LegacyFrequencyParser.Parse(text);
            if (rule is null || FrequencyRules.Validate(rule, weekdayRequired) is not null)
            {
                if (!string.IsNullOrWhiteSpace(text)) skipped.Add($"{label}: '{text}'");
                return;
            }
            FrequencyRules.Apply(target, rule);
            converted++;
        }

        foreach (var d in await _db.Departments.ToListAsync())
            Convert(d, d.TestingFrequency, $"Department {d.Name}", false);
        foreach (var p in await _db.WaterSamplingPoints.ToListAsync())
            Convert(p, p.TestingFrequency, $"Water point {p.Code}", true);

        await _db.SaveChangesAsync();
        return new(converted, skipped);
    }
}
