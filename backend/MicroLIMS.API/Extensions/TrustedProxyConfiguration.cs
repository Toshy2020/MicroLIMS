using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace MicroLIMS.API.Extensions;

// Which X-Forwarded-For / X-Forwarded-Proto values the API believes. The
// resolved client address is recorded in login history and on electronic
// signatures, and partitions the rate limiters - so it matters who may set it.
//
// ForwardLimit = 1 means only the LAST X-Forwarded-For entry is used: the
// one appended by the proxy the request actually came through. A value a
// client prepends itself is ignored, so behind a proxy that appends (as
// Render's does) the recorded address is the real client's.
//
// What that cannot cover is a request that does not come through the proxy
// at all: with no trusted-proxy list, a direct caller's own X-Forwarded-For
// is believed. Setting ForwardedHeaders:KnownNetworks (CIDRs) and/or
// ForwardedHeaders:KnownProxies (addresses) to the proxy's egress range
// closes that - forwarded headers from anywhere else are then ignored.
public static class TrustedProxyConfiguration
{
    public static IServiceCollection AddMicroLimsForwardedHeaders(this IServiceCollection services, IConfiguration config)
    {
        var knownNetworks = Split(config["ForwardedHeaders:KnownNetworks"]).Select(System.Net.IPNetwork.Parse).ToList();
        var knownProxies = Split(config["ForwardedHeaders:KnownProxies"]).Select(IPAddress.Parse).ToList();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            // Replace the loopback-only defaults either way: with nothing
            // configured every source is trusted (the proxy's address is not
            // fixed); with a list, only the listed proxies are.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in knownNetworks) options.KnownIPNetworks.Add(network);
            foreach (var proxy in knownProxies) options.KnownProxies.Add(proxy);
        });

        return services;
    }

    public static bool HasTrustedProxyList(IConfiguration config) =>
        Split(config["ForwardedHeaders:KnownNetworks"]).Any() || Split(config["ForwardedHeaders:KnownProxies"]).Any();

    private static IEnumerable<string> Split(string? value) =>
        (value ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
