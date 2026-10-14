using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Kpi;

public class KpiDashboardService
{
    private const string FailedMessage = "This figure could not be calculated. The error has been logged.";

    private readonly IMicroLimsDbContext _db;
    private readonly KpiRegistry _registry;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;
    private readonly ILogger<KpiDashboardService> _logger;

    public KpiDashboardService(IMicroLimsDbContext db, KpiRegistry registry, IUserSectionScopeService scope,
        TimeProvider? timeProvider = null, ILogger<KpiDashboardService>? logger = null)
    {
        _db = db;
        _registry = registry;
        _scope = scope;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<KpiDashboardService>.Instance;
    }

    public async Task<KpiDashboardDto> GetDashboardAsync(int userId, bool canViewTeam, DateOnly from, DateOnly to,
        string? category, string? location, string? testCode, int? analystId, CancellationToken ct = default)
    {
        var q = await BuildQueryAsync(userId, canViewTeam, from, to, category, location, testCode, analystId, ct);
        var entries = new List<KpiEntryDto>();
        // Sequential on purpose: one DbContext, and one failing KPI must not take the page down.
        foreach (var kpi in _registry.All)
        {
            try
            {
                var result = await kpi.ComputeAsync(q, ct);
                entries.Add(new KpiEntryDto(kpi.Key, kpi.Name, kpi.Unit, kpi.Direction, kpi.Group, kpi.Order, result, null));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KPI {Key} failed for window {From:o} - {To:o}", kpi.Key, q.From, q.To);
                entries.Add(new KpiEntryDto(kpi.Key, kpi.Name, kpi.Unit, kpi.Direction, kpi.Group, kpi.Order, null, FailedMessage));
            }
        }

        var prior = q.PriorPeriod();
        return new KpiDashboardDto(q.From, q.To, prior.From, prior.To, q.Now, entries);
    }

    public async Task<KpiRecordPageDto> GetRecordsAsync(string key, int userId, bool canViewTeam, DateOnly from, DateOnly to,
        string? category, string? location, string? testCode, int? analystId, int page, int pageSize, CancellationToken ct = default)
    {
        var kpi = _registry.Find(key) ?? throw new NotFoundException($"Unknown KPI '{key}'.");
        var q = await BuildQueryAsync(userId, canViewTeam, from, to, category, location, testCode, analystId, ct);
        return await kpi.GetRecordsAsync(q, Math.Max(page, 1), pageSize <= 0 ? 50 : Math.Min(pageSize, 200), ct);
    }

    public async Task<KpiDefinitionsDto> GetDefinitionsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        ValidateDates(from, to);
        var kpis = _registry.All
            .Select(k => new KpiDefinitionDto(k.Key, k.Name, k.Unit, k.Direction, k.Group, k.Order, k.Definition, k.ClockEvents, k.Population, k.Source))
            .ToList();

        var versions = await _db.WorkflowProfileVersions.AsNoTracking()
            .Where(v => v.Status == WorkflowProfileStatus.Effective && v.EffectiveFrom <= to)
            .ToListAsync(ct);

        // Each field is resolved on its own (units and target can come from different rows of one scope).
        var inForce = versions.GroupBy(v => (v.WorkflowType, v.TestCode)).Select(g =>
        {
            var ordered = g.OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.Id).ToList();
            var units = ordered.FirstOrDefault(v => v.WorkUnits.HasValue);
            var target = ordered.FirstOrDefault(v => v.TargetAnalystDays.HasValue);
            var latest = new[] { units, target }.Where(v => v is not null).OrderByDescending(v => v!.EffectiveFrom).ThenByDescending(v => v!.Id).First()!;
            return new ProfileInForceDto(g.Key.WorkflowType, g.Key.TestCode, units?.WorkUnits, target?.TargetAnalystDays, latest.EffectiveFrom, latest.Id);
        })
        .OrderBy(p => p.WorkflowType).ThenBy(p => p.TestCode, StringComparer.Ordinal)
        .ToList();

        return new KpiDefinitionsDto(kpis, inForce);
    }

    private static void ValidateDates(DateOnly from, DateOnly to)
    {
        if (from == default || to == default)
            throw new InvalidOperationException("A start date and an end date are required.");
        if (from > to)
            throw new InvalidOperationException("The start date must not be after the end date.");
    }

    private async Task<KpiQuery> BuildQueryAsync(int userId, bool canViewTeam, DateOnly from, DateOnly to,
        string? category, string? location, string? testCode, int? analystId, CancellationToken ct)
    {
        ValidateDates(from, to);
        // D8: without Kpi.ViewTeam a caller may only ask about themselves.
        if (!canViewTeam && analystId.HasValue && analystId.Value != userId)
            throw new UnauthorizedAccessException("You can only view your own analyst figures.");
        KpiScope.ParseCategory(category);   // reject an unknown category up front, as a 400

        var sections = await _scope.GetAccessibleSectionIdsAsync(userId, ct);   // null = unrestricted, empty = nothing
        var now = _time.GetUtcNow().UtcDateTime;
        return new KpiQuery(
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            sections, Blank(category), Blank(location), Blank(testCode), analystId, now, userId, canViewTeam);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
