using MicroLIMS.Infrastructure.Email;

namespace MicroLIMS.API.Extensions;

/// <summary>
/// Resolves and validates SMTP configuration from IConfiguration, supporting both
/// hierarchical section syntax ("Smtp:Host") and environment variable double-underscore syntax ("Smtp__Host").
/// </summary>
public static class SmtpConfiguration
{
    public const int DefaultPort = 587;
    public const string DefaultFromAddress = "no-reply@microlims.local";

    public static SmtpOptions Resolve(IConfiguration config)
    {
        var section = config.GetSection(SmtpOptions.SectionName);

        var host = (config["Smtp:Host"]
            ?? config["Smtp__Host"]
            ?? section["Host"]
            ?? string.Empty).Trim();

        var portRaw = config["Smtp:Port"]
            ?? config["Smtp__Port"]
            ?? section["Port"];

        var port = int.TryParse(portRaw, out var parsedPort) && parsedPort > 0
            ? parsedPort
            : DefaultPort;

        var username = (config["Smtp:Username"]
            ?? config["Smtp__Username"]
            ?? section["Username"]
            ?? string.Empty).Trim();

        var password = config["Smtp:Password"]
            ?? config["Smtp__Password"]
            ?? section["Password"]
            ?? string.Empty;

        var fromAddressRaw = config["Smtp:FromAddress"]
            ?? config["Smtp__FromAddress"]
            ?? section["FromAddress"];

        var fromAddress = string.IsNullOrWhiteSpace(fromAddressRaw)
            ? DefaultFromAddress
            : fromAddressRaw.Trim();

        var sslRaw = config["Smtp:EnableSsl"]
            ?? config["Smtp__EnableSsl"]
            ?? section["EnableSsl"];

        var enableSsl = bool.TryParse(sslRaw, out var parsedSsl)
            ? parsedSsl
            : true;

        return new SmtpOptions
        {
            Host = host,
            Port = port,
            Username = username,
            Password = password,
            FromAddress = fromAddress,
            EnableSsl = enableSsl
        };
    }
}
