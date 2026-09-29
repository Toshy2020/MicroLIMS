using MicroLIMS.Application.Services;

namespace MicroLIMS.API.BackgroundServices;

// Flips due Solution Preparation rows (Prepared, ExpiresAt <= now) to
// Expired every 5 minutes (HPLC chain S4, spec 4). Every read already
// treats Prepared && ExpiresAt <= now as Expired on its own
// (SolutionPreparationResponse.EffectiveStatusOf), so this worker only
// matters for what a plain Status filter sees, not correctness - there is
// no window in which an expired preparation is offered as available.
public class SolutionPreparationExpiryWorker : BackgroundService
{
    public const string ProcessName = "SolutionPreparationExpiryWorker";
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SolutionPreparationExpiryWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLock = new(1, 1);

    public SolutionPreparationExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<SolutionPreparationExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Worker}] Background service is starting. Every {Minutes} minute(s).", ProcessName, Interval.TotalMinutes);

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

        using var timer = new PeriodicTimer(Interval);

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
                _logger.LogError(ex, "[{Worker}] Unhandled exception during expiry cycle.", ProcessName);
            }
        }

        _logger.LogInformation("[{Worker}] Background service has stopped gracefully.", ProcessName);
    }

    public async Task<int> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!await _concurrencyLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("[{Worker}] Previous expiry cycle still running. Skipping tick.", ProcessName);
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<SolutionPreparationService>();

            var expired = await service.ExpireDueAsync(cancellationToken);
            if (expired > 0)
                _logger.LogInformation("[{Worker}] Expired {Count} solution preparation(s).", ProcessName, expired);

            return expired;
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }
}
