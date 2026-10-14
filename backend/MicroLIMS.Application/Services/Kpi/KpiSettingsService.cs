using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Kpi;

// Per-section capacity assumptions (spec 4.5). Edited in place (the automatic audit trail keeps the previous values);
// a reason is required and a semantic Configuration event is recorded. Not a weight, so no e-signature.
public class KpiSettingsService
{
    public const string SettingsChangedActionCode = "KpiSettingsChanged";
    public const string RecordType = "KpiSetting";

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public KpiSettingsService(IMicroLimsDbContext db, IUserSectionScopeService scope, IAuditEventService audit, TimeProvider? timeProvider = null)
    {
        _db = db;
        _scope = scope;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<KpiSettingsDto> GetAsync(int sectionId, int userId, CancellationToken ct = default)
    {
        await EnsureSectionAccessAsync(sectionId, userId, ct);
        var row = await _db.KpiSettings.AsNoTracking().FirstOrDefaultAsync(s => s.SectionId == sectionId, ct);
        return row is null
            ? new KpiSettingsDto(sectionId, KpiSettingDefaults.HoursPerAnalystDay, KpiSettingDefaults.WorkingWeekdays,
                KpiSettingDefaults.LoadHorizonDays, true, 0)
            : ToDto(row);
    }

    public async Task<KpiSettingsDto> UpdateAsync(int sectionId, UpdateKpiSettingsRequest r, int userId, CancellationToken ct = default)
    {
        var reason = (r.Reason ?? string.Empty).Trim();
        if (reason.Length == 0) throw new InvalidOperationException("A reason for the change is required.");
        if (reason.Length > 500) throw new InvalidOperationException("The reason must be 500 characters or fewer.");
        if (r.HoursPerAnalystDay <= 0 || r.HoursPerAnalystDay > 24 || decimal.Round(r.HoursPerAnalystDay, 2) != r.HoursPerAnalystDay)
            throw new InvalidOperationException("Hours per analyst day must be greater than 0 and at most 24, with at most two decimals.");
        if (r.WorkingWeekdays is < 1 or > 127)
            throw new InvalidOperationException("Select at least one working weekday.");
        if (r.LoadHorizonDays is < 1 or > 30)
            throw new InvalidOperationException("The load horizon must be between 1 and 30 working days.");

        await EnsureSectionAccessAsync(sectionId, userId, ct);
        _db.CurrentUserId = userId;

        var row = await _db.KpiSettings.FirstOrDefaultAsync(s => s.SectionId == sectionId, ct);
        if (row is null)
        {
            row = new KpiSetting { SectionId = sectionId }; // entity defaults equal KpiSettingDefaults, so "previous" below is the default
            _db.KpiSettings.Add(row);
        }
        else
        {
            RecordVersion.EnsureCurrent(_db, row);
        }

        var changes = new[]
        {
            new AuditFieldChange("HoursPerAnalystDay", Fmt(row.HoursPerAnalystDay), Fmt(r.HoursPerAnalystDay)),
            new AuditFieldChange("WorkingWeekdays", Fmt(row.WorkingWeekdays), Fmt(r.WorkingWeekdays)),
            new AuditFieldChange("LoadHorizonDays", Fmt(row.LoadHorizonDays), Fmt(r.LoadHorizonDays))
        };

        row.HoursPerAnalystDay = r.HoursPerAnalystDay;
        row.WorkingWeekdays = r.WorkingWeekdays;
        row.LoadHorizonDays = r.LoadHorizonDays;
        row.UpdatedByUserId = userId;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        // Saves the row and the event together.
        await _audit.RecordUserEventAsync(SettingsChangedActionCode, AuditActionCategory.Configuration, RecordType,
            reason: reason, changes: changes, entityId: sectionId.ToString(CultureInfo.InvariantCulture), cancellationToken: ct);
        return ToDto(row);
    }

    private async Task EnsureSectionAccessAsync(int sectionId, int userId, CancellationToken ct)
    {
        if (!await _db.DocumentSections.AnyAsync(s => s.Id == sectionId && s.IsActive, ct))
            throw new NotFoundException("Laboratory section not found.");
        var ids = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (ids is not null && !ids.Contains(sectionId))
            throw new UnauthorizedAccessException("You do not have access to this laboratory section.");
    }

    private static KpiSettingsDto ToDto(KpiSetting s) =>
        new(s.SectionId, s.HoursPerAnalystDay, s.WorkingWeekdays, s.LoadHorizonDays, false, s.Version);

    private static string Fmt(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Fmt(int v) => v.ToString(CultureInfo.InvariantCulture);
}
