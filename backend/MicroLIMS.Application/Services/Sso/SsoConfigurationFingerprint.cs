using System.Security.Cryptography;
using System.Text;
using MicroLIMS.Application.Abstractions.Sso;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// Computes a non-secret SHA-256 hex fingerprint of the configured SSO settings so
/// <see cref="SsoModeService"/> can detect when an admin changes configuration after a connection test.
/// </summary>
public sealed class SsoConfigurationFingerprint : ISsoConfigurationFingerprint
{
    private const string Unconfigured = "unconfigured";
    private readonly SsoOptions _options;

    public SsoConfigurationFingerprint(SsoOptions options)
    {
        _options = options;
    }

    public string GetFingerprint(SsoMode mode)
    {
        if (mode == SsoMode.ActiveDirectory)
            return GetLdapFingerprint();
        if (mode != SsoMode.OpenIdConnect)
            return Unconfigured;

        var oidc = _options.Oidc;
        if (string.IsNullOrWhiteSpace(oidc.Authority) && string.IsNullOrWhiteSpace(oidc.ClientId))
            return Unconfigured;

        var usernameClaim = string.IsNullOrWhiteSpace(oidc.UsernameClaim)
            ? OidcOptions.DefaultUsernameClaim
            : oidc.UsernameClaim.Trim();

        // Never include ClientSecret in the fingerprint: it is a secret and should not
        // appear in or derive stored audit metadata.
        var canonical = string.Join(
            '\n',
            "oidc",
            (oidc.Authority ?? string.Empty).Trim(),
            (oidc.ClientId ?? string.Empty).Trim(),
            usernameClaim,
            oidc.RequireMfaClaim ? "1" : "0",
            (oidc.RedirectUri ?? string.Empty).Trim());

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private string GetLdapFingerprint()
    {
        var ldap = _options.Ldap;
        if (string.IsNullOrWhiteSpace(ldap.Host) && string.IsNullOrWhiteSpace(ldap.BaseDn))
            return Unconfigured;

        // Never include BindPassword in the fingerprint: it is a secret and should not
        // appear in or derive stored audit metadata. Everything else that changes which
        // directory is contacted, how, or which account is searched is covered.
        var canonical = string.Join(
            '\n',
            "ldap",
            (ldap.Host ?? string.Empty).Trim().ToLowerInvariant(),
            ldap.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ldap.EffectiveUseLdaps ? "ldaps" : "starttls",
            (ldap.BaseDn ?? string.Empty).Trim(),
            (ldap.BindDn ?? string.Empty).Trim(),
            (ldap.UsernameAttribute ?? string.Empty).Trim(),
            (ldap.SubjectAttribute ?? string.Empty).Trim());

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
