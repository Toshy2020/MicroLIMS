namespace MicroLIMS.API.Middleware;

// Response headers for a JSON API that is never meant to be rendered as a
// page. Previously only a few download endpoints set nosniff.
//
// - X-Content-Type-Options: nosniff - a response is only ever treated as
//   the content type it declares (attachments included).
// - X-Frame-Options / CSP frame-ancestors - no response may be framed.
// - Content-Security-Policy: default-src 'none' - if a response is ever
//   opened directly in a browser, nothing in it may load or run. File
//   downloads are fetched by the frontend as blobs, so this does not touch
//   how documents are viewed.
// - Referrer-Policy: no-referrer - API URLs never leak onward.
//
// Swagger UI (Development only) serves a real page with scripts, so its
// paths are left without the CSP.
public class SecurityHeadersMiddleware
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var isSwagger = context.Request.Path.StartsWithSegments("/swagger");

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            if (!isSwagger)
                headers["Content-Security-Policy"] = ContentSecurityPolicy;
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
