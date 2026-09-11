using MicroLIMS.API.Middleware;

namespace MicroLIMS.API.Extensions;

public static class ApplicationBuilderExtensions
{
    // Runs before authentication: safe to log/catch exceptions this early.
    public static IApplicationBuilder UseMicroLimsEarlyPipeline(this IApplicationBuilder app)
    {
        // First in the pipeline - ExceptionMiddleware needs the
        // correlation id to already exist when it catches.
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        return app;
    }

    // Must run AFTER UseAuthentication/UseAuthorization - it reads
    // HttpContext.User, which is only populated once auth has run.
    public static IApplicationBuilder UseMicroLimsAuditPipeline(this IApplicationBuilder app)
    {
        app.UseMiddleware<AuditMiddleware>();
        return app;
    }
}
