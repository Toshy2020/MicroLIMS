using Microsoft.AspNetCore.Authorization;

namespace MicroLIMS.API.Authorization;

// Checks the "permission" claims JwtTokenService adds to the token
// (additive alongside the existing Role claim - see JwtTokenService.
// IssueToken). Stateless: no DB dependency, since the permission set is
// already baked into the token at login/refresh time.
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.PermissionCode))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
