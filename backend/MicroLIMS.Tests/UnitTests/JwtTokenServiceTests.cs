using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MicroLIMS.Infrastructure.Authentication;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class JwtTokenServiceTests
{
    // A throwaway signing key for these tests only - deliberately not the
    // old development placeholder, which JwtConfiguration now rejects
    // outside Development (see JwtConfigurationTests).
    internal const string TestSigningKey = "unit-test-signing-key-not-a-secret-0123456789";

    private static JwtTokenService CreateService(TimeSpan? lifetime = null) =>
        new(TestSigningKey, "MicroLIMS", "MicroLIMS.Client", lifetime ?? TimeSpan.FromMinutes(15));

    // The lifetime comes from configuration (Jwt:AccessTokenMinutes) rather
    // than the former fixed 8 hours.
    [Fact]
    public void IssueToken_ExpiresAfterTheConfiguredLifetime()
    {
        var before = DateTime.UtcNow;
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService(TimeSpan.FromMinutes(15)).IssueToken("1", "Analyst"));

        Assert.InRange(token.ValidTo, before.AddMinutes(15).AddSeconds(-1), DateTime.UtcNow.AddMinutes(15).AddSeconds(1));
    }

    // AccessTokenRevalidator compares iat with the account's last password
    // change, so every token must carry it.
    [Fact]
    public void IssueToken_CarriesTheIssuedAtClaim()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService().IssueToken("1", "Analyst"));

        var iat = long.Parse(token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Iat).Value);
        Assert.InRange(iat, before, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    [Fact]
    public void IssueToken_AddsOnePermissionClaimPerCode()
    {
        var service = CreateService();
        var codes = new[] { "Samples.Approve", "MasterData.Manage", "Audit.View" };

        var jwt = service.IssueToken("1", "SectionHead", codes);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        var permissionClaims = token.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.Equal(codes.OrderBy(c => c), permissionClaims.OrderBy(c => c));
    }

    [Fact]
    public void IssueToken_RoleClaimIsUnchanged_AlongsidePermissionClaims()
    {
        var service = CreateService();
        var jwt = service.IssueToken("1", "Analyst", new[] { "TestWorkflow.Execute" });
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.Contains(token.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Analyst");
        Assert.Contains(token.Claims, c => c.Type == "permission" && c.Value == "TestWorkflow.Execute");
    }

    [Fact]
    public void IssueToken_CarriesTheRoleIdAndPermissionVersion()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            CreateService().IssueToken("1", "SectionHead", new[] { "Samples.Approve" }, roleId: 6, permissionVersion: 3));

        Assert.Equal("6", jwt.Claims.Single(c => c.Type == "rid").Value);
        Assert.Equal("3", jwt.Claims.Single(c => c.Type == "pv").Value);
        // Existing claims are unchanged alongside them.
        Assert.Equal("SectionHead", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("Samples.Approve", jwt.Claims.Single(c => c.Type == "permission").Value);
    }

    [Fact]
    public void IssueToken_WithoutRoleIdAndPermissionVersion_AddsNeitherClaim()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(CreateService().IssueToken("1", "Analyst"));

        Assert.DoesNotContain(jwt.Claims, c => c.Type is "rid" or "pv");
    }

    [Fact]
    public void IssueToken_WithNoPermissionCodes_AddsNoPermissionClaims()
    {
        var service = CreateService();
        var jwt = service.IssueToken("1", "Analyst");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.DoesNotContain(token.Claims, c => c.Type == "permission");
        Assert.Contains(token.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Analyst");
    }
}
