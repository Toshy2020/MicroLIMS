using System.Net;
using System.Net.Mail;

namespace MicroLIMS.Infrastructure.Email;

// Real SMTP delivery via System.Net.Mail - no external package needed.
// Configured entirely from appsettings ("Smtp" section). If Smtp:Host
// is left blank (e.g. in local dev), sending is a safe no-op instead
// of throwing, so the rest of the app keeps working without a mail server.
public class EmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromAddress;
    private readonly bool _enableSsl;

    public EmailSender(string host, int port, string username, string password, string fromAddress, bool enableSsl)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
        _fromAddress = fromAddress;
        _enableSsl = enableSsl;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_host))
            return; // no SMTP configured - safe no-op for local/dev environments

        using var client = new SmtpClient(_host, _port)
        {
            Credentials = string.IsNullOrWhiteSpace(_username) ? null : new NetworkCredential(_username, _password),
            EnableSsl = _enableSsl
        };

        using var message = new MailMessage(_fromAddress, to, subject, body);
        await client.SendMailAsync(message);
    }
}
