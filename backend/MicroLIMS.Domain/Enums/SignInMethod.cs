namespace MicroLIMS.Domain.Enums;

/// <summary>
/// How a user signs in to MicroLIMS. Local users authenticate with their MicroLIMS password;
/// Directory users authenticate through the configured SSO provider and keep a separate
/// MicroLIMS signing password for electronic signatures.
/// </summary>
public enum SignInMethod
{
    Local = 0,
    Directory = 1
}
