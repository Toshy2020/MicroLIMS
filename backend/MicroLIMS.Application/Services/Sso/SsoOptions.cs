namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// Configuration bound from the <c>Sso</c> configuration section.
/// </summary>
public sealed class SsoOptions
{
    public const string SectionName = "Sso";

    public OidcOptions Oidc { get; set; } = new();
}

/// <summary>
/// OpenID Connect provider configuration bound from <c>Sso:Oidc</c>.
/// </summary>
public sealed class OidcOptions
{
    public const string DefaultUsernameClaim = "preferred_username";

    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string UsernameClaim { get; set; } = DefaultUsernameClaim;
    public bool RequireMfaClaim { get; set; }

    // The callback address registered with the provider. Set it when MicroLIMS runs
    // behind a reverse proxy; when blank the address is built from the request.
    public string? RedirectUri { get; set; }
}
