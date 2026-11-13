using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Infrastructure.Sso;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Sso;

public class OidcTokenValidationTests
{
    private const string Authority = "https://idp.example.test";
    private const string ClientId = "microlims-web";
    private const string ExpectedNonce = "nonce-12345";
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class InMemoryOidcHandler : HttpMessageHandler
    {
        private readonly string _discoveryJson;
        private readonly string _jwksJson;
        private readonly Func<string> _idTokenFactory;

        public InMemoryOidcHandler(RsaSecurityKey publicKey, Func<string> idTokenFactory)
        {
            _idTokenFactory = idTokenFactory;
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;
            _jwksJson = JsonSerializer.Serialize(new { keys = new[] { jwk } });
            _discoveryJson = JsonSerializer.Serialize(new
            {
                issuer = Authority,
                authorization_endpoint = $"{Authority}/authorize",
                token_endpoint = $"{Authority}/token",
                jwks_uri = $"{Authority}/jwks"
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;
            if (uri.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal))
                return Task.FromResult(JsonResponse(_discoveryJson));

            if (uri.EndsWith("/jwks", StringComparison.Ordinal))
                return Task.FromResult(JsonResponse(_jwksJson));

            if (uri.EndsWith("/token", StringComparison.Ordinal))
            {
                var tokenJson = JsonSerializer.Serialize(new { id_token = _idTokenFactory(), access_token = "unused" });
                return Task.FromResult(JsonResponse(tokenJson));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }

    private static (RsaSecurityKey SigningKey, RsaSecurityKey PublicKey) CreateRsaKeys(string keyId = "key-1")
    {
        var rsa = RSA.Create(2048);
        var fullKey = new RsaSecurityKey(rsa) { KeyId = keyId };
        var pubParams = rsa.ExportParameters(false);
        var publicKey = new RsaSecurityKey(pubParams) { KeyId = keyId };
        return (fullKey, publicKey);
    }

    private static string SignJwt(
        RsaSecurityKey signingKey,
        string issuer = Authority,
        string audience = ClientId,
        DateTime? notBefore = null,
        DateTime? expires = null,
        string? nonce = ExpectedNonce,
        string? subject = "sub-42",
        string? preferredUsername = "alice",
        string[]? amr = null)
    {
        var claims = new List<Claim>();
        if (nonce is not null) claims.Add(new Claim("nonce", nonce));
        if (subject is not null) claims.Add(new Claim("sub", subject));
        if (preferredUsername is not null) claims.Add(new Claim("preferred_username", preferredUsername));
        if (amr is not null)
        {
            foreach (var item in amr)
                claims.Add(new Claim("amr", item));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore ?? BaseUtc.UtcDateTime.AddMinutes(-1),
            Expires = expires ?? BaseUtc.UtcDateTime.AddMinutes(5),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.CreateEncodedJwt(descriptor);
    }

    private static OidcProviderClient CreateClient(
        RsaSecurityKey publicKey,
        Func<string> idTokenFactory,
        bool requireMfaClaim = false,
        string usernameClaim = "preferred_username")
    {
        var options = new SsoOptions
        {
            Oidc = new OidcOptions
            {
                Authority = Authority,
                ClientId = ClientId,
                ClientSecret = "secret",
                UsernameClaim = usernameClaim,
                RequireMfaClaim = requireMfaClaim
            }
        };

        var httpClient = new HttpClient(new InMemoryOidcHandler(publicKey, idTokenFactory));
        return new OidcProviderClient(options, httpClient, timeProvider: new FixedTimeProvider(BaseUtc));
    }

    [Fact]
    public async Task Valid_ReturnsIdentity()
    {
        var (signingKey, publicKey) = CreateRsaKeys();
        var client = CreateClient(publicKey, () => SignJwt(signingKey, amr: new[] { "pwd", "mfa" }));

        var identity = await client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback");

        Assert.Equal("sub-42", identity.SubjectId);
        Assert.Equal("alice", identity.Username);
        Assert.True(identity.MfaSatisfied);
    }

    [Fact]
    public async Task BadSignature()
    {
        var (_, publicKey) = CreateRsaKeys("key-1");
        var (otherSigningKey, _) = CreateRsaKeys("key-1");
        var client = CreateClient(publicKey, () => SignJwt(otherSigningKey));

        var ex = await Assert.ThrowsAsync<OidcValidationException>(
            () => client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback"));

        Assert.Equal(OidcValidationException.CodeInvalidToken, ex.ReasonCode);
    }

    [Fact]
    public async Task WrongIssuer()
    {
        var (signingKey, publicKey) = CreateRsaKeys();
        var client = CreateClient(publicKey, () => SignJwt(signingKey, issuer: "https://evil-idp.test"));

        var ex = await Assert.ThrowsAsync<OidcValidationException>(
            () => client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback"));

        Assert.Equal(OidcValidationException.CodeInvalidToken, ex.ReasonCode);
    }

    [Fact]
    public async Task WrongAudience()
    {
        var (signingKey, publicKey) = CreateRsaKeys();
        var client = CreateClient(publicKey, () => SignJwt(signingKey, audience: "other-client"));

        var ex = await Assert.ThrowsAsync<OidcValidationException>(
            () => client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback"));

        Assert.Equal(OidcValidationException.CodeInvalidToken, ex.ReasonCode);
    }

    [Fact]
    public async Task Expired()
    {
        var (signingKey, publicKey) = CreateRsaKeys();
        var client = CreateClient(
            publicKey,
            () => SignJwt(
                signingKey,
                notBefore: BaseUtc.UtcDateTime.AddMinutes(-20),
                expires: BaseUtc.UtcDateTime.AddMinutes(-10)));

        var ex = await Assert.ThrowsAsync<OidcValidationException>(
            () => client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback"));

        Assert.Equal(OidcValidationException.CodeInvalidToken, ex.ReasonCode);
    }

    [Fact]
    public async Task NonceMismatch()
    {
        var (signingKey, publicKey) = CreateRsaKeys();
        var client = CreateClient(publicKey, () => SignJwt(signingKey, nonce: "different-nonce"));

        var ex = await Assert.ThrowsAsync<OidcValidationException>(
            () => client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback"));

        Assert.Equal(OidcValidationException.CodeNonceMismatch, ex.ReasonCode);
    }

    [Fact]
    public async Task ThreeMinuteSkew_Rejected_NinetySeconds_Accepted()
    {
        var (signingKey, publicKey) = CreateRsaKeys();

        var threeMinExpiredClient = CreateClient(
            publicKey,
            () => SignJwt(
                signingKey,
                notBefore: BaseUtc.UtcDateTime.AddMinutes(-10),
                expires: BaseUtc.UtcDateTime.AddMinutes(-3)));

        var ex = await Assert.ThrowsAsync<OidcValidationException>(
            () => threeMinExpiredClient.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback"));
        Assert.Equal(OidcValidationException.CodeInvalidToken, ex.ReasonCode);

        var ninetySecExpiredClient = CreateClient(
            publicKey,
            () => SignJwt(
                signingKey,
                notBefore: BaseUtc.UtcDateTime.AddMinutes(-10),
                expires: BaseUtc.UtcDateTime.AddSeconds(-90)));

        var identity = await ninetySecExpiredClient.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback");
        Assert.Equal("sub-42", identity.SubjectId);
    }

    [Fact]
    public async Task RequireMfaClaim_WithoutAmrMfa_Throws()
    {
        var (signingKey, publicKey) = CreateRsaKeys();
        var client = CreateClient(publicKey, () => SignJwt(signingKey, amr: new[] { "pwd" }), requireMfaClaim: true);

        var ex = await Assert.ThrowsAsync<OidcValidationException>(
            () => client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback"));

        Assert.Equal(OidcValidationException.CodeMfaClaimMissing, ex.ReasonCode);
    }

    [Fact]
    public async Task RequireMfaClaim_WithAmrMfa_ReturnsIdentity()
    {
        var (signingKey, publicKey) = CreateRsaKeys();
        var client = CreateClient(publicKey, () => SignJwt(signingKey, amr: new[] { "pwd", "mfa" }), requireMfaClaim: true);

        var identity = await client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback");
        Assert.True(identity.MfaSatisfied);
    }

    [Fact]
    public async Task UsernameClaimMissing_Throws()
    {
        var (signingKey, publicKey) = CreateRsaKeys();
        var client = CreateClient(publicKey, () => SignJwt(signingKey, preferredUsername: null));

        var ex = await Assert.ThrowsAsync<OidcValidationException>(
            () => client.RedeemAsync("auth-code", "verifier", ExpectedNonce, "https://app.test/callback"));

        Assert.Equal(OidcValidationException.CodeUsernameClaimMissing, ex.ReasonCode);
    }
}
