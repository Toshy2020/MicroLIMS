using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Persists captured errors as ErrorLog rows grouped under an Incident.
//
// Incident and ErrorLog are on the CaptureAuditEntries deny-list, so
// nothing written here reaches the GxP audit trail - this is prunable
// operational data, not quality evidence.
public class ErrorCaptureService : IErrorCaptureService
{
    // Must match the column lengths in ErrorLogConfiguration /
    // IncidentConfiguration - an oversized value would fail the insert,
    // and losing the tail of a message beats losing the whole error.
    private const int CorrelationIdMaxLength = 100;
    private const int ExceptionTypeMaxLength = 300;
    private const int RequestPathMaxLength = 500;
    private const int HttpMethodMaxLength = 10;
    private const int SummaryMaxLength = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErrorCaptureService> _logger;

    public ErrorCaptureService(IServiceScopeFactory scopeFactory, ILogger<ErrorCaptureService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            // Swallowed on purpose. The caller is already handling an
            // error and is mid-response; a capture failure must not turn
            // one incident into two, or replace the caller's response.
            // The console/host log is the only remaining sink here - and
            // when the database itself is what failed, it is the only one
            // that can work at all.
            _logger.LogError(ex,
                "Failed to capture error log entry (CorrelationId {CorrelationId}, original {ExceptionType}: {Message})",
                request.CorrelationId, request.ExceptionType, request.Message);
        }
    }

    private async Task WriteAsync(ErrorCaptureRequest request, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();

        var now = DateTime.UtcNow;
        var correlationId = Truncate(request.CorrelationId, CorrelationIdMaxLength) ?? string.Empty;
        var exceptionType = Truncate(request.ExceptionType, ExceptionTypeMaxLength) ?? string.Empty;
        var requestPath = Truncate(request.RequestPath, RequestPathMaxLength);
        var httpMethod = Truncate(request.HttpMethod, HttpMethodMaxLength);

        var incident = await db.Incidents
            .FirstOrDefaultAsync(i => i.CorrelationId == correlationId, cancellationToken);

        if (incident is null)
        {
            incident = new Incident
            {
                CorrelationId = correlationId,
                FirstSeenUtc = now,
                LastSeenUtc = now,
                Status = IncidentStatus.Open,
                Severity = request.Severity,
                Summary = Truncate(request.Summary, SummaryMaxLength)
                    ?? BuildSummary(exceptionType, httpMethod, requestPath)
            };
            db.Incidents.Add(incident);
        }
        else
        {
            incident.LastSeenUtc = now;

            // Roll the incident up to the max of its children, honouring
            // any admin override, plus the entry being attached now. The
            // new row is not saved yet, so it is folded in separately.
            var existingMax = await db.ErrorLogs
                .Where(e => e.IncidentId == incident.Id)
                .Select(e => (int?)(e.SeverityOverride ?? e.Severity))
                .MaxAsync(cancellationToken);

            var rolledUp = Math.Max((int)request.Severity, existingMax ?? (int)request.Severity);
            incident.Severity = (ErrorSeverity)rolledUp;
        }

        db.ErrorLogs.Add(new ErrorLog
        {
            IncidentId = incident.Id,
            Source = request.Source,
            Severity = request.Severity,
            ExceptionType = exceptionType,
            Message = request.Message,
            StackTrace = request.StackTrace,
            RequestPath = requestPath,
            HttpMethod = httpMethod,
            StatusCode = request.StatusCode,
            UserId = request.UserId,
            CorrelationId = correlationId,
            OccurredAtUtc = now,
            RawContext = request.RawContext
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildSummary(string exceptionType, string? httpMethod, string? requestPath)
    {
        // Namespace-qualified type names are noise in a list view.
        var shortType = exceptionType;
        var lastDot = shortType.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < shortType.Length - 1)
            shortType = shortType[(lastDot + 1)..];

        var where = string.IsNullOrWhiteSpace(requestPath)
            ? null
            : string.IsNullOrWhiteSpace(httpMethod) ? requestPath : $"{httpMethod} {requestPath}";

        var summary = where is null ? shortType : $"{shortType} - {where}";
        return Truncate(summary, SummaryMaxLength) ?? shortType;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
