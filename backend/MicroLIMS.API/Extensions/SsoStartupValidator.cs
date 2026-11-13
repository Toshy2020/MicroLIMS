using MicroLIMS.Application.Services.Sso;

namespace MicroLIMS.API.Extensions;

/// <summary>
/// Fails startup before the host serves requests when <c>security.sso.mode</c> is
/// <see cref="SsoMode.OpenIdConnect"/> and any required <c>Sso:Oidc</c> setting is missing.
/// </summary>
public static class SsoStartupValidator
{
    public static async Task EnsureValidAsync(
        SsoModeService ssoMode,
        SsoOptions options,
        CancellationToken ct = default)
    {
        var mode = await ssoMode.GetModeAsync(ct);
        if (mode != SsoMode.OpenIdConnect)
            return;

        if (string.IsNullOrWhiteSpace(options.Oidc.Authority))
            throw new InvalidOperationException("Missing required SSO configuration 'Sso:Oidc:Authority' when 'security.sso.mode' is OpenIdConnect.");

        if (string.IsNullOrWhiteSpace(options.Oidc.ClientId))
            throw new InvalidOperationException("Missing required SSO configuration 'Sso:Oidc:ClientId' when 'security.sso.mode' is OpenIdConnect.");

        if (string.IsNullOrWhiteSpace(options.Oidc.ClientSecret))
            throw new InvalidOperationException("Missing required SSO configuration 'Sso:Oidc:ClientSecret' when 'security.sso.mode' is OpenIdConnect.");
    }
}
