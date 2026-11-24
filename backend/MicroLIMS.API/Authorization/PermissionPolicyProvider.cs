using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.API.Authorization;

// Dynamic policy resolution: [Authorize(Policy = "Samples.Approve")] just
// works for any of the 18 catalog codes with no matching AddPolicy() call
// in Program.cs - avoids the "forgot to register it somewhere" failure
// mode the role-string system has today. Any policy name that isn't a
// known permission code (the framework's own default/fallback policy,
// or a future named policy that isn't permission-based) delegates to the
// standard DefaultAuthorizationPolicyProvider.
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public const string OosContributeOrManage = "Oos.ContributeOrManage";

    public static IReadOnlyList<string> PermissionsFor(string policyName) =>
        policyName == OosContributeOrManage
            ? [PermissionConstants.OosContribute, PermissionConstants.OosManage]
            : [policyName];

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName == OosContributeOrManage)
        {
            var permissions = PermissionsFor(policyName);
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(context => permissions.Any(code => context.User.HasClaim("permission", code)))
                .Build());
        }

        if (PermissionConstants.All.Contains(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
