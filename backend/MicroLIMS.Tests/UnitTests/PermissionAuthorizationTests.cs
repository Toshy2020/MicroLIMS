using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MicroLIMS.API.Authorization;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class PermissionAuthorizationHandlerTests
{
    private static ClaimsPrincipal PrincipalWithPermissions(params string[] codes)
    {
        var claims = codes.Select(c => new Claim("permission", c));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Allows_WhenUserHasTheRequiredPermissionClaim()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(PermissionConstants.SamplesApprove);
        var user = PrincipalWithPermissions(PermissionConstants.SamplesApprove, PermissionConstants.AuditView);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Denies_WhenUserLacksTheRequiredPermissionClaim()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(PermissionConstants.SamplesApprove);
        var user = PrincipalWithPermissions(PermissionConstants.AuditView); // has a different permission, not this one
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Denies_WhenUserHasNoPermissionClaimsAtAll()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(PermissionConstants.SamplesApprove);
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

public class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider CreateProvider() =>
        new(Options.Create(new AuthorizationOptions()));

    [Theory]
    [InlineData("Samples.Approve")]
    [InlineData("MasterData.Manage")]
    [InlineData("Roles.Manage")]
    public async Task ResolvesAnyKnownPermissionCode_WithNoExplicitAddPolicyCall(string code)
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync(code);

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is PermissionRequirement pr && pr.PermissionCode == code);
    }

    [Fact]
    public async Task UnknownPolicyName_FallsBackToDefaultProvider_ReturnsNull()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("NotARealPermissionCode");

        Assert.Null(policy); // DefaultAuthorizationPolicyProvider returns null for a name with no AddPolicy() registration
    }

    [Fact]
    public async Task AllEighteenCatalogCodes_ResolveToAPolicy()
    {
        var provider = CreateProvider();

        foreach (var code in PermissionConstants.All)
        {
            var policy = await provider.GetPolicyAsync(code);
            Assert.NotNull(policy);
        }
    }
}
