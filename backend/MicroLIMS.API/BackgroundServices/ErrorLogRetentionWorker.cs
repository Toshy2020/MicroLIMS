using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.API.BackgroundServices;

// Prunes old incidents. This table is operational noise, not quality
// evidence - it carries no audit triggers and no immutability, and is
// explicitly outside the GxP record set, so deleting from it is a
// housekeeping action rather than a records decision.
//
// Deleting an Incident cascades its ErrorLogs, so the incident is the
// unit of retention: a half-pruned incident would be worse than either
// keeping or dropping the whole thing.
public class ErrorLogRetentionWorker : BackgroundService
{
    public const string ProcessName = "ErrorLogRetentionWorker";

    public const int DefaultIntervalHours = 24;

    // Low-severity noise ages out quickly; things someone may still need
    // to explain are kept for a year.
    public const int DefaultLowSeverityRetentionDays = 180;
    public const int DefaultHighSeverityRetentionDays = 365;

    // Deleted in batches so a first run against a large backlog cannot
    // hold one long transaction open against the app's own database.
    private const int DeleteBatchSize = 500;
    private const int MaxBatchesPerCycle = 40;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ErrorLogRetentionWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLock = new(1, 1);

    public ErrorLogRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ErrorLogRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private bool Enabled =>
        _configuration.GetValue<bool?>("ErrorMonitoring:Retention:Enabled") ?? true;

    private int IntervalHours =>
        Positive(_configuration.GetValue<int?>("ErrorMonitoring:Retention:IntervalHours"), DefaultIntervalHours);

    private int LowSeverityRetentionDays =>
        Positive(_configuration.GetValue<int?>("ErrorMonitoring:Retention:LowSeverityRetentionDays"),
            DefaultLowSeverityRetentionDays);

    private int HighSeverityRetentionDays =>
        Positive(_configuration.GetValue<int?>("ErrorMonitoring:Retention:HighSeverityRetentionDays"),
            DefaultHighSeverityRetentionDays);

    private static int Positive(int? configured, int fallback) =>
        configured is > 0 ? configured.Value : fallback;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            _logger.LogInformation(
                "[{Worker}] Disabled via ErrorMonitoring:Retention:Enabled - nothing will be pruned.", ProcessName);
            return;
        }

        var interval = TimeSpan.FromHours(IntervalHours);
        _logger.LogInformation(
            "[{Worker}] Starting. Every {Hours}h; Info/Warning kept {Low} days, Error/Critical kept {High} days.",
            ProcessName, IntervalHours, LowSeverityRetentionDays, HighSeverityRetentionDays);

        // Startup cycle: on Render the host sleeps after inactivity, so a
        // purely interval-driven job might never reach its first tick.
        try
        {
            await RunCycleAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Worker}] Unhandled error during startup cycle.", ProcessName);
        }

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await timer.WaitForNextTickAsync(stoppingToken))
                    await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Worker}] Unhandled exception during purge cycle.", ProcessName);
            }
        }

        _logger.LogInformation("[{Worker}] Background service has stopped gracefully.", ProcessName);
    }

    public async Task<int> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!await _concurrencyLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("[{Worker}] Previous purge cycle still running. Skipping tick.", ProcessName);
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();

            var now = DateTime.UtcNow;
            var lowCutoff = now.AddDays(-LowSeverityRetentionDays);
            var highCutoff = now.AddDays(-HighSeverityRetentionDays);

            var deleted = 0;

            for (var batch = 0; batch < MaxBatchesPerCycle; batch++)
            {
                // Aged on LastSeenUtc, not FirstSeenUtc: an incident that
                // recurred yesterday is current, however old its first
                // sighting. Severity is the rolled-up value, so an
                // incident containing anything Error or worse is kept on
                // the longer clock.
                var expiring = await db.Incidents
                    .Where(i =>
                        (i.Severity <= ErrorSeverity.Warning && i.LastSeenUtc < lowCutoff) ||
                        (i.Severity >= ErrorSeverity.Error && i.LastSeenUtc < highCutoff))
                    .OrderBy(i => i.LastSeenUtc)
                    .Take(DeleteBatchSize)
                    .ToListAsync(cancellationToken);

                if (expiring.Count == 0) break;

                // Children go with the parent via the cascade FK.
                db.Incidents.RemoveRange(expiring);
                await db.SaveChangesAsync(cancellationToken);

                deleted += expiring.Count;

                if (expiring.Count < DeleteBatchSize) break;
            }

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "[{Worker}] Purged {Count} incident(s) past retention.", ProcessName, deleted);
            }

            return deleted;
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }
}
