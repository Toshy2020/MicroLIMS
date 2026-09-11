using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using Microsoft.Extensions.Logging;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Settings for Critical incident alerting. Disabled unless explicitly
// switched on, so no environment mails anyone by accident - a developer
// running locally with a real Smtp:Host configured would otherwise start
// paging whoever is on the recipient list.
public record CriticalAlertOptions(bool Enabled, string? Recipient, string EnvironmentName);

// Sends one email when an Incident reaches Critical severity.
//
// It works on the Incident rather than the ErrorLog, which is what makes
// it source-agnostic: a Critical incident raised by the backend exception
// middleware, by a browser error report, or by the database health worker
// all arrive here the same way, and one outage producing fifty ErrorLog
// rows still produces one alert.
//
// This is operational notification, not evidence. Incident and ErrorLog
// are on the CaptureAuditEntries deny-list, so nothing written here
// reaches the GxP audit trail.
public class CriticalAlertService : ICriticalAlertService
{
    private const int MaxSummaryLength = 300;

    private readonly MicroLimsDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly CriticalAlertOptions _options;
    private readonly ILogger<CriticalAlertService> _logger;

    public CriticalAlertService(
        MicroLimsDbContext db,
        IEmailSender emailSender,
        CriticalAlertOptions options,
        ILogger<CriticalAlertService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _options = options;
        _logger = logger;
    }

    // Returns how many alerts were sent. Never throws: an alerting failure
    // must not become a second incident, and must never interrupt whatever
    // laboratory work produced the original error.
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Recipient))
            return 0;

        List<Domain.Entities.Incident> pending;
        try
        {
            pending = await _db.Incidents
                .Where(i => i.Severity == ErrorSeverity.Critical
                            && i.AlertedAtUtc == null
                            // An incident already triaged away does not
                            // need to page anyone - see the resolution
                            // rule: resolving never sends an alert.
                            && i.Status == IncidentStatus.Open)
                .OrderBy(i => i.FirstSeenUtc)
                .Take(20)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not query incidents awaiting a Critical alert.");
            return 0;
        }

        var sent = 0;
        foreach (var incident in pending)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (await TrySendAsync(incident, cancellationToken)) sent++;
        }

        return sent;
    }

    private async Task<bool> TrySendAsync(Domain.Entities.Incident incident, CancellationToken cancellationToken)
    {
        try
        {
            var occurrences = await _db.ErrorLogs
                .CountAsync(e => e.IncidentId == incident.Id, cancellationToken);

            var sources = await _db.ErrorLogs
                .Where(e => e.IncidentId == incident.Id)
                .Select(e => e.Source)
                .Distinct()
                .ToListAsync(cancellationToken);

            await _emailSender.SendAsync(
                _options.Recipient!,
                $"[MicroLIMS] Critical incident - {_options.EnvironmentName}",
                BuildBody(incident, occurrences, sources));

            // Stamped only after the send returns. A failure leaves it null
            // so the next pass retries; the worst case is a duplicate alert
            // for a send that succeeded but whose stamp did not commit,
            // which is preferable to a Critical incident nobody hears about.
            incident.AlertedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Critical alert sent for incident {IncidentId} (CorrelationId {CorrelationId}).",
                incident.Id, incident.CorrelationId);
            return true;
        }
        catch (Exception ex)
        {
            // Logged, never rethrown. The incident stays unalerted and is
            // retried on the next pass.
            _logger.LogError(ex,
                "Failed to send the Critical alert for incident {IncidentId}. It remains unalerted and will be retried.",
                incident.Id);
            return false;
        }
    }

    // Deliberately terse. No stack trace, no exception message, no request
    // body, no headers - anything that could carry a token, a credential or
    // a laboratory result stays in the incident record behind
    // authentication, and the operator is pointed at it instead.
    private string BuildBody(Domain.Entities.Incident incident, int occurrences, List<ErrorSource> sources)
    {
        var summary = incident.Summary.Length > MaxSummaryLength
            ? incident.Summary[..MaxSummaryLength]
            : incident.Summary;

        return string.Join(Environment.NewLine, new[]
        {
            "A Critical incident has been recorded in MicroLIMS.",
            "",
            $"Environment:    {_options.EnvironmentName}",
            $"Incident:       {incident.Id}",
            $"Summary:        {summary}",
            $"Severity:       {incident.Severity}",
            $"First seen:     {incident.FirstSeenUtc:u}",
            $"Last seen:      {incident.LastSeenUtc:u}",
            $"Occurrences:    {occurrences}",
            $"Source(s):      {(sources.Count > 0 ? string.Join(", ", sources) : "Unknown")}",
            $"CorrelationId:  {incident.CorrelationId}",
            "",
            "Open the Error Monitoring page in MicroLIMS for the full detail.",
            "This message contains no error content by design.",
            "",
            "This is an automated operational notification. It is not a GxP record."
        });
    }
}
