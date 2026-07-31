using MicroLIMS.Persistence.DbContext;
using System.Security.Claims;

namespace MicroLIMS.API.Middleware;

// Stamps the current authenticated user onto the DbContext so
// MicroLimsDbContext.SaveChanges knows who to attribute each audit entry to.
public class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, MicroLimsDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idClaim, out var userId))
                db.CurrentUserId = userId;
        }

        await _next(context);
    }
}
