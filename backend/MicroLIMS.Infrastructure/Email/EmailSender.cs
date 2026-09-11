using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MicroLIMS.Infrastructure.Email;

// Real SMTP delivery via System.Net.Mail - no external package needed.
// Configured entirely from appsettings ("Smtp" section), environment variables,
// or user-secrets. If Smtp:Host is left blank (e.g. in local dev), sending is a
// safe no-op instead of throwing, so the rest of the app keeps working without a mail server.
public class EmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromAddress;
    private readonly bool _enableSsl;

    public string Host => _host;
    public int Port => _port;
    public string Username => _username;
    public string FromAddress => _fromAddress;
    public bool EnableSsl => _enableSsl;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_host);

    public EmailSender(string host, int port, string username, string password, string fromAddress, bool enableSsl)
    {
        _host = host?.Trim() ?? string.Empty;
        _port = port > 0 ? port : 587;
        _username = username?.Trim() ?? string.Empty;
        _password = password ?? string.Empty;
        _fromAddress = string.IsNullOrWhiteSpace(fromAddress) ? "no-reply@microlims.local" : fromAddress.Trim();
        _enableSsl = enableSsl;
    }

    public EmailSender(SmtpOptions options)
        : this(options.Host, options.Port, options.Username, options.Password, options.FromAddress, options.EnableSsl)
    {
    }

    public EmailSender(IOptions<SmtpOptions> options)
        : this(options.Value)
    {
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(to))
            return; // no SMTP configured or no recipient - safe no-op for local/dev environments

        using var client = new SmtpClient(_host, _port)
        {
            Credentials = string.IsNullOrWhiteSpace(_username) ? null : new NetworkCredential(_username, _password),
            EnableSsl = _enableSsl
        };

        using var message = new MailMessage(_fromAddress, to, subject, body);
        await client.SendMailAsync(message);
    }

    public override string ToString() =>
        $"Host={_host}, Port={_port}, Username={_username}, FromAddress={_fromAddress}, EnableSsl={_enableSsl}, Password=[REDACTED]";
}
