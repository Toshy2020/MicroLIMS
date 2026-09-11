using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.API.Extensions;

// Supplies the Application layer with the request details a security
// event should carry, without giving it a dependency on ASP.NET.
//
// The correlation id is the technical one stamped by
// CorrelationIdMiddleware - the same id Incident/ErrorLog carry, so a
// security event and the error monitoring incident raised during the same
// request can be lined up. It is not the GxP AuditLog Guid.
public sealed class HttpSecurityRequestContext : ISecurityRequestContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpSecurityRequestContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private HttpContext? Context => _accessor.HttpContext;

    public string? CorrelationId
    {
        get
        {
            var context = Context;
            if (context is null) return null;
            var id = CorrelationIdMiddleware.GetCorrelationId(context);
            return string.IsNullOrEmpty(id) ? null : id;
        }
    }

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    public string? RequestPath => Context?.Request.Path.Value;

    // No HttpContext means this is background work, not a request.
    public string Source => Context is null ? "System" : "Api";
}
