using MicroLIMS.Application.Services.Sso;

namespace MicroLIMS.Application.Abstractions.Sso;

/// <summary>
/// Computes a canonical fingerprint from the non-secret SSO configuration values for a mode
/// so recorded connection-test passes are invalidated whenever the configuration changes.
/// </summary>
public interface ISsoConfigurationFingerprint
{
    string GetFingerprint(SsoMode mode);
}
