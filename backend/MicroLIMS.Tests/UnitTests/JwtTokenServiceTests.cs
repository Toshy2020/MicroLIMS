using System.IdentityModel.Tokens.Jwt;
using MicroLIMS.Infrastructure.Authentication;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class JwtTokenServiceTests
{
    // A throwaway signing key for these tests only - deliberately not the
    // old development placeholder, which JwtConfiguration now rejects
    // outside Development (see JwtConfigurationTests).
    internal const string TestSigningKey = "unit-test-signing-key-not-a-secret-0123456789";

    private static JwtTokenService CreateService() =>
        new(TestSigningKey, "MicroLIMS", "MicroLIMS.Client");

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
    public void IssueToken_WithNoPermissionCodes_AddsNoPermissionClaims()
    {
        var service = CreateService();
        var jwt = service.IssueToken("1", "Analyst");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.DoesNotContain(token.Claims, c => c.Type == "permission");
        Assert.Contains(token.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Analyst");
    }
}
