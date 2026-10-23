using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.EquipmentPlan;

public record SaveEquipmentScheduleRequest(
    int? CalibrationIntervalValue,
    IntervalUnit? CalibrationIntervalUnit,
    DateOnly? LastCalibratedOn,
    int? PmIntervalValue,
    IntervalUnit? PmIntervalUnit,
    DateOnly? LastPmOn);

public record EquipmentScheduleResponse(
    int Id,
    int? CalibrationIntervalValue,
    IntervalUnit? CalibrationIntervalUnit,
    DateOnly? LastCalibratedOn,
    int? PmIntervalValue,
    IntervalUnit? PmIntervalUnit,
    DateOnly? LastPmOn,
    uint Version)
{
    public int EquipmentId => Id;

    public static EquipmentScheduleResponse From(EquipmentInventory e) => new(
        e.Id,
        e.CalibrationIntervalValue,
        e.CalibrationIntervalUnit,
        e.LastCalibratedOn,
        e.PmIntervalValue,
        e.PmIntervalUnit,
        e.LastPmOn,
        e.Version);
}

public class EquipmentBaselineService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ILabClock _clock;

    public EquipmentBaselineService(IMicroLimsDbContext db, ILabClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<EquipmentScheduleResponse> SaveScheduleAsync(
        int equipmentId,
        SaveEquipmentScheduleRequest r,
        uint version,
        int userId)
    {
        ValidateScheduleRequest(r);

        var entity = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == equipmentId)
            ?? throw new NotFoundException($"Equipment {equipmentId} not found.");

        if (version != 0)
        {
            _db.ExpectedVersion = version;
        }
        RecordVersion.EnsureCurrent(_db, entity);

        if (r.LastCalibratedOn != entity.LastCalibratedOn)
        {
            var hasCompletedCalibration = await _db.EquipmentTasks.AnyAsync(t =>
                t.EquipmentInventoryId == equipmentId &&
                t.TaskType == EquipmentTaskType.Calibration &&
                t.CompletedOn != null);

            if (hasCompletedCalibration)
            {
                throw new BusinessRuleException(
                    "Last calibration date cannot be changed once a calibration has been completed.");
            }
        }

        var previousSchedule = DescribeSchedule(
            entity.CalibrationIntervalValue,
            entity.CalibrationIntervalUnit,
            entity.LastCalibratedOn,
            entity.PmIntervalValue,
            entity.PmIntervalUnit,
            entity.LastPmOn);

        var newSchedule = DescribeSchedule(
            r.CalibrationIntervalValue,
            r.CalibrationIntervalUnit,
            r.LastCalibratedOn,
            r.PmIntervalValue,
            r.PmIntervalUnit,
            r.LastPmOn);

        entity.CalibrationIntervalValue = r.CalibrationIntervalValue;
        entity.CalibrationIntervalUnit = r.CalibrationIntervalUnit;
        entity.LastCalibratedOn = r.LastCalibratedOn;
        entity.PmIntervalValue = r.PmIntervalValue;
        entity.PmIntervalUnit = r.PmIntervalUnit;
        entity.LastPmOn = r.LastPmOn;
        entity.LastModifiedByUserId = userId;
        entity.LastModifiedAt = _clock.UtcNow.UtcDateTime;

        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(EquipmentInventory),
            EntityId = entity.Id.ToString(),
            Action = "Update",
            PreviousValue = previousSchedule,
            NewValue = newSchedule,
            UserId = userId,
            Timestamp = _clock.UtcNow.UtcDateTime
        });

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        return EquipmentScheduleResponse.From(entity);
    }

    private void ValidateScheduleRequest(SaveEquipmentScheduleRequest r)
    {
        // Calibration interval: value and unit set together or both null
        if (r.CalibrationIntervalValue.HasValue != r.CalibrationIntervalUnit.HasValue)
        {
            throw new BusinessRuleException("Calibration interval value and unit must be specified together.");
        }

        if (r.CalibrationIntervalValue.HasValue)
        {
            ValidateIntervalRange("Calibration", r.CalibrationIntervalValue.Value, r.CalibrationIntervalUnit!.Value);
        }

        // PM interval: value and unit set together or both null
        if (r.PmIntervalValue.HasValue != r.PmIntervalUnit.HasValue)
        {
            throw new BusinessRuleException("Maintenance interval value and unit must be specified together.");
        }

        if (r.PmIntervalValue.HasValue)
        {
            ValidateIntervalRange("Maintenance", r.PmIntervalValue.Value, r.PmIntervalUnit!.Value);
        }

        // Baseline dates not after today
        var today = _clock.LabToday;
        if (r.LastCalibratedOn.HasValue && r.LastCalibratedOn.Value > today)
        {
            throw new BusinessRuleException("Last calibration date cannot be in the future.");
        }

        if (r.LastPmOn.HasValue && r.LastPmOn.Value > today)
        {
            throw new BusinessRuleException("Last maintenance date cannot be in the future.");
        }
    }

    private static void ValidateIntervalRange(string intervalName, int value, IntervalUnit unit)
    {
        switch (unit)
        {
            case IntervalUnit.Weeks:
                if (value is < 1 or > 104)
                {
                    throw new BusinessRuleException($"{intervalName} interval in weeks must be between 1 and 104.");
                }
                break;
            case IntervalUnit.Months:
                if (value is < 1 or > 60)
                {
                    throw new BusinessRuleException($"{intervalName} interval in months must be between 1 and 60.");
                }
                break;
            case IntervalUnit.Years:
                if (value is < 1 or > 5)
                {
                    throw new BusinessRuleException($"{intervalName} interval in years must be between 1 and 5.");
                }
                break;
            default:
                throw new BusinessRuleException($"Unsupported interval unit: {unit}.");
        }
    }

    private static string DescribeInterval(int? value, IntervalUnit? unit)
    {
        if (!value.HasValue || !unit.HasValue) return "None";
        return (unit.Value, value.Value) switch
        {
            (IntervalUnit.Weeks, 1) => "Weekly",
            (IntervalUnit.Weeks, var v) => $"Every {v} weeks",
            (IntervalUnit.Months, 1) => "Monthly",
            (IntervalUnit.Months, var v) => $"Every {v} months",
            (IntervalUnit.Years, 1) => "Yearly",
            (IntervalUnit.Years, var v) => $"Every {v} years",
            _ => $"{value} {unit}"
        };
    }

    private static string DescribeSchedule(
        int? calVal,
        IntervalUnit? calUnit,
        DateOnly? lastCal,
        int? pmVal,
        IntervalUnit? pmUnit,
        DateOnly? lastPm)
    {
        var calDesc = DescribeInterval(calVal, calUnit);
        var calPart = $"Calibration: {calDesc}" + (lastCal.HasValue ? $" (Last: {lastCal.Value:yyyy-MM-dd})" : "");
        var pmDesc = DescribeInterval(pmVal, pmUnit);
        var pmPart = $"PM: {pmDesc}" + (lastPm.HasValue ? $" (Last: {lastPm.Value:yyyy-MM-dd})" : "");
        return $"{calPart}; {pmPart}";
    }
}
