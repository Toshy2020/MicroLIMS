using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Application.Services.Quality;

namespace MicroLIMS.API.BackgroundServices;

public sealed class QualityOverdueWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<QualityOverdueWorker> logger,
    IBackgroundJobRunRecorder? recorder = null) : BackgroundService
{
    public const string ProcessName = "QualityOverdueWorker";
    public const int DefaultIntervalMinutes = 60;

    private bool Enabled => configuration.GetValue<bool?>("QualityOverdue:Enabled") ?? true;

    private int IntervalMinutes =>
        configuration.GetValue<int?>("QualityOverdue:IntervalMinutes") is > 0 and var m ? m : DefaultIntervalMinutes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            logger.LogInformation("[{Worker}] Disabled via QualityOverdue:Enabled.", ProcessName);
            return;
        }

        logger.LogInformation("[{Worker}] Starting. Every {Minutes} minutes.", ProcessName, IntervalMinutes);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(IntervalMinutes));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            do
            {
                await RunCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ScheduledJobPass.RunAsync(recorder, ProcessName, ScanOnceAsync, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Worker}] Scan failed; it will run again on the next tick.", ProcessName);
        }
    }

    public async Task<int?> ScanOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>().CurrentSystemProcessName = ProcessName;
        var clock = scope.ServiceProvider.GetRequiredService<ILabClock>();
        var notifier = scope.ServiceProvider.GetRequiredService<QualityNotifier>();
        var count = await notifier.NotifyOverdueAsync(clock.LabToday, cancellationToken);
        if (count > 0)
            logger.LogInformation("[{Worker}] Sent {Count} overdue quality notification(s).", ProcessName, count);
        return count;
    }
}
