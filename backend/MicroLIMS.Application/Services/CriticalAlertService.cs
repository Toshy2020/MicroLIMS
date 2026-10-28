using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using Microsoft.Extensions.Logging;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.SystemSettings;

namespace MicroLIMS.Application.Services;

// Settings for Critical incident alerting. Disabled unless explicitly
// switched on, so no environment mails anyone by accident - a developer
// running locally with a real Smtp:Host configured would otherwise start
// paging whoever is on the recipient list.
//
// Recipient is the legacy single-address configuration value. When the settings
// registry is available the recipients come from Alerts.CriticalRecipients
// (database, then this same legacy key, then empty); Recipient is used only
// when there is no registry, e.g. in unit tests and tools.
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

    private readonly IMicroLimsDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly CriticalAlertOptions _options;
    private readonly ILogger<CriticalAlertService> _logger;
    private readonly TimeProvider _time;
    private readonly ISystemSettingsReader? _settings;

    public CriticalAlertService(
        IMicroLimsDbContext db,
        IEmailSender emailSender,
        CriticalAlertOptions options,
        ILogger<CriticalAlertService> logger,
        TimeProvider? timeProvider = null,
        ISystemSettingsReader? settings = null)
    {
        _settings = settings;
        _time = timeProvider ?? TimeProvider.System;
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
        if (!_options.Enabled)
            return 0;

        IReadOnlyList<string> recipients;
        try
        {
            recipients = await ResolveRecipientsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read the Critical alert recipients.");
            return 0;
        }
        if (recipients.Count == 0)
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
            if (await TrySendAsync(incident, recipients, cancellationToken)) sent++;
        }

        return sent;
    }

    // Shared with the test alert so both go to the same people.
    public Task<IReadOnlyList<string>> ResolveRecipientsAsync(CancellationToken cancellationToken = default) =>
        CriticalAlertRecipients.ResolveAsync(_settings, _options, cancellationToken);

    private async Task<bool> TrySendAsync(Domain.Entities.Incident incident, IReadOnlyList<string> recipients, CancellationToken cancellationToken)
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

            var subject = $"[MicroLIMS] Critical incident - {_options.EnvironmentName}";
            var body = BuildBody(incident, occurrences, sources);

            // Each recipient is tried on its own so one bad mailbox does not
            // keep the others from hearing about the incident.
            var delivered = 0;
            foreach (var recipient in recipients)
            {
                try
                {
                    await _emailSender.SendAsync(recipient, subject, body);
                    delivered++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send the Critical alert for incident {IncidentId} to one recipient.", incident.Id);
                }
            }
            if (delivered == 0)
                throw new InvalidOperationException("The alert could not be delivered to any recipient.");

            // Stamped only after at least one send returns. A total failure
            // leaves it null so the next pass retries; the worst case is a
            // duplicate alert for a send that succeeded but whose stamp did
            // not commit, which is preferable to a Critical incident nobody
            // hears about. A partial failure is not retried, to avoid
            // mailing the recipients that already have it again.
            incident.AlertedAtUtc = _time.GetUtcNow().UtcDateTime;
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

public static class CriticalAlertRecipients
{
    public static async Task<IReadOnlyList<string>> ResolveAsync(
        ISystemSettingsReader? settings, CriticalAlertOptions options, CancellationToken cancellationToken = default)
    {
        var raw = settings is null
            ? options.Recipient
            : await settings.GetStringAsync(SystemSettingKeys.AlertsCriticalRecipients, cancellationToken);
        // Only valid addresses are ever mailed: a value edited outside the application is filtered, not trusted.
        return EmailAddressList.Parse(raw).Where(EmailAddressList.IsValidAddress).ToList();
    }
}
