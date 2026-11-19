using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Services.Sso;

namespace MicroLIMS.Infrastructure.Sso;

/// <summary>
/// OpenID Connect protocol client backed by <see cref="ConfigurationManager{OpenIdConnectConfiguration}"/>
/// and <see cref="JwtSecurityTokenHandler"/>. Supports authorization-code + PKCE (S256) and
/// validates ID token signature, issuer, audience, lifetime (2-minute skew), nonce, MFA claim,
/// and configured username claim.
/// </summary>
public sealed class OidcProviderClient : IOidcProviderClient
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(2);

    private readonly SsoOptions _options;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _time;
    private readonly ILogger<OidcProviderClient> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    private ConfigurationManager<OpenIdConnectConfiguration>? _configManager;
    private string? _configManagerAuthority;

    public OidcProviderClient(
        SsoOptions options,
        HttpClient httpClient,
        TimeProvider? timeProvider = null,
        ILogger<OidcProviderClient>? logger = null)
    {
        _options = options;
        _httpClient = httpClient;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<OidcProviderClient>.Instance;
        _tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
    }

    public async Task<Uri> BuildAuthorizeUriAsync(
        string state,
        string nonce,
        string codeChallenge,
        string redirectUri,
        CancellationToken ct = default)
    {
        var config = await GetDiscoveryConfigurationAsync(ct);
        if (string.IsNullOrWhiteSpace(config.AuthorizationEndpoint))
            throw new InvalidOperationException("OIDC discovery document does not specify an authorization_endpoint.");

        var clientId = RequireConfigured(_options.Oidc.ClientId, "Sso:Oidc:ClientId");

        var query = string.Join('&', new[]
        {
            $"response_type={Uri.EscapeDataString("code")}",
            $"scope={Uri.EscapeDataString("openid profile email")}",
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"state={Uri.EscapeDataString(state)}",
            $"nonce={Uri.EscapeDataString(nonce)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            $"code_challenge_method={Uri.EscapeDataString("S256")}"
        });

        var separator = config.AuthorizationEndpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return new Uri($"{config.AuthorizationEndpoint}{separator}{query}");
    }

    public async Task<DirectoryIdentity> RedeemAsync(
        string code,
        string codeVerifier,
        string expectedNonce,
        string redirectUri,
        CancellationToken ct = default)
    {
        var config = await GetDiscoveryConfigurationAsync(ct);
        if (string.IsNullOrWhiteSpace(config.TokenEndpoint))
            throw new OidcValidationException(OidcValidationException.CodeTokenExchangeFailed, "OIDC discovery document does not specify a token_endpoint.");

        var clientId = RequireConfigured(_options.Oidc.ClientId, "Sso:Oidc:ClientId");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = codeVerifier
        };
        if (!string.IsNullOrWhiteSpace(_options.Oidc.ClientSecret))
            form["client_secret"] = _options.Oidc.ClientSecret;

        using var request = new HttpRequestMessage(HttpMethod.Post, config.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new OidcValidationException(
                OidcValidationException.CodeTokenExchangeFailed,
                $"OIDC token endpoint returned HTTP {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        string? idToken;
        try
        {
            using var doc = JsonDocument.Parse(json);
            idToken = doc.RootElement.TryGetProperty("id_token", out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            throw new OidcValidationException(OidcValidationException.CodeTokenExchangeFailed, "OIDC token endpoint returned invalid JSON.", ex);
        }

        if (string.IsNullOrWhiteSpace(idToken))
            throw new OidcValidationException(OidcValidationException.CodeMissingIdToken, "OIDC token response did not include an id_token.");

        return ValidateIdToken(idToken, config, clientId, expectedNonce);
    }

    public DirectoryIdentity ValidateIdToken(
        string idToken,
        OpenIdConnectConfiguration config,
        string clientId,
        string expectedNonce)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = AllowedClockSkew,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            LifetimeValidator = (notBefore, expires, _, _) => ValidateLifetime(notBefore, expires)
        };

        ClaimsPrincipal principal;
        try
        {
            principal = _tokenHandler.ValidateToken(idToken, validationParameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            throw new OidcValidationException(OidcValidationException.CodeInvalidToken, $"ID token validation failed: {ex.Message}", ex);
        }

        var nonce = principal.FindFirst("nonce")?.Value;
        if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
            throw new OidcValidationException(OidcValidationException.CodeNonceMismatch, "ID token nonce does not match expected nonce.");

        var subject = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject))
            throw new OidcValidationException(OidcValidationException.CodeSubjectClaimMissing, "ID token is missing the 'sub' claim.");

        var usernameClaimName = string.IsNullOrWhiteSpace(_options.Oidc.UsernameClaim)
            ? OidcOptions.DefaultUsernameClaim
            : _options.Oidc.UsernameClaim.Trim();
        var username = principal.FindFirst(usernameClaimName)?.Value;
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new OidcValidationException(
                OidcValidationException.CodeUsernameClaimMissing,
                $"ID token is missing the configured username claim '{usernameClaimName}'.");
        }

        var mfaSatisfied = HasAmrMfaClaim(principal);
        if (_options.Oidc.RequireMfaClaim && !mfaSatisfied)
        {
            throw new OidcValidationException(
                OidcValidationException.CodeMfaClaimMissing,
                "ID token is missing required 'mfa' value in 'amr' claim.");
        }

        // A "sub" is only unique within its issuer, so the stored permanent id is the validated
        // issuer joined to it: the same "sub" from a different issuer is a different person.
        return new DirectoryIdentity($"{config.Issuer}|{subject}", username.Trim(), mfaSatisfied);
    }

    // Throws when the provider cannot be reached or its metadata is incomplete.
    // The result is recorded by the caller (SsoAdminService), which knows the
    // acting administrator.
    public async Task TestAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await GetDiscoveryConfigurationAsync(ct);
            if (string.IsNullOrWhiteSpace(config.AuthorizationEndpoint)
                || string.IsNullOrWhiteSpace(config.TokenEndpoint)
                || config.SigningKeys.Count == 0)
            {
                throw new InvalidOperationException(
                    "OIDC discovery metadata is incomplete (missing authorization_endpoint, token_endpoint, or signing keys).");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "OIDC connection test failed.");
            throw;
        }
    }

    private Task<OpenIdConnectConfiguration> GetDiscoveryConfigurationAsync(CancellationToken ct)
    {
        var authority = RequireConfigured(_options.Oidc.Authority, "Sso:Oidc:Authority").TrimEnd('/');
        if (_configManager is null || !string.Equals(_configManagerAuthority, authority, StringComparison.Ordinal))
        {
            var metadataAddress = $"{authority}/.well-known/openid-configuration";
            var retriever = new HttpDocumentRetriever(_httpClient)
            {
                RequireHttps = !IsLoopbackHttp(metadataAddress)
            };
            _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                retriever);
            _configManagerAuthority = authority;
        }

        return _configManager.GetConfigurationAsync(ct);
    }

    private bool ValidateLifetime(DateTime? notBefore, DateTime? expires)
    {
        if (!expires.HasValue)
            return false;

        var now = _time.GetUtcNow().UtcDateTime;
        if (notBefore.HasValue && now < notBefore.Value.Subtract(AllowedClockSkew))
            return false;

        if (now > expires.Value.Add(AllowedClockSkew))
            return false;

        return true;
    }

    private static bool HasAmrMfaClaim(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.FindAll("amr"))
        {
            if (string.Equals(claim.Value, "mfa", StringComparison.OrdinalIgnoreCase))
                return true;

            if (claim.Value.StartsWith('['))
            {
                try
                {
                    var items = JsonSerializer.Deserialize<string[]>(claim.Value);
                    if (items is not null && items.Any(item => string.Equals(item, "mfa", StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
                catch (JsonException)
                {
                    // Ignore malformed amr JSON array and continue checking other claims.
                }
            }
        }

        return false;
    }

    private static string RequireConfigured(string? value, string keyName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required OIDC configuration '{keyName}'.");

        return value.Trim();
    }

    private static bool IsLoopbackHttp(string uriString)
    {
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return false;

        if (uri.IsLoopback)
            return true;

        return IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip);
    }
}
