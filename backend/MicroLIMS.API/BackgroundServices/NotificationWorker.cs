using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Application.Services.Notifications;

namespace MicroLIMS.API.BackgroundServices;

/// <summary>
/// Periodically runs <see cref="NotificationScheduler"/> to evaluate scheduled
/// notification conditions, retention purge, and daily digests, then dispatches
/// due emails via <see cref="EmailOutboxSender"/>.
/// </summary>
public class NotificationWorker : BackgroundService
{
    public const string ProcessName = "NotificationWorker";
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLock = new(1, 1);
    private readonly IBackgroundJobRunRecorder? _recorder;

    public NotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationWorker> logger,
        IBackgroundJobRunRecorder? recorder = null)
    {
        _recorder = recorder;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Worker}] Background service is starting. Every {Minutes} minute(s).", ProcessName, Interval.TotalMinutes);

        try
        {
            await ScheduledJobPass.RunAsync(_recorder, ProcessName, async ct => await RunCycleAsync(ct), stoppingToken);
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
                {
                    await ScheduledJobPass.RunAsync(_recorder, ProcessName, async ct => await RunCycleAsync(ct), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Worker}] Unhandled exception during notification cycle.", ProcessName);
            }
        }

        _logger.LogInformation("[{Worker}] Background service has stopped gracefully.", ProcessName);
    }

    public async Task<int> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!await _concurrencyLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("[{Worker}] Previous notification cycle still running. Skipping tick.", ProcessName);
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>().CurrentSystemProcessName = ProcessName;

            var scheduler = scope.ServiceProvider.GetRequiredService<NotificationScheduler>();
            var created = await scheduler.RunAsync(cancellationToken);

            var outboxSender = scope.ServiceProvider.GetRequiredService<EmailOutboxSender>();
            var sent = await outboxSender.SendDueAsync(ct: cancellationToken);

            if (created > 0 || sent > 0)
            {
                _logger.LogInformation("[{Worker}] Created {Created} notification(s) and sent {Sent} email(s).", ProcessName, created, sent);
            }

            return created + sent;
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }
}
