using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MicroLIMS.API.HealthChecks;

namespace MicroLIMS.API.Extensions;

// Liveness and readiness, kept apart on purpose.
//
//   /health        - is this process alive? Runs no dependency checks, so
//                    a database outage can never make the container look
//                    dead and get it restarted while it is fine.
//   /health/ready  - can this instance actually serve MicroLIMS requests?
//                    Runs the checks tagged "ready".
//
// Built on the ASP.NET Core health-check infrastructure in the shared
// framework - no extra package, and no second mechanism alongside it.
public static class HealthCheckExtensions
{
    public const string ReadyTag = "ready";

    private const string LivenessStatus = "Healthy";

    public static IServiceCollection AddMicroLimsHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessHealthCheck>(
                name: "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { ReadyTag });

        return services;
    }

    public static void MapMicroLimsHealthChecks(this WebApplication app)
    {
        // ---- Liveness ----
        // predicate => false selects no checks, so this is a pure "the
        // pipeline is running" answer and stays cheap. The response shape
        // is unchanged from the original endpoint because DEPLOYMENT.md
        // documents callers checking for {"status":"Healthy",...}.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = static (context, _) =>
            {
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    status = LivenessStatus,
                    timestamp = DateTime.UtcNow
                }));
            }
        }).AllowAnonymous();

        // ---- Readiness ----
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            // The framework defaults already map Unhealthy to 503; stated
            // explicitly so the contract a load balancer depends on is
            // visible here rather than implied. Degraded is deliberately
            // 503 too: this endpoint answers yes or no, and "partly ready"
            // is not something a deployment gate can act on.
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = (int)HttpStatusCode.OK,
                [HealthStatus.Degraded] = (int)HttpStatusCode.ServiceUnavailable,
                [HealthStatus.Unhealthy] = (int)HttpStatusCode.ServiceUnavailable
            },
            ResponseWriter = WriteReadinessResponse
        }).AllowAnonymous();
    }

    // Writes only what an operator needs to act: the overall verdict, and
    // which named dependency is at fault.
    //
    // What it deliberately never writes: the exception attached to a
    // failed check, its stack trace, connection strings, host names, SQL,
    // configuration values, or the check's Data dictionary. Descriptions
    // are the fixed strings authored in DatabaseReadinessHealthCheck, not
    // anything produced by the failure itself.
    private static Task WriteReadinessResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
