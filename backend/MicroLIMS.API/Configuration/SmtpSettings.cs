namespace MicroLIMS.API.Configuration;

using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Application.Abstractions.Email;

/// <summary>
/// SMTP configuration settings, aliasing <see cref="SmtpOptions"/> for consistency
/// with other options classes in the Configuration namespace.
/// </summary>
public class SmtpSettings : SmtpOptions
{
}
