using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Kpi;

// Leave / training / secondment windows that reduce an analyst's capacity (spec 4.5). Entered by the Section Head
// for analysts in their own sections; never deleted (a correction is an edit with a reason, audited); one
// analyst's windows never overlap, so a day has at most one fraction.
public class AnalystAvailabilityService
{
    public const string ChangedActionCode = "AnalystAvailabilityChanged";
    public const string RecordType = "AnalystAvailability";

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public AnalystAvailabilityService(IMicroLimsDbContext db, IUserSectionScopeService scope, IAuditEventService audit, TimeProvider? timeProvider = null)
    {
        _db = db;
        _scope = scope;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    // D8: without Kpi.ViewTeam only the caller's own rows are ever returned, enforced here.
    public async Task<List<AnalystAvailabilityDto>> ListAsync(int viewerUserId, bool canViewTeam, int? userId,
        DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        if (!canViewTeam && userId is int other && other != viewerUserId)
            throw new UnauthorizedAccessException("You may only view your own availability.");

        var viewerScope = canViewTeam ? await _scope.GetReadableSectionIdsAsync(viewerUserId, ct) : null;
        if (canViewTeam && userId is int asked && !await KpiUserScope.InScopeAsync(_scope, asked, viewerScope, ct))
            throw new UnauthorizedAccessException("That analyst is outside your laboratory sections.");

        var target = canViewTeam ? userId : viewerUserId;
        var query = _db.AnalystAvailabilities.AsNoTracking();
        if (target is int t) query = query.Where(a => a.UserId == t);
        if (from is DateOnly f) query = query.Where(a => a.ToDate >= f);
        if (to is DateOnly e) query = query.Where(a => a.FromDate <= e);
        var rows = await query.OrderBy(a => a.UserId).ThenBy(a => a.FromDate).ToListAsync(ct);

        if (canViewTeam && target is null)
        {
            var kept = new List<AnalystAvailability>();
            foreach (var g in rows.GroupBy(a => a.UserId))
                if (await KpiUserScope.InScopeAsync(_scope, g.Key, viewerScope, ct)) kept.AddRange(g);
            rows = kept;
        }
        return await ToDtosAsync(rows, ct);
    }

    public Task<AnalystAvailabilityDto> CreateAsync(SaveAvailabilityRequest r, int enteredByUserId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, async () =>
        {
            var reason = Validate(r);
            await EnsureEditableAsync(r.UserId, enteredByUserId, ct);
            await EnsureNoOverlapAsync(r.UserId, r.FromDate, r.ToDate, exceptId: 0, ct);
            _db.CurrentUserId = enteredByUserId;

            var row = new AnalystAvailability
            {
                UserId = r.UserId, FromDate = r.FromDate, ToDate = r.ToDate, FractionAvailable = r.FractionAvailable,
                Reason = reason, EnteredByUserId = enteredByUserId, EnteredAt = _time.GetUtcNow().UtcDateTime
            };
            _db.AnalystAvailabilities.Add(row);
            await _db.SaveChangesAsync(ct); // the id is needed for the event

            await _audit.RecordUserEventAsync(ChangedActionCode, AuditActionCategory.Configuration, RecordType,
                reason: reason, entityId: row.Id.ToString(CultureInfo.InvariantCulture), cancellationToken: ct,
                changes: new[]
                {
                    new AuditFieldChange("UserId", null, Fmt(row.UserId)),
                    new AuditFieldChange("FromDate", null, Fmt(row.FromDate)),
                    new AuditFieldChange("ToDate", null, Fmt(row.ToDate)),
                    new AuditFieldChange("FractionAvailable", null, Fmt(row.FractionAvailable))
                });
            return (await ToDtosAsync(new List<AnalystAvailability> { row }, ct))[0];
        });

    public async Task<AnalystAvailabilityDto> UpdateAsync(int id, SaveAvailabilityRequest r, int enteredByUserId, CancellationToken ct = default)
    {
        var reason = Validate(r);
        var row = await _db.AnalystAvailabilities.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Availability entry not found.");
        if (row.UserId != r.UserId)
            throw new InvalidOperationException("An entry cannot be moved to another analyst; correct the dates or enter a new entry.");
        await EnsureEditableAsync(row.UserId, enteredByUserId, ct);
        await EnsureNoOverlapAsync(row.UserId, r.FromDate, r.ToDate, exceptId: id, ct);
        _db.CurrentUserId = enteredByUserId;

        var changes = new[]
        {
            new AuditFieldChange("FromDate", Fmt(row.FromDate), Fmt(r.FromDate)),
            new AuditFieldChange("ToDate", Fmt(row.ToDate), Fmt(r.ToDate)),
            new AuditFieldChange("FractionAvailable", Fmt(row.FractionAvailable), Fmt(r.FractionAvailable))
        };
        row.FromDate = r.FromDate;
        row.ToDate = r.ToDate;
        row.FractionAvailable = r.FractionAvailable;
        row.Reason = reason;
        row.EnteredByUserId = enteredByUserId;
        row.EnteredAt = _time.GetUtcNow().UtcDateTime;

        // Saves the edit and the event together.
        await _audit.RecordUserEventAsync(ChangedActionCode, AuditActionCategory.Configuration, RecordType,
            reason: reason, changes: changes, entityId: id.ToString(CultureInfo.InvariantCulture), cancellationToken: ct);
        return (await ToDtosAsync(new List<AnalystAvailability> { row }, ct))[0];
    }

    private static string Validate(SaveAvailabilityRequest r)
    {
        var reason = (r.Reason ?? string.Empty).Trim();
        if (reason.Length == 0) throw new InvalidOperationException("A reason is required.");
        if (reason.Length > 500) throw new InvalidOperationException("The reason must be 500 characters or fewer.");
        if (r.ToDate < r.FromDate) throw new InvalidOperationException("The end date must not be before the start date.");
        if (r.FractionAvailable < 0 || r.FractionAvailable > 1 || decimal.Round(r.FractionAvailable, 2) != r.FractionAvailable)
            throw new InvalidOperationException("Fraction available must be between 0 and 1 with at most two decimals.");
        return reason;
    }

    private async Task EnsureEditableAsync(int analystId, int editorId, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == analystId && u.IsActive, ct))
            throw new InvalidOperationException("The analyst does not exist or is inactive.");
        var editorScope = await _scope.GetMemberSectionIdsAsync(editorId, ct);
        if (!await KpiUserScope.InScopeAsync(_scope, analystId, editorScope, ct))
            throw new UnauthorizedAccessException("You may only record availability for analysts in your laboratory sections.");
    }

    private async Task EnsureNoOverlapAsync(int userId, DateOnly from, DateOnly to, int exceptId, CancellationToken ct)
    {
        if (await _db.AnalystAvailabilities.AnyAsync(a => a.UserId == userId && a.Id != exceptId && a.FromDate <= to && a.ToDate >= from, ct))
            throw new InvalidOperationException("This period overlaps an existing availability entry for the analyst. Correct that entry instead.");
    }

    private async Task<List<AnalystAvailabilityDto>> ToDtosAsync(List<AnalystAvailability> rows, CancellationToken ct)
    {
        var ids = rows.Select(a => a.UserId).Concat(rows.Select(a => a.EnteredByUserId)).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string Name(int id) => names.TryGetValue(id, out var n) ? n : "(unknown user)";
        return rows.Select(a => new AnalystAvailabilityDto(a.Id, a.UserId, Name(a.UserId), a.FromDate, a.ToDate,
            a.FractionAvailable, a.Reason, Name(a.EnteredByUserId), a.EnteredAt)).ToList();
    }

    private static string Fmt(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Fmt(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Fmt(int i) => i.ToString(CultureInfo.InvariantCulture);
}
