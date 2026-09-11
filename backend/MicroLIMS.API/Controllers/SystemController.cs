using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using System.Security.Claims;
using System.Text.Json;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    // Bounded before storage regardless of what the client claims, and
    // independently of the DTO's MaxLength - model validation can be
    // bypassed by a malformed body that still binds.
    private const int MessageMaxLength = 2000;
    private const int StackMaxLength = 8000;
    private const int RouteMaxLength = 500;
    private const int UserAgentMaxLength = 400;
    private const int CorrelationIdMaxLength = 100;

    private readonly IErrorCaptureService _errorCapture;

    public SystemController(IErrorCaptureService errorCapture)
    {
        _errorCapture = errorCapture;
    }

    // Anonymous by necessity: a React render crash can happen on the login
    // screen, or after a token expires, and those are exactly the crashes
    // worth hearing about. That makes this a public write into the
    // database, so it is rate limited per client address and size capped -
    // see RateLimitingExtensions.
    //
    // Always returns 202 with no body. The browser is already in a failed
    // state; telling it the report failed only invites a retry loop, and
    // any detail here would leak whether an Incident already exists.
    [HttpPost("client-errors")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.ClientErrors)]
    [RequestSizeLimit(32 * 1024)]
    public async Task<IActionResult> ReportClientError(
        [FromBody] ClientErrorReportRequest request,
        [FromServices] MicroLimsDbContext db,
        CancellationToken cancellationToken)
    {
        var correlationId = ResolveCorrelationId(request.CorrelationId);

        var source = Truncate(request.Source, 50);
        var exceptionType = Truncate(request.ErrorType, 200)
            ?? (source switch
            {
                "errorBoundary" => "ReactRenderError",
                "unhandledRejection" => "UnhandledRejection",
                _ => "ClientError"
            });

        // A render crash takes the whole screen down with it, which is the
        // frontend equivalent of an unhandled backend exception. The
        // window-level handlers catch things the app often survives, so
        // they do not get the same weight.
        var severity = source == "errorBoundary"
            ? ErrorSeverity.Critical
            : ErrorSeverity.Error;

        // Present only when the crash happened while a valid token was
        // attached; anonymous reports are expected and fine.
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true &&
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsed))
        {
            userId = parsed;
        }
        userId ??= db.CurrentUserId;

        var stack = Truncate(request.Stack, StackMaxLength);
        var componentStack = Truncate(request.ComponentStack, StackMaxLength);
        var route = Truncate(request.Route, RouteMaxLength);

        await _errorCapture.CaptureAsync(new ErrorCaptureRequest(
            Source: ErrorSource.Frontend,
            Severity: severity,
            CorrelationId: correlationId,
            ExceptionType: exceptionType,
            Message: Truncate(request.Message, MessageMaxLength) ?? string.Empty,
            // The component stack is the more useful of the two for a
            // render crash, so it leads when present.
            StackTrace: componentStack is null
                ? stack
                : stack is null ? componentStack : $"{componentStack}\n\n--- JS stack ---\n{stack}",
            RequestPath: route,
            HttpMethod: null,
            StatusCode: null,
            UserId: userId,
            RawContext: JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["kind"] = "clientError",
                ["source"] = source,
                ["route"] = route,
                ["userAgent"] = Truncate(request.UserAgent, UserAgentMaxLength),
                // Distinguishes "the browser knew the failing request" from
                // "we generated one for this report", which changes whether
                // the Incident is really shared with a backend error.
                ["correlatedWithRequest"] = !string.IsNullOrEmpty(request.CorrelationId)
            }),
            Summary: BuildSummary(exceptionType, route)), cancellationToken);

        return Accepted();
    }

    // The client echoes back the X-Correlation-Id of the API call that
    // failed, which is what ties its error to the backend's. That value is
    // caller-controlled, so it is accepted only in the shape this system
    // issues - otherwise anyone could append rows to an arbitrary existing
    // Incident. Falls back to this request's own id.
    private string ResolveCorrelationId(string? claimed)
    {
        if (!string.IsNullOrWhiteSpace(claimed) && claimed.Length <= CorrelationIdMaxLength)
        {
            var valid = true;
            foreach (var c in claimed)
            {
                if (c < '!' || c > '~') { valid = false; break; }
            }
            if (valid) return claimed;
        }

        return CorrelationIdMiddleware.GetCorrelationId(HttpContext);
    }

    private static string BuildSummary(string exceptionType, string? route) =>
        string.IsNullOrWhiteSpace(route)
            ? $"{exceptionType} (client)"
            : $"{exceptionType} - {route} (client)";

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
