using MicroLIMS.Application.Abstractions.Email;

namespace MicroLIMS.Application.Services.Integrations;

public record EmailDeliveryResult(DateTime AtUtc, string Outcome, string? Error);

// Remembers the outcome of the last email send in this process, for the
// Integrations tab. In memory only: it is operational status, not a record.
public class EmailDeliveryTracker
{
    private EmailDeliveryResult? _last;
    public EmailDeliveryResult? Last => Volatile.Read(ref _last);
    public void Record(EmailDeliveryResult result) => Volatile.Write(ref _last, result);
}

// Wraps the real sender so every send, whoever asked for it, updates the tracker.
// The error kept is the exception type and a short message - never the recipient or body.
public class TrackingEmailSender : IEmailSender
{
    private readonly IEmailSender _inner;
    private readonly EmailDeliveryTracker _tracker;
    private readonly ISmtpStatusProvider _status;
    private readonly TimeProvider _time;

    public TrackingEmailSender(IEmailSender inner, EmailDeliveryTracker tracker, ISmtpStatusProvider status, TimeProvider? timeProvider = null)
    {
        _inner = inner;
        _tracker = tracker;
        _status = status;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        var configured = _status.Describe().IsConfigured;
        try
        {
            await _inner.SendAsync(to, subject, body);
            _tracker.Record(new EmailDeliveryResult(_time.GetUtcNow().UtcDateTime, configured ? "Sent" : "Skipped", configured ? null : "SMTP is not configured."));
        }
        catch (Exception ex)
        {
            var message = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message;
            _tracker.Record(new EmailDeliveryResult(_time.GetUtcNow().UtcDateTime, "Failed", $"{ex.GetType().Name}: {message}"));
            throw;
        }
    }
}
