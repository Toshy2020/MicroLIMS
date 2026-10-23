using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MicroLIMS.API.Authorization;

namespace MicroLIMS.API.Extensions;

// The "AgentKey" scheme for edge agents. Not a default scheme: JWT stays the
// default, and only endpoints that name AgentKey (the InstrumentAgent policy)
// accept the X-Agent-Key header.
public static class AgentAuthenticationExtensions
{
    public static IServiceCollection AddMicroLimsAgentAuthentication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AgentKeyAuthenticationHandler>(AgentKeyDefaults.Scheme, null);
        return services;
    }
}
