using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.Monitoring;

namespace MicroLIMS.API.BackgroundServices;

// Opens deviations for the Annual Monitoring Plan: occurrences that were
// missed and approved results over their action limit. The scan is
// idempotent, so running often costs nothing and a restart loses nothing.
public class MonitoringDeviationWorker : BackgroundService
{
    public const string ProcessName = "MonitoringDeviationWorker";
    public const int DefaultIntervalMinutes = 60;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MonitoringDeviationWorker> _logger;

    public MonitoringDeviationWorker(
        IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MonitoringDeviationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private bool Enabled => _configuration.GetValue<bool?>("MonitoringDeviations:Enabled") ?? true;

    private int IntervalMinutes =>
        _configuration.GetValue<int?>("MonitoringDeviations:IntervalMinutes") is > 0 and var m ? m : DefaultIntervalMinutes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            _logger.LogInformation("[{Worker}] Disabled via MonitoringDeviations:Enabled.", ProcessName);
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

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>().CurrentSystemProcessName = ProcessName;
            var service = scope.ServiceProvider.GetRequiredService<MonitoringDeviationService>();
            var result = await service.DetectAsync();
            if (result.MissedOpened + result.ExcursionsOpened > 0)
                _logger.LogInformation(
                    "[{Worker}] Opened {Missed} missed and {Excursions} excursion deviation(s); {FollowUps} follow-up sampling occurrence(s).",
                    ProcessName, result.MissedOpened, result.ExcursionsOpened, result.FollowUpsCreated);
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
}
