using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

// One captured error, as handed to IErrorCaptureService by whichever
// middleware/service caught it. Deliberately a flat DTO rather than the
// Exception itself: Phase 4 reports client-side errors that have no
// .NET exception behind them at all.
public record ErrorCaptureRequest(
    ErrorSource Source,
    ErrorSeverity Severity,
    string CorrelationId,
    string ExceptionType,
    string Message,
    string? StackTrace = null,
    string? RequestPath = null,
    string? HttpMethod = null,
    int? StatusCode = null,
    int? UserId = null,
    string? RawContext = null,
    // Overrides the auto-generated Incident summary. Exception-based
    // callers leave this null and get "<Type> - <METHOD> <path>";
    // polled database findings have no request path, so without an
    // override every one of them would read just "SlowQuery".
    string? Summary = null);

// Writes ErrorLog rows and attaches them to their parent Incident.
//
// Implementations must never throw: an error-capture failure must not
// change the response the caller was already sending. Registered as a
// singleton and opens its own DbContext scope, because the caller's
// request-scoped context is typically mid-exception with a dirty
// ChangeTracker and would re-throw the original failure if reused.
public interface IErrorCaptureService
{
    Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default);
}
