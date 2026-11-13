using MicroLIMS.Application.Abstractions.Sso;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// Placeholder fingerprint provider used until OIDC/AD configuration options are wired.
/// </summary>
public sealed class DefaultSsoConfigurationFingerprint : ISsoConfigurationFingerprint
{
    public const string UnconfiguredValue = "unconfigured";

    public string GetFingerprint(SsoMode mode) => UnconfiguredValue;
}
