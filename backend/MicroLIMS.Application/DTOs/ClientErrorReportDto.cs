using System.ComponentModel.DataAnnotations;

namespace MicroLIMS.Application.DTOs;

// What the browser is allowed to report when it crashes.
//
// Deliberately narrow. Everything here lands in ErrorLog, which is
// prunable non-GxP operational data - it must not become a side channel
// for laboratory records. The frontend sends location.pathname only,
// never the query string or hash, because a filtered list URL can carry
// sample references and result values.
//
// Lengths are enforced here as well as clamped on the client: the
// endpoint is anonymous by necessity (a render crash can happen before
// login), so nothing the browser sends can be trusted for size.
public class ClientErrorReportRequest
{
    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ErrorType { get; set; }

    [MaxLength(8000)]
    public string? Stack { get; set; }

    // React's componentStack from an ErrorBoundary - the component tree
    // that was rendering when it threw. Null for window-level handlers.
    [MaxLength(8000)]
    public string? ComponentStack { get; set; }

    // Route path only. The client strips query and hash before sending.
    [MaxLength(500)]
    public string? Route { get; set; }

    [MaxLength(400)]
    public string? UserAgent { get; set; }

    // Echoed back from the X-Correlation-Id of a recent failed API call,
    // so the browser error and the backend error from the same user
    // action land on one Incident. Validated server-side before use -
    // it is caller-controlled and would otherwise let anyone attach a
    // row to an arbitrary existing Incident.
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    // Where in the client this came from: "errorBoundary", "windowError"
    // or "unhandledRejection". Free-text but bounded; used only for the
    // ErrorLog.ExceptionType marker when ErrorType is absent.
    [MaxLength(50)]
    public string? Source { get; set; }
}
