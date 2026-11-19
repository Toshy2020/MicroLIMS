using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Notifications;

public sealed record WorkingHoursDayDto(
    DayOfWeek DayOfWeek,
    bool IsWorkingDay,
    int StartMinute,
    int EndMinute);

public sealed record WorkingHoursDto(
    string TimeZoneId,
    string TimeZoneDisplayName,
    bool IsConfigured,
    IReadOnlyList<WorkingHoursDayDto> Days);

public sealed record UpdateWorkingHoursDayRequest(
    DayOfWeek DayOfWeek,
    bool IsWorkingDay,
    int StartMinute,
    int EndMinute);

public sealed record UpdateWorkingHoursRequest(
    IReadOnlyList<UpdateWorkingHoursDayRequest> Days);

/// <summary>
/// Manages the laboratory-wide weekly working-hours schedule used for escalation calculations.
/// </summary>
public sealed class WorkingHoursService
{
    private const int DefaultStartMinute = 8 * 60;
    private const int DefaultEndMinute = 16 * 60;

    private static readonly DayOfWeek[] AllDaysOfWeek =
    [
        DayOfWeek.Sunday,
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday
    ];

    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;
    private readonly PermissionService _permissions;

    public WorkingHoursService(
        IMicroLimsDbContext db,
        ILabClock clock,
        PermissionService permissions)
    {
        _db = db;
        _clock = clock;
        _permissions = permissions;
    }

    /// <summary>
    /// Returns all seven days in Sunday-Saturday order along with the laboratory time zone.
    /// </summary>
    public async Task<WorkingHoursDto> GetAsync(CancellationToken ct = default)
    {
        var saved = await _db.LabWorkingHours
            .AsNoTracking()
            .ToDictionaryAsync(w => w.DayOfWeek, ct);

        return BuildDto(saved);
    }

    /// <summary>
    /// Validates and persists the complete seven-day working-hours schedule.
    /// Requires <see cref="PermissionConstants.NotificationsConfigure"/>.
    /// </summary>
    public async Task<WorkingHoursDto> UpdateAsync(
        int actorUserId,
        UpdateWorkingHoursRequest request,
        CancellationToken ct = default)
    {
        if (!await _permissions.UserHasPermissionAsync(actorUserId, PermissionConstants.NotificationsConfigure, ct))
            throw new UnauthorizedAccessException("Notifications.Configure permission is required.");

        if (request?.Days is null || request.Days.Count != AllDaysOfWeek.Length)
        {
            throw new FieldValidationException(
                "All seven days of the week must be provided.",
                [new FieldError("days", "All seven days of the week must be provided.")]);
        }

        var errors = new List<FieldError>();
        var seenDays = new HashSet<DayOfWeek>();
        var validated = new Dictionary<DayOfWeek, UpdateWorkingHoursDayRequest>(AllDaysOfWeek.Length);

        for (var i = 0; i < request.Days.Count; i++)
        {
            var day = request.Days[i];
            if (!Enum.IsDefined(day.DayOfWeek))
            {
                errors.Add(new FieldError("dayOfWeek", "Invalid day of week.", i));
                continue;
            }

            if (!seenDays.Add(day.DayOfWeek))
            {
                errors.Add(new FieldError("dayOfWeek", $"Duplicate entry for {day.DayOfWeek}.", i));
                continue;
            }

            if (day.IsWorkingDay)
            {
                if (day.StartMinute < 0 || day.StartMinute >= WorkingTimeCalculator.MinutesPerDay)
                {
                    errors.Add(new FieldError("startMinute", "Start time must be between 00:00 and 23:59.", i));
                }

                if (day.EndMinute <= 0 || day.EndMinute > WorkingTimeCalculator.MinutesPerDay)
                {
                    errors.Add(new FieldError("endMinute", "End time must be between 00:01 and 24:00.", i));
                }

                if (day.StartMinute >= day.EndMinute)
                {
                    errors.Add(new FieldError("endMinute", "End time must be after start time.", i));
                }
            }

            validated[day.DayOfWeek] = day;
        }

        if (errors.Count == 0 && validated.Count != AllDaysOfWeek.Length)
        {
            errors.Add(new FieldError("days", "All seven days of the week must be provided."));
        }

        if (errors.Count > 0)
            throw new FieldValidationException(errors[0].Message, errors);

        var existingByDay = await _db.LabWorkingHours
            .ToDictionaryAsync(w => w.DayOfWeek, ct);

        foreach (var dayOfWeek in AllDaysOfWeek)
        {
            var incoming = validated[dayOfWeek];
            if (existingByDay.TryGetValue(dayOfWeek, out var entity))
            {
                entity.IsWorkingDay = incoming.IsWorkingDay;
                entity.StartMinute = incoming.StartMinute;
                entity.EndMinute = incoming.EndMinute;
            }
            else
            {
                var created = new LabWorkingHours
                {
                    DayOfWeek = dayOfWeek,
                    IsWorkingDay = incoming.IsWorkingDay,
                    StartMinute = incoming.StartMinute,
                    EndMinute = incoming.EndMinute
                };
                _db.LabWorkingHours.Add(created);
                existingByDay[dayOfWeek] = created;
            }
        }

        _db.CurrentUserId = actorUserId;
        await _db.SaveChangesAsync(ct);

        return BuildDto(existingByDay);
    }

    private WorkingHoursDto BuildDto(IReadOnlyDictionary<DayOfWeek, LabWorkingHours> byDay)
    {
        var days = new List<WorkingHoursDayDto>(AllDaysOfWeek.Length);
        var calculatorWeek = new List<WorkingDay>(AllDaysOfWeek.Length);

        foreach (var dayOfWeek in AllDaysOfWeek)
        {
            if (byDay.TryGetValue(dayOfWeek, out var row))
            {
                days.Add(new WorkingHoursDayDto(row.DayOfWeek, row.IsWorkingDay, row.StartMinute, row.EndMinute));
                calculatorWeek.Add(new WorkingDay(row.DayOfWeek, row.IsWorkingDay, row.StartMinute, row.EndMinute));
            }
            else
            {
                days.Add(new WorkingHoursDayDto(dayOfWeek, IsWorkingDay: false, DefaultStartMinute, DefaultEndMinute));
                calculatorWeek.Add(new WorkingDay(dayOfWeek, IsWorkingDay: false, DefaultStartMinute, DefaultEndMinute));
            }
        }

        return new WorkingHoursDto(
            _clock.LabTimeZone.Id,
            _clock.LabTimeZone.DisplayName,
            WorkingTimeCalculator.IsConfigured(calculatorWeek),
            days);
    }
}
