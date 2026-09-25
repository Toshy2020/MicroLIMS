using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using MicroLIMS.Shared.Responses;
using System.Threading.RateLimiting;

namespace MicroLIMS.API.Extensions;

public static class RateLimitingPolicies
{
    public const string ClientErrors = "client-errors";

    // Anonymous endpoints that check a secret: login, password-reset
    // request and confirm, admin password-recovery confirm.
    public const string Authentication = "authentication";

    // Anonymous, but called routinely: every signed-in tab refreshes its
    // 15-minute access token, and a lab often shares one public IP.
    public const string TokenRefresh = "token-refresh";

    // Authenticated password change - a stolen session must not become an
    // unlimited current-password guessing oracle.
    public const string PasswordChange = "password-change";
}

public static class RateLimitingExtensions
{
    public const int DefaultClientErrorsPerWindow = 20;
    public const int DefaultClientErrorWindowSeconds = 60;

    // Per client IP. Generous enough for a lab signing in at shift change
    // from one shared address; per-account lockout (5 failures) still
    // protects each individual account.
    public const int DefaultAuthenticationPerWindow = 20;
    public const int DefaultAuthenticationWindowSeconds = 60;

    public const int DefaultTokenRefreshPerWindow = 60;
    public const int DefaultTokenRefreshWindowSeconds = 60;

    // Per signed-in user.
    public const int DefaultPasswordChangePerWindow = 5;
    public const int DefaultPasswordChangeWindowSeconds = 900;

    public static IServiceCollection AddMicroLimsRateLimiting(
        this IServiceCollection services, IConfiguration config)
    {
        // POST /api/system/client-errors is anonymous by necessity, so it is
        // the one endpoint where an unauthenticated caller can cause a
        // database write. A crash loop in one browser tab can also fire the
        // window handlers hundreds of times a second without any malice, so
        // this protects against the honest case as much as the hostile one.
        var clientErrors = Window(config, "ErrorMonitoring:ClientErrors", "PerWindow",
            DefaultClientErrorsPerWindow, DefaultClientErrorWindowSeconds);

        var authentication = Window(config, "RateLimiting:Authentication", "PermitLimit",
            DefaultAuthenticationPerWindow, DefaultAuthenticationWindowSeconds);

        var tokenRefresh = Window(config, "RateLimiting:TokenRefresh", "PermitLimit",
            DefaultTokenRefreshPerWindow, DefaultTokenRefreshWindowSeconds);

        var passwordChange = Window(config, "RateLimiting:PasswordChange", "PermitLimit",
            DefaultPasswordChangePerWindow, DefaultPasswordChangeWindowSeconds);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitingPolicies.ClientErrors, httpContext =>
                FixedWindow(ClientIp(httpContext), clientErrors));

            options.AddPolicy(RateLimitingPolicies.Authentication, httpContext =>
                FixedWindow(ClientIp(httpContext), authentication));

            options.AddPolicy(RateLimitingPolicies.TokenRefresh, httpContext =>
                FixedWindow(ClientIp(httpContext), tokenRefresh));

            // Runs after authentication (see Program.cs), so the user is
            // known; fall back to the IP only if somehow it is not.
            options.AddPolicy(RateLimitingPolicies.PasswordChange, httpContext =>
                FixedWindow(
                    httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { } userId
                        ? "user:" + userId
                        : ClientIp(httpContext),
                    passwordChange));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail("Too many requests. Please wait a moment and try again."), cancellationToken);
            };
        });

        return services;
    }

    // Behind Render's proxy the socket address is the load balancer, so
    // partition on the forwarded client address that UseForwardedHeaders
    // has already resolved (see TrustedProxyConfiguration for which
    // proxies are trusted). Unknown addresses share one bucket rather than
    // getting a free pass each.
    private static string ClientIp(HttpContext httpContext) =>
        "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private static RateLimitPartition<string> FixedWindow(string partitionKey, (int PermitLimit, TimeSpan Window) limits) =>
        RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.PermitLimit,
            Window = limits.Window,
            // No queue: a rejected request is refused, not delayed. Holding
            // requests open would turn a flood into a thread-pool problem.
            QueueLimit = 0
        });

    private static (int PermitLimit, TimeSpan Window) Window(
        IConfiguration config, string section, string permitKey, int defaultPermits, int defaultWindowSeconds) =>
        (Positive(config.GetValue<int?>($"{section}:{permitKey}"), defaultPermits),
         TimeSpan.FromSeconds(Positive(config.GetValue<int?>($"{section}:WindowSeconds"), defaultWindowSeconds)));

    private static int Positive(int? configured, int fallback) =>
        configured is > 0 ? configured.Value : fallback;
}
