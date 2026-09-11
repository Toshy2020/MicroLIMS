using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.API.BackgroundServices;

// Drives Critical incident alerting off the request thread.
//
// Alerting deliberately does not happen inline in ErrorCaptureService: an
// SMTP round trip on the path that is already handling an error would slow
// - and could fail - the laboratory operation that hit it. Polling also
// catches an incident that only becomes Critical later, when a more severe
// ErrorLog attaches to it and the rolled-up severity rises.
//
// Durability comes from Incident.AlertedAtUtc rather than from this
// worker's memory, so a restart mid-outage neither loses a pending alert
// nor resends one that already went out.
public class CriticalAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CriticalAlertWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly bool _enabled;

    public CriticalAlertWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CriticalAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = configuration.GetValue("ErrorMonitoring:CriticalAlerts:Enabled", false);
        var seconds = configuration.GetValue("ErrorMonitoring:CriticalAlerts:PollIntervalSeconds", 60);
        _interval = TimeSpan.FromSeconds(Math.Max(15, seconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation(
                "Critical incident alerting is disabled (ErrorMonitoring:CriticalAlerts:Enabled). No alerts will be sent.");
            return;
        }

        _logger.LogInformation("Critical incident alerting is enabled, polling every {IntervalSeconds}s.",
            _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var alerts = scope.ServiceProvider.GetRequiredService<ICriticalAlertService>();
                await alerts.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The service already swallows its own failures; this is
                // the last resort so a fault here cannot stop the loop or
                // take the host down.
                _logger.LogError(ex, "Critical alert dispatch failed. Retrying on the next interval.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
