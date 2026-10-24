using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Spc;

// Control charts and limit review over the ResultRecord projection, and
// the one write: replacing a Water / EM configuration's alert and action
// limits with ones derived from the trend, under an electronic signature
// and an audit entry. The limits are recomputed here from the same data
// and settings the analyst charted - the client's numbers are only
// accepted as an override, which the signature comment records.
public class ControlChartService
{
    public const string LimitsAppliedActionCode = "TrendLimitsApplied";
    public const string RoomTestConfigurationKind = "RoomTestConfiguration";
    public const string SamplingConfigurationKind = "SamplingConfiguration";

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatures;
    private readonly IAuditEventService _audit;

    public ControlChartService(IMicroLimsDbContext db, IElectronicSignatureService signatures, IAuditEventService audit)
    {
        _db = db;
        _signatures = signatures;
        _audit = audit;
    }

    private sealed record SeriesPoint(
        int RecordId, string ReferenceNumber, DateTime Date, string SubjectName, double Value, string ReportedValue,
        bool IsImputed, ResultLevel ResultLevel, string? AlertLimit, string? ActionLimit, string? SpecLimit, string? Unit);

    // ---- Single location chart ------------------------------------------

    public async Task<ControlChartResult> GetChartAsync(ControlChartQuery q, IReadOnlyCollection<int>? sectionIds = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q.SubjectName)) throw new InvalidOperationException("subjectName is required.");
        ControlChartCalculator.Validate(q.Settings);
        var test = await RequireNumericTestAsync(q.TestCode, ct);

        var (series, category) = await LoadSeriesAsync(q.TestCode, q.SubjectName, q.Category, q.FromDate, q.ToDate, q.ApprovedOnly, sectionIds, ct);
        var targets = category is null ? new List<LimitTarget>() : await FindTargetsAsync(category.Value, q.TestCode, new[] { q.SubjectName }, ct);
        var current = CurrentLimitsFor(targets, series);

        var inCalc = series.Select(p =>
            (q.CalculationFrom is null || p.Date >= q.CalculationFrom) && (q.CalculationTo is null || p.Date <= q.CalculationTo)).ToList();
        var values = series.Select(p => p.Value).ToList();

        var spec = q.CapabilityBasis switch
        {
            CapabilityBasis.ActionLimit => new CapabilitySpec(null, current.Action),
            CapabilityBasis.AlertLimit => new CapabilitySpec(null, current.Alert),
            CapabilityBasis.Custom => new CapabilitySpec(q.CustomLsl, q.CustomUsl),
            _ => new CapabilitySpec(current.SpecLower, current.SpecUpper)
        };
        if (spec.Lsl.HasValue && spec.Usl.HasValue && spec.Lsl > spec.Usl)
            throw new InvalidOperationException("The lower capability limit must not be above the upper limit.");

        var stats = ControlChartCalculator.Compute(values, q.Settings, spec, inCalc);

        var points = series.Select((p, i) => new ControlChartPoint(
            p.RecordId, p.ReferenceNumber, p.Date, p.Value, p.ReportedValue, p.IsImputed, stats.MovingRanges[i],
            p.ResultLevel, inCalc[i], stats.RuleViolations[i],
            UpperOf(p.AlertLimit), UpperOf(p.ActionLimit), UpperOf(p.SpecLimit))).ToList();

        var monthly = series
            .GroupBy(p => p.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var sorted = g.Select(p => p.Value).OrderBy(v => v).ToList();
                return new MonthlyBox(g.Key, sorted.Count, sorted.Average(), ControlChartCalculator.Box(sorted)!);
            })
            .ToList();

        return new ControlChartResult(
            test.Code, test.DisplayName, q.SubjectName, series.Select(p => p.Unit).FirstOrDefault(u => u is not null),
            q.Settings, q.CapabilityBasis, series.Count(p => p.IsImputed), stats, current, points, monthly, targets);
    }

    // ---- Every location for one test -----------------------------------

    public async Task<LimitReviewResult> GetLimitReviewAsync(LimitReviewQuery q, IReadOnlyCollection<int>? sectionIds = null, CancellationToken ct = default)
    {
        ControlChartCalculator.Validate(q.Settings);
        var test = await RequireNumericTestAsync(q.TestCode, ct);
        var (series, _) = await LoadSeriesAsync(q.TestCode, null, q.Category, q.FromDate, q.ToDate, q.ApprovedOnly, sectionIds, ct);

        var bySubject = series.GroupBy(p => p.SubjectName).ToDictionary(g => g.Key, g => g.ToList());
        var targets = await FindTargetsAsync(q.Category, q.TestCode, null, ct);
        var targetsBySubject = targets.GroupBy(t => t.Location).ToDictionary(g => g.Key, g => g.ToList());

        // Every configured location is listed, with or without data, so a
        // point that was never sampled in the period is visible in review.
        var subjects = bySubject.Keys.Union(targetsBySubject.Keys).OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

        var rows = subjects.Select(subject =>
        {
            var pts = bySubject.TryGetValue(subject, out var list) ? list : new List<SeriesPoint>();
            var subjectTargets = targetsBySubject.TryGetValue(subject, out var t) ? t : new List<LimitTarget>();
            var current = CurrentLimitsFor(subjectTargets, pts);
            var stats = ControlChartCalculator.Compute(pts.Select(p => p.Value).ToList(), q.Settings,
                new CapabilitySpec(current.SpecLower, current.SpecUpper));
            return new LimitReviewRow(
                subject, stats.Count, stats.BelowMinimumPoints, stats.Mean, stats.StandardDeviation, stats.SigmaUsed,
                stats.Limits.AlertUpper, stats.Limits.ActionUpper, current.Alert, current.Action,
                stats.Capability.Cpk, stats.RuleViolations.Count(r => r.Length > 0), subjectTargets);
        }).ToList();

        return new LimitReviewResult(test.Code, test.DisplayName, q.Category,
            series.Select(p => p.Unit).FirstOrDefault(u => u is not null), q.Settings, rows);
    }

    // ---- Apply to configuration ----------------------------------------

    public Task<List<AppliedLimit>> ApplyLimitsAsync(
        ApplyTrendLimitsRequest r, int userId, string? ip, IReadOnlyCollection<int>? sectionIds = null, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => ApplyLimitsCoreAsync(r, userId, ip, sectionIds, ct));

    private async Task<List<AppliedLimit>> ApplyLimitsCoreAsync(
        ApplyTrendLimitsRequest r, int userId, string? ip, IReadOnlyCollection<int>? sectionIds, CancellationToken ct)
    {
        if (r.Category is not (SampleCategory.EnvironmentalMonitoring or SampleCategory.Water))
            throw new InvalidOperationException("Limits can only be applied to Water and Environmental Monitoring configurations.");
        if (r.Items is null || r.Items.Count == 0)
            throw new InvalidOperationException("Select at least one location to update.");
        if (r.Items.Select(i => (i.Kind, i.ConfigurationId)).Distinct().Count() != r.Items.Count)
            throw new InvalidOperationException("A configuration is listed more than once.");
        var reason = r.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for change is required.");
        if (string.IsNullOrEmpty(r.Password))
            throw new SignatureVerificationException("Password is required to sign.");
        ControlChartCalculator.Validate(r.Settings);
        var test = await RequireNumericTestAsync(r.TestCode, ct);

        var (series, _) = await LoadSeriesAsync(r.TestCode, null, r.Category, r.FromDate, r.ToDate, r.ApprovedOnly, sectionIds, ct);
        var bySubject = series.GroupBy(p => p.SubjectName).ToDictionary(g => g.Key, g => g.Select(p => p.Value).ToList());

        _db.CurrentUserId = userId;
        var applied = new List<AppliedLimit>();
        foreach (var item in r.Items)
        {
            ValidateLimitPair(item);
            var (entity, location, spec) = await LoadConfigurationAsync(item, r.TestCode, r.Category, ct);
            if (entity.Version != item.Version)
                throw new DbUpdateConcurrencyException(
                    $"The limits for {location} changed after the analysis was loaded (version {item.Version}, now {entity.Version}). Reload and try again.");
            if (!string.Equals(location, item.SubjectName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Configuration {item.ConfigurationId} belongs to {location}, not {item.SubjectName}.");

            var specMax = SpecLimitParser.Parse(spec).Max;
            if (specMax.HasValue && item.ActionLimit > specMax.Value)
                throw new InvalidOperationException($"{location}: the action limit {Fmt(item.ActionLimit)} is above the specification limit {spec}.");

            var values = bySubject.TryGetValue(location, out var v) ? v : new List<double>();
            var stats = ControlChartCalculator.Compute(values, r.Settings);
            var overridden = !Same(stats.Limits.AlertUpper, item.AlertLimit) || !Same(stats.Limits.ActionUpper, item.ActionLimit);

            var (previousAlert, previousAction) = Limits(entity);
            var newAlert = Fmt(item.AlertLimit);
            var newAction = Fmt(item.ActionLimit);

            // Sign first: a wrong password throws before any limit changes.
            var signature = await _signatures.SignAsync(
                userId, r.Password, SignatureMeaning.MasterDataChanged, item.Kind, item.ConfigurationId,
                SignatureComment(test.Code, location, r, stats, previousAlert, previousAction, newAlert, newAction, overridden, reason), ip);

            SetLimits(entity, newAlert, newAction);

            await _audit.RecordUserEventAsync(
                LimitsAppliedActionCode, AuditActionCategory.Configuration, item.Kind,
                reason: reason,
                changes: new List<AuditFieldChange>
                {
                    new("Location", null, location),
                    new("TestCode", null, test.Code),
                    new("AlertLimit", previousAlert, newAlert),
                    new("ActionLimit", previousAction, newAction),
                    new("Basis", null, Basis(r, stats, overridden))
                },
                entityId: item.ConfigurationId.ToString(CultureInfo.InvariantCulture),
                cancellationToken: ct);

            applied.Add(new AppliedLimit(item.Kind, item.ConfigurationId, location, previousAlert, previousAction, newAlert, newAction,
                stats.Limits.AlertUpper, stats.Limits.ActionUpper, overridden, signature.Id));
        }
        return applied;
    }

    private static void ValidateLimitPair(ApplyLimitItem item)
    {
        if (item.AlertLimit < 0 || item.ActionLimit < 0)
            throw new InvalidOperationException($"{item.SubjectName}: limits cannot be negative.");
        if (item.AlertLimit > item.ActionLimit)
            throw new InvalidOperationException($"{item.SubjectName}: the alert limit must not be above the action limit.");
    }

    private async Task<(IVersionedEntity Entity, string Location, string Spec)> LoadConfigurationAsync(
        ApplyLimitItem item, string testCode, SampleCategory category, CancellationToken ct)
    {
        switch (item.Kind)
        {
            case RoomTestConfigurationKind when category == SampleCategory.EnvironmentalMonitoring:
            {
                var c = await _db.RoomTestConfigurations.Include(x => x.Room).FirstOrDefaultAsync(x => x.Id == item.ConfigurationId, ct)
                    ?? throw new NotFoundException($"Room test configuration {item.ConfigurationId} not found.");
                if (!c.IsActive) throw new InvalidOperationException($"Room test configuration {c.Id} is inactive.");
                if (c.TestCode != testCode) throw new InvalidOperationException($"Room test configuration {c.Id} is for {c.TestCode}, not {testCode}.");
                return (c, c.Room?.Name ?? string.Empty, c.SpecLimit);
            }
            case SamplingConfigurationKind when category == SampleCategory.Water:
            {
                var c = await _db.SamplingConfigurations.Include(x => x.WaterSamplingPoint).FirstOrDefaultAsync(x => x.Id == item.ConfigurationId, ct)
                    ?? throw new NotFoundException($"Water sampling configuration {item.ConfigurationId} not found.");
                if (c.TestCode != testCode) throw new InvalidOperationException($"Water sampling configuration {c.Id} is for {c.TestCode}, not {testCode}.");
                return (c, c.WaterSamplingPoint?.Code ?? string.Empty, c.SpecLimit);
            }
            default:
                throw new InvalidOperationException($"'{item.Kind}' is not a {category} limit configuration.");
        }
    }

    private static (string Alert, string Action) Limits(IVersionedEntity e) => e switch
    {
        RoomTestConfiguration c => (c.AlertLimit, c.ActionLimit),
        SamplingConfiguration c => (c.AlertLimit, c.ActionLimit),
        _ => throw new InvalidOperationException("Unsupported configuration.")
    };

    private static void SetLimits(IVersionedEntity e, string alert, string action)
    {
        switch (e)
        {
            case RoomTestConfiguration c: c.AlertLimit = alert; c.ActionLimit = action; break;
            case SamplingConfiguration c: c.AlertLimit = alert; c.ActionLimit = action; break;
        }
    }

    // ---- Shared loading -------------------------------------------------

    private async Task<TestDefinition> RequireNumericTestAsync(string testCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(testCode)) throw new InvalidOperationException("testCode is required.");
        var test = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == testCode, ct)
            ?? throw new InvalidOperationException($"Test code \"{testCode}\" is not configured in Test Master.");
        if (test.WorkflowType != WorkflowType.CountTest)
            throw new InvalidOperationException($"Control charts are only available for numeric results. {testCode} produces qualitative results.");
        return test;
    }

    // Results in sampling-date order (falling back to the receipt date when
    // no sampling date was recorded), latest retest round only, with the
    // below-detection-limit convention (plotted at half the limit).
    private async Task<(List<SeriesPoint> Points, SampleCategory? Category)> LoadSeriesAsync(
        string testCode, string? subjectName, SampleCategory? category, DateTime? from, DateTime? to, bool approvedOnly,
        IReadOnlyCollection<int>? sectionIds, CancellationToken ct)
    {
        var query = (sectionIds is null ? _db.ResultRecords : _db.ResultRecords.Where(r => sectionIds.Contains(r.TestOrder!.SectionId)))
            .Where(r => r.TestCode == testCode);
        if (subjectName is not null) query = query.Where(r => r.SubjectName == subjectName);
        if (category is not null) query = query.Where(r => r.Category == category);
        if (approvedOnly) query = query.Where(r => r.SampleStatus == SampleStatus.Approved);
        if (from is not null) query = query.Where(r => (r.Sample!.SampledAt ?? r.Sample!.ReceivedAt) >= from);
        if (to is not null) query = query.Where(r => (r.Sample!.SampledAt ?? r.Sample!.ReceivedAt) <= to);

        var rows = await query
            .Select(r => new { Record = r, Date = r.Sample!.SampledAt ?? r.Sample!.ReceivedAt })
            .ToListAsync(ct);

        var latestRound = rows
            .GroupBy(x => (x.Record.SourceTable, x.Record.SourceId))
            .Select(g => g.OrderByDescending(x => x.Record.Round).First());

        var points = latestRound
            .Select(x =>
            {
                var r = x.Record;
                var imputed = r.IsBelowDetectionLimit && r.DetectionLimit.HasValue;
                var value = imputed ? r.DetectionLimit!.Value / 2 : r.NumericValue;
                return value is null ? null : new SeriesPoint(
                    r.Id, r.ReferenceNumber, x.Date, r.SubjectName, (double)value.Value, r.ReportedValue, imputed,
                    r.ResultLevel, r.AlertLimit, r.ActionLimit, r.SpecLimit, r.Unit);
            })
            .Where(p => p is not null)
            .Select(p => p!)
            .OrderBy(p => p.Date).ThenBy(p => p.RecordId)
            .ToList();

        var resolvedCategory = category ?? rows.Select(x => (SampleCategory?)x.Record.Category).FirstOrDefault();
        return (points, resolvedCategory);
    }

    private async Task<List<LimitTarget>> FindTargetsAsync(SampleCategory category, string testCode, IReadOnlyCollection<string>? subjects, CancellationToken ct)
    {
        if (category == SampleCategory.EnvironmentalMonitoring)
        {
            var q = _db.RoomTestConfigurations.Include(c => c.Room!).ThenInclude(r => r.Department)
                .Where(c => c.IsActive && c.TestCode == testCode && c.Room!.IsActive);
            if (subjects is not null) q = q.Where(c => subjects.Contains(c.Room!.Name));
            return (await q.OrderBy(c => c.Room!.Name).ThenBy(c => c.Id).ToListAsync(ct))
                .Select(c => new LimitTarget(RoomTestConfigurationKind, c.Id, c.Version, c.Room!.Name,
                    Join(c.Room.Department?.Name, c.Room.GradeClassification), c.TestCode, c.TestType,
                    c.AlertLimit, c.ActionLimit, c.SpecLimit, c.Unit))
                .ToList();
        }
        if (category == SampleCategory.Water)
        {
            var q = _db.SamplingConfigurations.Include(c => c.WaterSamplingPoint)
                .Where(c => c.TestCode == testCode && c.WaterSamplingPoint!.IsActive);
            if (subjects is not null) q = q.Where(c => subjects.Contains(c.WaterSamplingPoint!.Code));
            return (await q.OrderBy(c => c.WaterSamplingPoint!.Code).ThenBy(c => c.Id).ToListAsync(ct))
                .Select(c => new LimitTarget(SamplingConfigurationKind, c.Id, c.Version, c.WaterSamplingPoint!.Code,
                    c.WaterSamplingPoint.Location, c.TestCode, null, c.AlertLimit, c.ActionLimit, c.SpecLimit, c.Unit))
                .ToList();
        }
        return new List<LimitTarget>();
    }

    // The configured limits when the location has exactly one matching
    // configuration; otherwise the limits snapshotted on its latest result.
    private static CurrentLimits CurrentLimitsFor(IReadOnlyList<LimitTarget> targets, IReadOnlyList<SeriesPoint> series)
    {
        string? alert, action, spec;
        if (targets.Count == 1)
        {
            (alert, action, spec) = (targets[0].AlertLimit, targets[0].ActionLimit, targets[0].SpecLimit);
        }
        else
        {
            var latest = series.Count > 0 ? series[^1] : null;
            (alert, action, spec) = (latest?.AlertLimit, latest?.ActionLimit, latest?.SpecLimit);
        }
        var parsedSpec = SpecLimitParser.Parse(spec);
        return new CurrentLimits(UpperOf(alert), UpperOf(action), (double?)parsedSpec.Min, (double?)parsedSpec.Max);
    }

    private static double? UpperOf(string? limit) => (double?)SpecLimitParser.Parse(limit).Max;

    private static string? Join(string? a, string? b) =>
        string.Join(" · ", new[] { a, b }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } joined ? joined : null;

    private static bool Same(double? calculated, decimal applied) =>
        calculated.HasValue && Math.Abs(calculated.Value - (double)applied) < 1e-6;

    private static string Fmt(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Fmt(double? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "n/a";

    private static string MethodLabel(ControlChartSettings s) => s.Method switch
    {
        LimitMethod.Percentile => $"percentiles P{s.AlertPercentile:0.##}/P{s.ActionPercentile:0.##}",
        LimitMethod.StandardDeviation => $"mean ± {s.AlertK:0.##}/{s.ActionK:0.##} SD",
        _ => $"mean ± {s.AlertK:0.##}/{s.ActionK:0.##}σ (MR span {s.MovingRangeLength})"
    };

    private static string Period(ApplyTrendLimitsRequest r) =>
        $"{r.FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "start"} to {r.ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "now"}";

    private static string Basis(ApplyTrendLimitsRequest r, ControlChartComputation stats, bool overridden) =>
        $"{MethodLabel(r.Settings)}, rounding {r.Settings.Rounding}, {Period(r)}{(r.ApprovedOnly ? ", approved results only" : "")}, " +
        $"n={stats.Count}{(stats.BelowMinimumPoints ? $" (below minimum {r.Settings.MinimumPoints})" : "")}, " +
        $"mean={Fmt(stats.Mean)}, σ={Fmt(stats.SigmaUsed)}, calculated alert/action={Fmt(stats.Limits.AlertUpper)}/{Fmt(stats.Limits.ActionUpper)}" +
        (overridden ? ", OVERRIDDEN by signer" : "");

    private static string SignatureComment(
        string testCode, string location, ApplyTrendLimitsRequest r, ControlChartComputation stats,
        string previousAlert, string previousAction, string newAlert, string newAction, bool overridden, string reason) =>
        $"Trend-based limit update for {testCode} at {location}: alert {previousAlert} -> {newAlert}, action {previousAction} -> {newAction}. " +
        $"Basis: {Basis(r, stats, overridden)}. Reason: {reason}";
}
