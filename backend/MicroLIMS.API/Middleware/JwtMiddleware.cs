namespace MicroLIMS.API.Middleware;

// Placeholder for any custom JWT pre-processing beyond what
// UseAuthentication() already provides (e.g. token blacklist checks
// for a "logout" endpoint that revokes tokens server-side).
public class JwtMiddleware
{
    private readonly RequestDelegate _next;

    public JwtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // TODO: add token revocation/blacklist check here if required.
        await _next(context);
    }
}
