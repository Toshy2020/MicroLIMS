namespace MicroLIMS.Application.Abstractions.Email;

// What the SMTP configuration looks like, without its secrets. There is
// deliberately no member that could return the password.
public record SmtpConfigurationInfo(
    bool IsConfigured, string Host, int Port, string FromAddress, bool EnableSsl, bool AuthenticationConfigured);

public interface ISmtpStatusProvider
{
    SmtpConfigurationInfo Describe();
}
