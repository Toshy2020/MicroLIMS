namespace MicroLIMS.Infrastructure.Email;

/// <summary>
/// Strongly-typed configuration options for the SMTP email delivery service.
/// Bound from the "Smtp" section in appsettings.json, environment variables (Smtp__*),
/// or .NET user-secrets in development.
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>
    /// The SMTP relay server hostname (e.g. "smtp.sendgrid.net", "localhost").
    /// If empty or whitespace, email delivery is disabled (safe no-op).
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// The SMTP port. Defaults to 587 (submission with STARTTLS).
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// The SMTP authentication username. If empty, anonymous sending is attempted.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The SMTP authentication password or API key. Kept secret and never logged.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The sender email address on outgoing messages (e.g. "no-reply@microlims.local").
    /// </summary>
    public string FromAddress { get; set; } = "no-reply@microlims.local";

    /// <summary>
    /// Whether SSL/TLS is enabled for the connection. Defaults to true.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// Returns true if an SMTP host has been configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    public override string ToString() =>
        $"Host={Host}, Port={Port}, Username={Username}, FromAddress={FromAddress}, EnableSsl={EnableSsl}, Password=[REDACTED]";
}
