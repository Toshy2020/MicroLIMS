using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Integrations;

public record IntegrationAgentDto(
    int Id, string Code, string Name, int EquipmentId, string? EquipmentCode, string KeyPrefix,
    string Status, DateTime? LastSeenAt, DateTime CreatedAt, DateTime? RevokedAt, string? RevokeReason);

public record SmtpStatusDto(
    bool IsConfigured, string Host, int Port, string FromAddress, bool EnableSsl, bool AuthenticationConfigured,
    string Password, DateTime? LastSendAtUtc, string? LastSendOutcome, string? LastSendError);

public record StorageStatusDto(string Provider, string Location, long? FreeBytes, long? TotalBytes);

public record ProbeStepDto(string Step, bool Succeeded, long ElapsedMs, string? Error);

public record StorageProbeResultDto(bool Succeeded, IReadOnlyList<ProbeStepDto> Steps, string? Warning);

public record DeliveryOutcomeDto(string Recipient, string Outcome, string? Error);

public record TestSendResultDto(int RecipientCount, int Sent, IReadOnlyList<DeliveryOutcomeDto> Outcomes);

public interface IIntegrationsService
{
    Task<IReadOnlyList<IntegrationAgentDto>> ListInstrumentAgentsAsync(CancellationToken cancellationToken = default);
    SmtpStatusDto GetSmtpStatus();
    Task<TestSendResultDto> SendTestEmailAsync(int userId, string? to, string? reason, CancellationToken cancellationToken = default);
    Task<TestSendResultDto> SendTestAlertAsync(int userId, string? reason, CancellationToken cancellationToken = default);
    StorageStatusDto GetStorageStatus();
    Task<StorageProbeResultDto> RunStorageProbeAsync(int userId, string? reason, CancellationToken cancellationToken = default);
}

// Read-mostly status of what the system talks to, plus the on-demand checks
// that prove it still can. Controls the system, never GxP data: nothing here
// is a regulated record, so a test send or probe is a security event (who,
// why, with what outcome) rather than an audit-trail entry. The test buttons
// are rate limited as a class so they cannot be used to flood a mailbox or
// hammer the storage provider.
public class IntegrationsService : IIntegrationsService
{
    internal const string ThrottleAlert = "integrations.alert-test";
    internal const string ThrottleEmail = "integrations.email-test";
    internal const string ThrottleProbe = "integrations.storage-probe";
    private const int MaxErrorLength = 200;

    private readonly IMicroLimsDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISmtpStatusProvider _smtp;
    private readonly EmailDeliveryTracker _tracker;
    private readonly IFileStorageService _storage;
    private readonly ISecurityAuditService _securityAudit;
    private readonly CriticalAlertOptions _alertOptions;
    private readonly AdminActionThrottle _throttle;
    private readonly ISystemSettingsReader? _settings;
    private readonly TimeProvider _time;

    public IntegrationsService(
        IMicroLimsDbContext db,
        IEmailSender email,
        ISmtpStatusProvider smtp,
        EmailDeliveryTracker tracker,
        IFileStorageService storage,
        ISecurityAuditService securityAudit,
        CriticalAlertOptions alertOptions,
        AdminActionThrottle throttle,
        ISystemSettingsReader? settings = null,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _email = email;
        _smtp = smtp;
        _tracker = tracker;
        _storage = storage;
        _securityAudit = securityAudit;
        _alertOptions = alertOptions;
        _throttle = throttle;
        _settings = settings;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<IntegrationAgentDto>> ListInstrumentAgentsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await (from a in _db.InstrumentAgents.AsNoTracking()
                          join e in _db.Equipment.AsNoTracking() on a.EquipmentId equals e.Id into eq
                          from e in eq.DefaultIfEmpty()
                          orderby a.IsActive descending, a.Code
                          select new { a.Id, a.Code, a.Name, a.EquipmentId, EquipmentCode = e != null ? e.Code : null, a.KeyPrefix,
                                       a.IsActive, a.LastSeenAt, a.CreatedAt, a.RevokedAt, a.RevokeReason })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new IntegrationAgentDto(
            r.Id, r.Code, r.Name, r.EquipmentId, r.EquipmentCode, r.KeyPrefix,
            !r.IsActive ? "Revoked" : r.LastSeenAt is null ? "Never seen" : "Active",
            r.LastSeenAt, r.CreatedAt, r.RevokedAt, r.RevokeReason)).ToList();
    }

    public SmtpStatusDto GetSmtpStatus()
    {
        var info = _smtp.Describe();
        var last = _tracker.Last;
        return new SmtpStatusDto(
            info.IsConfigured, info.Host, info.Port, info.FromAddress, info.EnableSsl, info.AuthenticationConfigured,
            Password: "********", last?.AtUtc, last?.Outcome, last?.Error);
    }

    public async Task<TestSendResultDto> SendTestEmailAsync(int userId, string? to, string? reason, CancellationToken cancellationToken = default)
    {
        var why = AdminActionReason.Require(reason, "send a test email");
        var address = to?.Trim().ToLowerInvariant();
        if (!EmailAddressList.IsValidAddress(address))
            throw new InvalidOperationException("Enter a valid email address to send the test to.");
        _throttle.Enter(ThrottleEmail);

        var subject = $"[MicroLIMS] TEST email - {_alertOptions.EnvironmentName}";
        var body = TestBody("an SMTP test email", why);
        var outcome = await SendOneAsync(address!, subject, body);

        var result = new TestSendResultDto(1, outcome.Outcome == "Sent" ? 1 : 0, new[] { outcome });
        await RecordAsync(SecurityEventCodes.SmtpTestEmailSent, userId, why, result, new { recipient = address }, cancellationToken);
        return result;
    }

    public async Task<TestSendResultDto> SendTestAlertAsync(int userId, string? reason, CancellationToken cancellationToken = default)
    {
        var why = AdminActionReason.Require(reason, "send a test alert");
        var recipients = await CriticalAlertRecipients.ResolveAsync(_settings, _alertOptions, cancellationToken);
        if (recipients.Count == 0)
            throw new InvalidOperationException($"No critical-alert recipients are configured. Set {SystemSettingKeys.AlertsCriticalRecipients} first.");
        _throttle.Enter(ThrottleAlert);

        var subject = $"[MicroLIMS] TEST ALERT - {_alertOptions.EnvironmentName}";
        var body = TestBody("a test of critical incident alerting", why);
        var outcomes = new List<DeliveryOutcomeDto>();
        foreach (var recipient in recipients)
            outcomes.Add(await SendOneAsync(recipient, subject, body));

        var result = new TestSendResultDto(recipients.Count, outcomes.Count(o => o.Outcome == "Sent"), outcomes);
        await RecordAsync(SecurityEventCodes.CriticalAlertTestSent, userId, why, result, null, cancellationToken);
        return result;
    }

    public StorageStatusDto GetStorageStatus()
    {
        if (_storage is IStorageDiagnostics diagnostics)
        {
            var d = diagnostics.Describe();
            return new StorageStatusDto(d.Provider, d.Location, d.FreeBytes, d.TotalBytes);
        }
        return new StorageStatusDto(_storage.GetType().Name, "Not reported by this provider", null, null);
    }

    public async Task<StorageProbeResultDto> RunStorageProbeAsync(int userId, string? reason, CancellationToken cancellationToken = default)
    {
        var why = AdminActionReason.Require(reason, "run the storage probe");
        _throttle.Enter(ThrottleProbe);

        var steps = new List<ProbeStepDto>();
        var payload = Encoding.UTF8.GetBytes($"MicroLIMS storage probe {Guid.NewGuid():N}");
        string? key = null;
        string? warning = null;

        var wrote = await StepAsync(steps, "Write", async () =>
            key = await _storage.SaveAsync($"_probe/{Guid.NewGuid():N}.txt", payload));
        if (wrote)
        {
            await StepAsync(steps, "Read back", async () =>
            {
                var read = await _storage.ReadAsync(key!);
                if (!read.AsSpan().SequenceEqual(payload))
                    throw new InvalidOperationException("The bytes read back differ from the bytes written.");
            });

            if (_storage is IStorageDiagnostics diagnostics)
            {
                if (!await StepAsync(steps, "Delete", () => diagnostics.DeleteAsync(key!)))
                    warning = $"The probe object {key} could not be removed and may need cleaning up by hand.";
            }
            else
            {
                warning = $"This storage provider cannot delete objects, so the probe object {key} was left in place.";
            }
        }

        var result = new StorageProbeResultDto(steps.All(s => s.Succeeded), steps, warning);

        _securityAudit.Record(new SecurityEventRequest(
            EventCode: SecurityEventCodes.StorageProbeRun,
            Outcome: result.Succeeded ? SecurityEventOutcome.Success : SecurityEventOutcome.Failure,
            ActorUserId: userId,
            Reason: why,
            Metadata: JsonSerializer.Serialize(new { provider = GetStorageStatus().Provider, steps = steps.Select(s => new { s.Step, s.Succeeded }) })));
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task<bool> StepAsync(List<ProbeStepDto> steps, string name, Func<Task> action)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            await action();
            steps.Add(new ProbeStepDto(name, true, watch.ElapsedMilliseconds, null));
            return true;
        }
        catch (Exception ex)
        {
            steps.Add(new ProbeStepDto(name, false, watch.ElapsedMilliseconds, Describe(ex)));
            return false;
        }
    }

    private async Task<DeliveryOutcomeDto> SendOneAsync(string recipient, string subject, string body)
    {
        if (!_smtp.Describe().IsConfigured)
            return new DeliveryOutcomeDto(recipient, "Skipped", "SMTP is not configured (Smtp:Host is empty), so nothing was sent.");
        try
        {
            await _email.SendAsync(recipient, subject, body);
            return new DeliveryOutcomeDto(recipient, "Sent", null);
        }
        catch (Exception ex)
        {
            return new DeliveryOutcomeDto(recipient, "Failed", Describe(ex));
        }
    }

    private async Task RecordAsync(string code, int userId, string reason, TestSendResultDto result, object? extra, CancellationToken cancellationToken)
    {
        _securityAudit.Record(new SecurityEventRequest(
            EventCode: code,
            Outcome: result.Sent == result.RecipientCount ? SecurityEventOutcome.Success : SecurityEventOutcome.Failure,
            ActorUserId: userId,
            Reason: reason,
            Metadata: JsonSerializer.Serialize(new { recipientCount = result.RecipientCount, sent = result.Sent, extra })));
        await _db.SaveChangesAsync(cancellationToken);
    }

    private string TestBody(string what, string reason) => string.Join(Environment.NewLine, new[]
    {
        $"THIS IS A TEST. This message is {what} sent from the MicroLIMS administration panel.",
        "It is not a real incident and needs no action.",
        "",
        $"Environment:  {_alertOptions.EnvironmentName}",
        $"Sent at:      {_time.GetUtcNow():u}",
        $"Reason:       {reason}",
        "",
        "This is an automated operational message. It is not a GxP record."
    });

    // Exception type and a short message only; storage and SMTP messages name hosts and buckets, never keys or passwords.
    private static string Describe(Exception ex)
    {
        var message = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] : ex.Message;
        return $"{ex.GetType().Name}: {message}";
    }
}
