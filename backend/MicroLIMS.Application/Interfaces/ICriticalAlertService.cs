namespace MicroLIMS.Application.Interfaces;

// Sends operator notification for Critical incidents. Implementations must
// never throw: an alerting failure must not interrupt the laboratory work
// that produced the original error, nor become an incident of its own.
public interface ICriticalAlertService
{
    // Alerts every Critical incident that has not been alerted yet, and
    // returns how many were sent.
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default);
}
