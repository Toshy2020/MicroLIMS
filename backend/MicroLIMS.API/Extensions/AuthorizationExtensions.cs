using Microsoft.AspNetCore.Authorization;
using MicroLIMS.API.Authorization;

namespace MicroLIMS.API.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddMicroLimsAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Deny by default: an endpoint that carries no [Authorize] or
            // [AllowAnonymous] still requires a signed-in user. Every endpoint
            // is annotated today; this keeps a future one that is not from
            // being public by accident. Anonymous endpoints (login, health,
            // client errors) opt out explicitly with [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            // Edge agents (X-Agent-Key). Names the scheme itself so a human
            // JWT is never evaluated here and an agent key never reaches a
            // JWT endpoint.
            options.AddPolicy(AgentKeyDefaults.Policy, policy => policy
                .AddAuthenticationSchemes(AgentKeyDefaults.Scheme)
                .RequireAuthenticatedUser()
                .RequireClaim(AgentKeyDefaults.AgentIdClaim));
        });

        // Permission-based authorization, running alongside the existing role-
        // string [Authorize(Roles=...)] system - not replacing it in this phase.
        // [Authorize(Policy = "<permission code>")] resolves dynamically via
        // PermissionPolicyProvider, no per-code AddPolicy() call needed.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
