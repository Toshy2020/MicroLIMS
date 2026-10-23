using System.Threading.RateLimiting;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Extensions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Middleware;

// Limits requests that present an X-Agent-Key per client address BEFORE the
// key is checked (this runs between UseAuthentication and UseAuthorization;
// the AgentKey handler itself runs inside UseAuthorization). Without it a
// flood of bad keys would each cost a database lookup and never reach the
// post-authentication rate limiter. The per-agent limiter stays where it is:
// it needs the authenticated agent.
public sealed class AgentKeyPreAuthThrottleMiddleware
{
    public const int DefaultPermitLimit = 30;
    public const int DefaultWindowSeconds = 60;

    private readonly RequestDelegate _next;
    private readonly PartitionedRateLimiter<HttpContext> _limiter;

    public AgentKeyPreAuthThrottleMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        var limits = RateLimitingExtensions.Window(config, "RateLimiting:AgentPreAuth", "PermitLimit",
            DefaultPermitLimit, DefaultWindowSeconds);
        _limiter = PartitionedRateLimiter.Create<HttpContext, string>(
            ctx => RateLimitingExtensions.FixedWindow(RateLimitingExtensions.ClientIp(ctx), limits));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey(AgentKeyDefaults.HeaderName))
        {
            await _next(context);
            return;
        }

        using var lease = await _limiter.AcquireAsync(context, 1, context.RequestAborted);
        if (lease.IsAcquired)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail("Too many requests. Please wait a moment and try again."), context.RequestAborted);
    }
}
