using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.EquipmentPlan;

namespace MicroLIMS.API.BackgroundServices;

// Takes instruments with overdue calibrations out of service.
// The scan is idempotent, so running often costs nothing and a restart loses nothing.
public class EquipmentOverdueWorker : BackgroundService
{
    public const string ProcessName = "EquipmentOverdueWorker";
    public const int DefaultIntervalMinutes = 60;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EquipmentOverdueWorker> _logger;
    private readonly IBackgroundJobRunRecorder? _recorder;

    public EquipmentOverdueWorker(
        IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<EquipmentOverdueWorker> logger,
        IBackgroundJobRunRecorder? recorder = null)
    {
        _recorder = recorder;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private bool Enabled => _configuration.GetValue<bool?>("EquipmentOverdue:Enabled") ?? true;

    private int IntervalMinutes =>
        _configuration.GetValue<int?>("EquipmentOverdue:IntervalMinutes") is > 0 and var m ? m : DefaultIntervalMinutes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            _logger.LogInformation("[{Worker}] Disabled via EquipmentOverdue:Enabled.", ProcessName);
            return;
        }

        _logger.LogInformation("[{Worker}] Starting. Every {Minutes} minutes.", ProcessName, IntervalMinutes);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(IntervalMinutes));

        // First scan shortly after start, then on the timer.
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

    // Scheduled pass: recorded when a recorder is present; otherwise a failure is logged and retried next tick.
    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ScheduledJobPass.RunAsync(_recorder, ProcessName, ScanAsync, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Worker}] Scan failed; it will run again on the next tick.", ProcessName);
        }
    }

    // One scan. Failures propagate so the recorder can mark the pass Failed.
    private async Task<int?> ScanAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>().CurrentSystemProcessName = ProcessName;
        var clock = scope.ServiceProvider.GetRequiredService<ILabClock>();
        var service = scope.ServiceProvider.GetRequiredService<EquipmentWorkService>();
        var changed = await service.ApplyOverdueAsync(clock.LabToday);
        if (changed > 0)
            _logger.LogInformation(
                "[{Worker}] Marked {Count} overdue instrument(s) as out of service.",
                ProcessName, changed);
        return changed;
    }
}
