using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.BackgroundJobs;
using MicroLIMS.Application.Services.Sso;

namespace MicroLIMS.API.BackgroundServices;

// Hourly check of directory-linked users against the company directory (Active Directory mode only;
// the service does nothing in any other mode). A scope is created per run: the service and the
// database context are scoped, this worker is a singleton.
public class DirectoryLeaverWorker : BackgroundService
{
    public const string ProcessName = DirectoryLeaverService.ProcessName;
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DirectoryLeaverWorker> _logger;
    private readonly IBackgroundJobRunRecorder? _recorder;

    public DirectoryLeaverWorker(
        IServiceScopeFactory scopeFactory, ILogger<DirectoryLeaverWorker> logger,
        IBackgroundJobRunRecorder? recorder = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _recorder = recorder;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Worker}] Starting. Every {Minutes} minutes.", ProcessName, Interval.TotalMinutes);
        using var timer = new PeriodicTimer(Interval);
        try
        {
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

    // A failed run is logged and the loop carries on with the next tick.
    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ScheduledJobPass.RunAsync(_recorder, ProcessName, RunOnceAsync, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Worker}] Run failed; it will run again on the next tick.", ProcessName);
        }
    }

    internal async Task<int?> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>().CurrentSystemProcessName = ProcessName;
        var service = scope.ServiceProvider.GetRequiredService<DirectoryLeaverService>();
        var result = await service.RunAsync(cancellationToken);
        if (result.Deactivated > 0 || result.DirectoryUnavailable)
            _logger.LogInformation(
                "[{Worker}] Checked {Checked}, deactivated {Deactivated}, directory unavailable: {Unavailable}.",
                ProcessName, result.Checked, result.Deactivated, result.DirectoryUnavailable);
        return result.Deactivated;
    }
}
