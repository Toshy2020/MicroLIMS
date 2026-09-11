using System.IdentityModel.Tokens.Jwt;
using System.Text;
using MicroLIMS.API.Extensions;
using MicroLIMS.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The security invariant these cover: the application cannot start with a
// missing, blank, too-short, or publicly-known JWT signing key, and no
// fallback path remains that would let it. Startup failure here is the
// intended behaviour, not a bug.
public class JwtConfigurationTests
{
    private const string ValidKey = "a-valid-configured-signing-key-of-sufficient-length";

    private static IConfiguration Config(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    private static IHostEnvironment Env(string environmentName) =>
        new StubEnvironment { EnvironmentName = environmentName };

    // ---- 1. Missing key outside Development -> startup failure ----

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    public void Resolve_MissingKey_OutsideDevelopment_Throws(string environmentName)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtConfiguration.Resolve(Config(), Env(environmentName)));

        Assert.Contains("not configured", ex.Message);
        Assert.Contains("will not start", ex.Message);
    }

    // ---- 2 and 3. Empty and whitespace keys -> failure ----

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t  ")]
    public void Resolve_EmptyOrWhitespaceKey_Throws(string key)
    {
        Assert.Throws<InvalidOperationException>(
            () => JwtConfiguration.Resolve(Config(("Jwt:Key", key)), Env(Environments.Production)));
    }

    // ---- 4. The known development placeholder -> failure outside Development ----

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Resolve_KnownDevelopmentPlaceholder_OutsideDevelopment_Throws(string environmentName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => JwtConfiguration.Resolve(
            Config(("Jwt:Key", JwtConfiguration.RejectedDevelopmentKey)), Env(environmentName)));

        Assert.Contains("development placeholder", ex.Message);
        Assert.Contains(environmentName, ex.Message);
    }

    [Fact]
    public void Resolve_KnownDevelopmentPlaceholder_IsToleratedInDevelopmentOnly()
    {
        // Deliberate carve-out: the placeholder is no longer shipped in
        // tracked configuration, so only a developer's own untracked file
        // can still supply it, and only Development accepts it.
        var settings = JwtConfiguration.Resolve(
            Config(("Jwt:Key", JwtConfiguration.RejectedDevelopmentKey)), Env(Environments.Development));

        Assert.Equal(JwtConfiguration.RejectedDevelopmentKey, settings.Key);
    }

    // ---- 5. A valid configured key succeeds ----

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Resolve_ValidKey_ReturnsSettings(string environmentName)
    {
        var settings = JwtConfiguration.Resolve(
            Config(("Jwt:Key", ValidKey), ("Jwt:Issuer", "MicroLIMS"), ("Jwt:Audience", "MicroLIMS.Client")),
            Env(environmentName));

        Assert.Equal(ValidKey, settings.Key);
        Assert.Equal("MicroLIMS", settings.Issuer);
        Assert.Equal("MicroLIMS.Client", settings.Audience);
    }

    [Fact]
    public void Resolve_KeyShorterThanMinimum_Throws()
    {
        var tooShort = new string('k', JwtConfiguration.MinimumKeyLength - 1);

        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtConfiguration.Resolve(Config(("Jwt:Key", tooShort)), Env(Environments.Production)));

        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void Resolve_KeyAtExactlyMinimumLength_Succeeds()
    {
        var exact = new string('k', JwtConfiguration.MinimumKeyLength);

        var settings = JwtConfiguration.Resolve(Config(("Jwt:Key", exact)), Env(Environments.Production));

        Assert.Equal(exact, settings.Key);
    }

    // ---- 6. Signing and validation still agree on the configured key ----

    [Fact]
    public void ResolvedSettings_SignAndValidateRoundTrip_Succeeds()
    {
        var settings = JwtConfiguration.Resolve(
            Config(("Jwt:Key", ValidKey), ("Jwt:Issuer", "MicroLIMS"), ("Jwt:Audience", "MicroLIMS.Client")),
            Env(Environments.Production));

        // Signing side, exactly as ServiceCollectionExtensions builds it.
        var issued = new JwtTokenService(settings.Key, settings.Issuer, settings.Audience)
            .IssueToken("42", "SectionHead", new[] { "Samples.Approve" });

        // Validation side, exactly as Program.cs configures AddJwtBearer.
        var principal = new JwtSecurityTokenHandler().ValidateToken(issued, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key))
        }, out _);

        Assert.Contains(principal.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "SectionHead");
        Assert.Contains(principal.Claims, c => c.Type == "permission" && c.Value == "Samples.Approve");
    }

    [Fact]
    public void TokenSignedWithTheRetiredPlaceholder_FailsValidation()
    {
        var settings = JwtConfiguration.Resolve(Config(("Jwt:Key", ValidKey)), Env(Environments.Production));

        // A token forged with the retired placeholder must not validate
        // against the configured key - this is the attack the fallback
        // made possible.
        var forged = new JwtTokenService(
            JwtConfiguration.RejectedDevelopmentKey, settings.Issuer, settings.Audience)
            .IssueToken("1", "SystemAdministrator");

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(forged, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = settings.Issuer,
                ValidAudience = settings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key))
            }, out _));
    }

    // ---- 7. No alternate fallback path remains ----

    [Fact]
    public void Resolve_MissingKey_AlsoThrowsInDevelopment()
    {
        // The fallback used to apply in every environment. Development
        // must fail too, or the fallback has simply moved.
        Assert.Throws<InvalidOperationException>(
            () => JwtConfiguration.Resolve(Config(), Env(Environments.Development)));
    }

    [Fact]
    public void Resolve_DoubleUnderscoreEnvironmentVariableSpelling_IsHonoured()
    {
        // DEPLOYMENT.md documents Jwt__Key as the Render variable name.
        var settings = JwtConfiguration.Resolve(
            Config(("Jwt__Key", ValidKey)), Env(Environments.Production));

        Assert.Equal(ValidKey, settings.Key);
    }

    [Fact]
    public void Resolve_NeverReportsTheConfiguredValue()
    {
        var configured = "a-configured-but-far-too-short";

        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtConfiguration.Resolve(Config(("Jwt:Key", configured)), Env(Environments.Production)));

        Assert.DoesNotContain(configured, ex.Message);
    }

    [Fact]
    public void Resolve_IssuerAndAudienceDefaults_MatchTheValidationDefaults()
    {
        // Signing and validation now share one instance, so the defaults
        // cannot drift apart the way two separate reads could.
        var settings = JwtConfiguration.Resolve(Config(("Jwt:Key", ValidKey)), Env(Environments.Production));

        Assert.Equal("MicroLIMS", settings.Issuer);
        Assert.Equal("MicroLIMS.Client", settings.Audience);
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "MicroLIMS.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
