using System.Text.Json;
using MicroLIMS.API.Authorization;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Middleware;

// While maintenance mode is on, every state-changing request from an
// authenticated caller is refused with 503 and the maintenance message.
// Reads (GET/HEAD/OPTIONS) always pass, so users can still look at records.
//
// Allowed through even while on:
//  - sign-in, sign-out, token refresh and change-password (people must be able to
//    get in and out; none of them changes laboratory data);
//  - the admin settings endpoint, so maintenance can be turned off by whoever is
//    entitled to (that endpoint itself requires System.ConfigureSettings);
//  - anything under /api/admin/ from a caller holding System.ConfigureSettings
//    (the administrator doing the maintenance).
// Anonymous requests are not this middleware's business: they either hit an
// anonymous endpoint (password reset, client error reports) or are rejected by
// authorization. The flag is read through the cached settings reader; a change
// made on this instance invalidates that cache immediately.
public sealed class MaintenanceModeMiddleware
{
    public const string HeaderName = "X-Maintenance-Mode";

    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
        { "GET", "HEAD", "OPTIONS" };

    private static readonly string[] AuthenticationPaths =
    {
        "/api/auth/login", "/api/auth/logout", "/api/auth/refresh", "/api/auth/change-password"
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;

    public MaintenanceModeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ISystemSettingsReader settings)
    {
        if (SafeMethods.Contains(context.Request.Method) || IsAlwaysAllowed(context))
        {
            await _next(context);
            return;
        }

        // Instrument agents authenticate with a key inside the authorization step, so
        // they are not yet "authenticated" here; they write GxP data and are covered too.
        var isCaller = context.User.Identity?.IsAuthenticated == true
            || context.Request.Headers.ContainsKey(AgentKeyDefaults.HeaderName);
        if (!isCaller)
        {
            await _next(context);
            return;
        }

        if (!await settings.IsMaintenanceOnAsync(context.RequestAborted))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
            && context.User.HasClaim("permission", PermissionConstants.SystemConfigureSettings))
        {
            await _next(context);
            return;
        }

        var message = await settings.GetStringAsync(SystemSettingKeys.MaintenanceMessage, context.RequestAborted);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers[HeaderName] = "on";
        context.Response.Headers.RetryAfter = "300";
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(ApiResponse<object>.Fail(message), Json), context.RequestAborted);
    }

    private static bool IsAlwaysAllowed(HttpContext context)
    {
        var path = context.Request.Path;
        foreach (var allowed in AuthenticationPaths)
            if (path.Equals(allowed, StringComparison.OrdinalIgnoreCase)) return true;
        return path.StartsWithSegments("/api/admin/settings", StringComparison.OrdinalIgnoreCase);
    }
}
