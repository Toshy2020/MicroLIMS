using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Npgsql;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicroLIMS.API.BackgroundServices;

// Proactive database health polling: slow queries and lock contention.
//
// Deliberately does NOT capture connection failures - a stats view can
// never see a connection that never opened, so those are caught
// client-side by ExceptionMiddleware instead. Duplicating them here
// would produce double rows, or a false sense of coverage.
public class DatabaseHealthMonitorWorker : BackgroundService
{
    public const string ProcessName = "DatabaseHealthMonitorWorker";

    public const int DefaultPollIntervalSeconds = 60;
    public const int DefaultSlowQueryThresholdMs = 1000;
    public const int DefaultBlockedQueryThresholdMs = 5000;

    // Sustained blocking is re-reported once, at this multiple of the
    // threshold, so a lock that never clears escalates from Warning to
    // Error without emitting a row on every single poll cycle.
    private const int SustainedBlockingMultiplier = 5;

    // Guard against a pathological cycle: the admin page has to stay
    // readable, and nobody triages 500 slow queries at once.
    private const int MaxFindingsPerCycle = 50;
    private const int QueryTextMaxLength = 4000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseHealthMonitorWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLock = new(1, 1);

    // Cumulative counters from the previous cycle, keyed by queryid. Only
    // the growth between cycles is reported, so a query that was slow last
    // week does not resurface every minute forever.
    private readonly Dictionary<long, (long Calls, double TotalExecTimeMs)> _lastSeenStatements = new();

    // Findings already reported, keyed by pid + query start, so a query
    // blocked across ten consecutive cycles produces one row, not ten.
    private readonly HashSet<string> _reportedActivity = new();

    // Latched so an unavailable extension is reported once, not once a minute.
    private bool _statStatementsUnavailableLogged;

    // Set after the first successful poll. Before it, an unknown queryid is
    // pre-existing history; after it, an unknown queryid is genuinely new.
    private bool _statementBaselineEstablished;

    public DatabaseHealthMonitorWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DatabaseHealthMonitorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private bool Enabled =>
        _configuration.GetValue<bool?>("ErrorMonitoring:DatabaseHealth:Enabled") ?? true;

    private int PollIntervalSeconds =>
        Positive(_configuration.GetValue<int?>("ErrorMonitoring:DatabaseHealth:PollIntervalSeconds"), DefaultPollIntervalSeconds);

    private int SlowQueryThresholdMs =>
        Positive(_configuration.GetValue<int?>("ErrorMonitoring:DatabaseHealth:SlowQueryThresholdMs"), DefaultSlowQueryThresholdMs);

    private int BlockedQueryThresholdMs =>
        Positive(_configuration.GetValue<int?>("ErrorMonitoring:DatabaseHealth:BlockedQueryThresholdMs"), DefaultBlockedQueryThresholdMs);

    private static int Positive(int? configured, int fallback) =>
        configured is > 0 ? configured.Value : fallback;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            _logger.LogInformation(
                "[{Worker}] Disabled via ErrorMonitoring:DatabaseHealth:Enabled - not polling.", ProcessName);
            return;
        }

        var interval = TimeSpan.FromSeconds(PollIntervalSeconds);
        _logger.LogInformation(
            "[{Worker}] Starting. Interval {Interval}s, slow-query threshold {SlowMs}ms, blocked-query threshold {BlockedMs}ms.",
            ProcessName, PollIntervalSeconds, SlowQueryThresholdMs, BlockedQueryThresholdMs);

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunCycleAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The monitor must never take the host down - it exists to
                // report failures, not to become one.
                _logger.LogError(ex, "[{Worker}] Unhandled exception during poll cycle.", ProcessName);
            }
        }

        _logger.LogInformation("[{Worker}] Background service has stopped gracefully.", ProcessName);
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!await _concurrencyLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning(
                "[{Worker}] Previous poll cycle still running. Skipping overlapping tick.", ProcessName);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
            var capture = scope.ServiceProvider.GetRequiredService<IErrorCaptureService>();

            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            var statementsAvailable = await PollSlowQueriesAsync(connection, capture, cancellationToken);
            await PollActivityAsync(connection, capture, statementsAvailable, cancellationToken);
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }

    // Returns false when pg_stat_statements is not usable, so the caller
    // can fall back to what pg_stat_activity alone can see.
    private async Task<bool> PollSlowQueriesAsync(
        NpgsqlConnection connection, IErrorCaptureService capture, CancellationToken cancellationToken)
    {
        const string sql =
            "SELECT s.queryid, s.query, s.calls, s.total_exec_time, s.mean_exec_time, s.max_exec_time " +
            "FROM pg_stat_statements s " +
            "JOIN pg_database d ON d.oid = s.dbid " +
            "WHERE d.datname = current_database() AND s.queryid IS NOT NULL";

        var rows = new List<(long QueryId, string Query, long Calls, double TotalMs, double MeanMs, double MaxMs)>();

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetDouble(3),
                    reader.GetDouble(4),
                    reader.GetDouble(5)));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable ||
                                           ex.SqlState == PostgresErrorCodes.UndefinedFunction ||
                                           ex.SqlState == PostgresErrorCodes.UndefinedObject ||
                                           ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            // Extension absent, dropped, or unreadable by this role. Log
            // once and keep going on pg_stat_activity alone - never a line
            // per cycle, and never stop polling for locks.
            if (!_statStatementsUnavailableLogged)
            {
                _statStatementsUnavailableLogged = true;
                _logger.LogWarning(
                    "[{Worker}] pg_stat_statements unavailable ({SqlState}: {Message}). Aggregate slow-query capture disabled; falling back to in-flight long-running query detection via pg_stat_activity.",
                    ProcessName, ex.SqlState, ex.MessageText);
            }
            return false;
        }

        var threshold = SlowQueryThresholdMs;
        var emitted = 0;

        foreach (var row in rows)
        {
            var known = _lastSeenStatements.TryGetValue(row.QueryId, out var previous);

            if (!known && !_statementBaselineEstablished)
            {
                // First poll of this process: everything here predates the
                // worker. Baseline it, or startup would dump every
                // historically slow query at once - and on Render's free
                // tier the host wakes constantly.
                _lastSeenStatements[row.QueryId] = (row.Calls, row.TotalMs);
                continue;
            }

            if (!known)
            {
                // Appeared since the last cycle, so its whole counter is
                // this window. Treating it as baseline instead would
                // silently swallow the first sighting of a newly slow
                // query - the case most worth hearing about, and for a
                // query that runs once a day, a day of blindness.
                previous = (0, 0);
            }

            if (row.Calls < previous.Calls || row.TotalMs < previous.TotalExecTimeMs)
            {
                // Counters went backwards: pg_stat_statements_reset(), a
                // server restart, or a Neon compute recycle. Re-baseline
                // rather than emit a nonsense negative delta.
                _lastSeenStatements[row.QueryId] = (row.Calls, row.TotalMs);
                continue;
            }

            var deltaCalls = row.Calls - previous.Calls;
            var deltaTotalMs = row.TotalMs - previous.TotalExecTimeMs;
            _lastSeenStatements[row.QueryId] = (row.Calls, row.TotalMs);

            if (deltaCalls <= 0) continue;

            // Mean over this window only, not lifetime: a query that used
            // to be slow and has since been fixed stops reporting.
            var windowMeanMs = deltaTotalMs / deltaCalls;
            if (windowMeanMs < threshold) continue;

            if (emitted++ >= MaxFindingsPerCycle)
            {
                _logger.LogWarning(
                    "[{Worker}] More than {Max} slow queries in one cycle - remainder skipped this tick.",
                    ProcessName, MaxFindingsPerCycle);
                break;
            }

            var queryText = Truncate(row.Query, QueryTextMaxLength) ?? string.Empty;

            await capture.CaptureAsync(new ErrorCaptureRequest(
                Source: ErrorSource.Database,
                Severity: ErrorSeverity.Warning,
                // Keyed by queryid so every recurrence of the same
                // normalized statement accumulates under one Incident.
                CorrelationId: $"db-slowquery-{row.QueryId}",
                ExceptionType: "SlowQuery",
                Message: $"Slow query averaged {windowMeanMs:F0}ms over {deltaCalls} execution(s) (threshold {threshold}ms).",
                StackTrace: queryText,
                RawContext: JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["kind"] = "slowQuery",
                    ["queryId"] = row.QueryId,
                    ["query"] = queryText,
                    ["windowMeanMs"] = Math.Round(windowMeanMs, 2),
                    ["windowCalls"] = deltaCalls,
                    ["lifetimeMeanMs"] = Math.Round(row.MeanMs, 2),
                    ["lifetimeMaxMs"] = Math.Round(row.MaxMs, 2),
                    ["lifetimeCalls"] = row.Calls,
                    ["thresholdMs"] = threshold
                }),
                Summary: $"Slow query {windowMeanMs:F0}ms - {Excerpt(queryText)}"), cancellationToken);
        }

        _statementBaselineEstablished = true;
        return true;
    }

    private async Task PollActivityAsync(
        NpgsqlConnection connection,
        IErrorCaptureService capture,
        bool statementsAvailable,
        CancellationToken cancellationToken)
    {
        var blockedThreshold = BlockedQueryThresholdMs;
        var slowThreshold = SlowQueryThresholdMs;

        // One pass over pg_stat_activity covers both concerns: queries
        // blocked on a lock, and - only when pg_stat_statements is not
        // available - queries simply running too long right now.
        const string sql =
            "SELECT a.pid, a.query_start, a.wait_event_type, a.wait_event, a.query, " +
            "pg_blocking_pids(a.pid) AS blocked_by, " +
            "EXTRACT(EPOCH FROM (now() - a.query_start)) * 1000 AS elapsed_ms " +
            "FROM pg_stat_activity a " +
            "WHERE a.datname = current_database() " +
            "AND a.pid <> pg_backend_pid() " +
            "AND a.state <> 'idle' " +
            "AND a.query_start IS NOT NULL";

        var seenKeys = new HashSet<string>();
        var findings = new List<ActivityFinding>();

        await using (var command = new NpgsqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var pid = reader.GetInt32(0);
                var queryStart = reader.GetDateTime(1);
                var waitType = reader.IsDBNull(2) ? null : reader.GetString(2);
                var waitEvent = reader.IsDBNull(3) ? null : reader.GetString(3);
                var query = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                var blockedBy = reader.IsDBNull(5) ? Array.Empty<int>() : (int[])reader.GetValue(5);
                var elapsedMs = reader.IsDBNull(6) ? 0d : reader.GetDouble(6);

                var isBlocked = blockedBy.Length > 0;

                if (isBlocked && elapsedMs >= blockedThreshold)
                {
                    // pid alone is not unique - PostgreSQL reuses them.
                    // Pairing it with query_start identifies one episode.
                    var episode = $"block:{pid}:{queryStart.Ticks}";
                    var sustained = elapsedMs >= (double)blockedThreshold * SustainedBlockingMultiplier;
                    var key = sustained ? episode + ":sustained" : episode;

                    seenKeys.Add(episode);
                    seenKeys.Add(key);

                    // Dedupe on the escalation-qualified key, but group on
                    // the bare episode: the sustained row must land on the
                    // same Incident and roll its severity up to Error,
                    // rather than opening a second Incident for one lock.
                    if (_reportedActivity.Add(key))
                        findings.Add(new ActivityFinding(key, episode, true, sustained, pid, elapsedMs, waitType, waitEvent, query, blockedBy));
                }
                else if (!statementsAvailable && !isBlocked && elapsedMs >= slowThreshold)
                {
                    // Degraded mode only. Sampling: catches queries slow
                    // enough to still be running at poll time, and misses
                    // the fast-but-frequent ones pg_stat_statements sees.
                    var key = $"inflight:{pid}:{queryStart.Ticks}";
                    seenKeys.Add(key);

                    if (_reportedActivity.Add(key))
                        findings.Add(new ActivityFinding(key, key, false, false, pid, elapsedMs, waitType, waitEvent, query, blockedBy));
                }
            }
        }

        // Forget episodes that have cleared, so a later pid reuse is not
        // silently suppressed and the set cannot grow without bound.
        _reportedActivity.RemoveWhere(k => !seenKeys.Contains(k));

        foreach (var finding in findings.Take(MaxFindingsPerCycle))
        {
            var queryText = Truncate(RedactLiterals(finding.Query), QueryTextMaxLength) ?? string.Empty;

            var message = finding.IsBlocked
                ? $"Query blocked for {finding.ElapsedMs:F0}ms by pid(s) {string.Join(", ", finding.BlockedBy)} (threshold {blockedThreshold}ms)."
                : $"Query still running after {finding.ElapsedMs:F0}ms (threshold {slowThreshold}ms).";

            await capture.CaptureAsync(new ErrorCaptureRequest(
                Source: ErrorSource.Database,
                // Sustained blocking is the one case worth escalating: a
                // lock nobody clears stops the lab, not just one request.
                Severity: finding.Sustained ? ErrorSeverity.Error : ErrorSeverity.Warning,
                CorrelationId: Truncate($"db-{finding.CorrelationKey}", 100)!,
                ExceptionType: finding.IsBlocked ? "BlockedQuery" : "LongRunningQuery",
                Message: message,
                StackTrace: queryText,
                RawContext: JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["kind"] = finding.IsBlocked ? "blockedQuery" : "longRunningQuery",
                    ["pid"] = finding.Pid,
                    ["elapsedMs"] = Math.Round(finding.ElapsedMs, 0),
                    ["waitEventType"] = finding.WaitEventType,
                    ["waitEvent"] = finding.WaitEvent,
                    ["blockedByPids"] = finding.BlockedBy,
                    ["sustained"] = finding.Sustained,
                    ["query"] = queryText,
                    ["thresholdMs"] = finding.IsBlocked ? blockedThreshold : slowThreshold
                }),
                Summary: finding.IsBlocked
                    ? $"Query blocked by pid {string.Join(", ", finding.BlockedBy)} - {Excerpt(queryText)}"
                    : $"Long-running query {finding.ElapsedMs:F0}ms - {Excerpt(queryText)}"), cancellationToken);
        }
    }

    private record ActivityFinding(
        string Key,
        string CorrelationKey,
        bool IsBlocked,
        bool Sustained,
        int Pid,
        double ElapsedMs,
        string? WaitEventType,
        string? WaitEvent,
        string Query,
        int[] BlockedBy);

    // pg_stat_activity.query is the RAW statement - unlike
    // pg_stat_statements, PostgreSQL does not normalize it. A blocked or
    // long-running query can therefore carry real sample ids, result
    // values or usernames, and this table is prunable non-GxP operational
    // data that must not become a shadow copy of laboratory records.
    // Quoted identifiers stay intact: "Samples"."SampleId" is schema, not
    // data, and stripping it would leave the finding undiagnosable.
    private static readonly Regex StringLiteralPattern = new(@"'(?:[^']|'')*'", RegexOptions.Compiled);
    private static readonly Regex NumericLiteralPattern = new(@"(?<![\w""$.])\d+(?:\.\d+)?(?![\w""])", RegexOptions.Compiled);

    public static string RedactLiterals(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return sql;
        var redacted = StringLiteralPattern.Replace(sql, "'?'");
        // $1/$2 placeholders are left alone - they are already the
        // normalized form Npgsql sends for every parameterized query.
        return NumericLiteralPattern.Replace(redacted, "?");
    }

    // Collapses a statement to one short line so the incident list reads
    // as distinct rows rather than a column of identical "SlowQuery".
    private static string Excerpt(string query)
    {
        var collapsed = string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= 120 ? collapsed : collapsed[..120] + "...";
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
