using Microsoft.AspNetCore.RateLimiting;
using MicroLIMS.Shared.Responses;
using System.Threading.RateLimiting;

namespace MicroLIMS.API.Extensions;

public static class RateLimitingPolicies
{
    public const string ClientErrors = "client-errors";
}

public static class RateLimitingExtensions
{
    public const int DefaultClientErrorsPerWindow = 20;
    public const int DefaultClientErrorWindowSeconds = 60;

    // POST /api/system/client-errors is anonymous by necessity, so it is
    // the one endpoint where an unauthenticated caller can cause a
    // database write. A crash loop in one browser tab can also fire the
    // window handlers hundreds of times a second without any malice, so
    // this protects against the honest case as much as the hostile one.
    public static IServiceCollection AddMicroLimsRateLimiting(
        this IServiceCollection services, IConfiguration config)
    {
        var permitLimit = Positive(
            config.GetValue<int?>("ErrorMonitoring:ClientErrors:PerWindow"), DefaultClientErrorsPerWindow);

        var windowSeconds = Positive(
            config.GetValue<int?>("ErrorMonitoring:ClientErrors:WindowSeconds"), DefaultClientErrorWindowSeconds);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitingPolicies.ClientErrors, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // Behind Render's proxy the socket address is the load
                    // balancer, so partition on the forwarded client
                    // address that UseForwardedHeaders has already
                    // resolved. Unknown addresses share one bucket rather
                    // than getting a free pass each.
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        // No queue: a rejected error report is dropped, not
                        // delayed. Holding requests open would turn a crash
                        // loop into a thread-pool problem.
                        QueueLimit = 0
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail("Too many requests."), cancellationToken);
            };
        });

        return services;
    }

    private static int Positive(int? configured, int fallback) =>
        configured is > 0 ? configured.Value : fallback;
}
