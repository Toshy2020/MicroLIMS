using MicroLIMS.Application.Abstractions.Email;

namespace MicroLIMS.Infrastructure.Email;

// Describes the SMTP configuration for the admin Integrations tab. The
// password is read only to say whether authentication is configured; it is
// never copied into the result.
public class SmtpStatusProvider : ISmtpStatusProvider
{
    private readonly SmtpOptions _options;

    public SmtpStatusProvider(SmtpOptions options) => _options = options;

    public SmtpConfigurationInfo Describe() => new(
        IsConfigured: !string.IsNullOrWhiteSpace(_options.Host),
        Host: _options.Host?.Trim() ?? string.Empty,
        Port: _options.Port > 0 ? _options.Port : 587,
        FromAddress: _options.FromAddress ?? string.Empty,
        EnableSsl: _options.EnableSsl,
        AuthenticationConfigured: !string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrEmpty(_options.Password));
}
