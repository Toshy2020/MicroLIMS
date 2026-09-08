using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using System.Text;

namespace MicroLIMS.Application.Services;

public class ErrorMonitoringService : IErrorMonitoringService
{
    private const int MaxPageSize = 200;

    // Export is bounded: this is a triage aid, not a data warehouse
    // extract, and the table is deliberately prunable operational noise.
    private const int MaxExportRows = 5000;

    private readonly MicroLimsDbContext _db;

    public ErrorMonitoringService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<IncidentListResult> SearchAsync(
        IncidentQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 25 : Math.Min(query.PageSize, MaxPageSize);

        var filtered = ApplyFilters(query);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var rows = await filtered
            // Newest activity first: an incident that recurred a minute ago
            // matters more than one that first appeared last week.
            .OrderByDescending(i => i.LastSeenUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.CorrelationId,
                i.Severity,
                i.Status,
                i.Summary,
                i.FirstSeenUtc,
                i.LastSeenUtc,
                i.ResolvedAtUtc,
                i.ResolutionNotes,
                ResolvedByUserName = i.ResolvedByUser != null ? i.ResolvedByUser.FullName : null,
                OccurrenceCount = i.ErrorLogs.Count,
                Sources = i.ErrorLogs.Select(e => e.Source).Distinct().ToList()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new IncidentListItemDto(
            r.Id, r.CorrelationId, r.Severity, r.Status, r.Summary,
            r.FirstSeenUtc, r.LastSeenUtc, r.OccurrenceCount,
            r.Sources.OrderBy(s => s).ToList(),
            r.ResolvedAtUtc, r.ResolvedByUserName, r.ResolutionNotes)).ToList();

        return new IncidentListResult(items, totalCount, page, pageSize);
    }

    public async Task<IncidentDetailDto?> GetAsync(
        Guid incidentId, CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents
            .AsNoTracking()
            .Include(i => i.ResolvedByUser)
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);

        if (incident is null) return null;

        var logs = await _db.ErrorLogs
            .AsNoTracking()
            .Where(e => e.IncidentId == incidentId)
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new
            {
                e.Id, e.Source, e.Severity, e.SeverityOverride, e.ExceptionType, e.Message,
                e.StackTrace, e.RequestPath, e.HttpMethod, e.StatusCode, e.UserId,
                UserName = e.User != null ? e.User.FullName : null,
                e.OccurredAtUtc, e.RawContext
            })
            .ToListAsync(cancellationToken);

        var logDtos = logs.Select(e => new ErrorLogDto(
            e.Id, e.Source, e.Severity, e.SeverityOverride,
            e.SeverityOverride ?? e.Severity,
            e.ExceptionType, e.Message, e.StackTrace, e.RequestPath, e.HttpMethod,
            e.StatusCode, e.UserId, e.UserName, e.OccurredAtUtc, e.RawContext)).ToList();

        return new IncidentDetailDto(
            incident.Id, incident.CorrelationId, incident.Severity, incident.Status,
            incident.Summary, incident.FirstSeenUtc, incident.LastSeenUtc,
            incident.ResolvedAtUtc, incident.ResolvedByUser?.FullName, incident.ResolutionNotes,
            logDtos);
    }

    public async Task<bool> ResolveAsync(
        Guid incidentId, int userId, string? notes, CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);
        if (incident is null) return false;

        incident.Status = IncidentStatus.Resolved;
        incident.ResolvedByUserId = userId;
        incident.ResolvedAtUtc = DateTime.UtcNow;
        incident.ResolutionNotes = notes;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReopenAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);
        if (incident is null) return false;

        incident.Status = IncidentStatus.Open;
        // Resolution fields are kept deliberately - who closed it and why
        // is still the useful history once it comes back.
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> OverrideSeverityAsync(
        Guid errorLogId, ErrorSeverity? severity, CancellationToken cancellationToken = default)
    {
        var log = await _db.ErrorLogs.FirstOrDefaultAsync(e => e.Id == errorLogId, cancellationToken);
        if (log is null) return false;

        log.SeverityOverride = severity;

        // Recompute from scratch rather than raising like capture does:
        // an override exists precisely to correct a misclassification, and
        // downgrading one child must be able to downgrade the incident.
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == log.IncidentId, cancellationToken);
        if (incident is not null)
        {
            var effective = await _db.ErrorLogs
                .Where(e => e.IncidentId == incident.Id && e.Id != log.Id)
                .Select(e => (int?)(e.SeverityOverride ?? e.Severity))
                .MaxAsync(cancellationToken);

            var mine = (int)(severity ?? log.Severity);
            incident.Severity = (ErrorSeverity)Math.Max(mine, effective ?? mine);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<string> ExportCsvAsync(
        IncidentQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await ApplyFilters(query)
            .OrderByDescending(i => i.LastSeenUtc)
            .Take(MaxExportRows)
            .Select(i => new
            {
                i.CorrelationId,
                i.Severity,
                i.Status,
                i.Summary,
                i.FirstSeenUtc,
                i.LastSeenUtc,
                i.ResolutionNotes,
                ResolvedByUserName = i.ResolvedByUser != null ? i.ResolvedByUser.FullName : null,
                OccurrenceCount = i.ErrorLogs.Count,
                Sources = i.ErrorLogs.Select(e => e.Source).Distinct().ToList()
            })
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("CorrelationId,Severity,Status,Sources,Occurrences,FirstSeenUtc,LastSeenUtc,Summary,ResolvedBy,ResolutionNotes");

        foreach (var r in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(r.CorrelationId),
                Csv(r.Severity.ToString()),
                Csv(r.Status.ToString()),
                Csv(string.Join(" | ", r.Sources.OrderBy(s => s))),
                r.OccurrenceCount.ToString(),
                Csv(r.FirstSeenUtc.ToString("o")),
                Csv(r.LastSeenUtc.ToString("o")),
                Csv(r.Summary),
                Csv(r.ResolvedByUserName),
                Csv(r.ResolutionNotes)
            }));
        }

        return csv.ToString();
    }

    private IQueryable<Incident> ApplyFilters(IncidentQuery query)
    {
        var incidents = _db.Incidents.AsNoTracking().AsQueryable();

        if (query.Severity is { } severity)
            incidents = incidents.Where(i => i.Severity == severity);

        if (query.Status is { } status)
            incidents = incidents.Where(i => i.Status == status);

        // An Incident has no Source of its own - it is whatever tiers its
        // children came from, so filtering means "involved this tier".
        if (query.Source is { } source)
            incidents = incidents.Where(i => i.ErrorLogs.Any(e => e.Source == source));

        // Range is on LastSeenUtc: "what was active in this window",
        // which is the question an admin is actually asking.
        if (query.FromUtc is { } from)
            incidents = incidents.Where(i => i.LastSeenUtc >= from);

        if (query.ToUtc is { } to)
            incidents = incidents.Where(i => i.LastSeenUtc <= to);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            incidents = incidents.Where(i =>
                EF.Functions.ILike(i.Summary, $"%{term}%") ||
                EF.Functions.ILike(i.CorrelationId, $"%{term}%"));
        }

        return incidents;
    }

    // Excel treats a leading =, +, - or @ as a formula, so a crafted error
    // message could execute on open. Prefixed with an apostrophe, which
    // Excel renders as plain text.
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        var sanitized = value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
        if (sanitized.Length > 0 && "=+-@".Contains(sanitized[0])) sanitized = "'" + sanitized;

        return $"\"{sanitized}\"";
    }
}
