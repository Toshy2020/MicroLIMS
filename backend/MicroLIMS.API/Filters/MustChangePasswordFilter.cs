using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using System.Security.Claims;

namespace MicroLIMS.API.Filters;

// Enforces MustChangePassword on the server.
//
// The flag was previously returned to the browser and honoured only by the
// frontend, so any client that ignored it - curl, a stale tab, a script -
// could work normally with a password that was supposed to be replaced
// first. That matters most for the provisioning password of the first
// System Administrator, which is a delivery mechanism rather than a
// credential the operator chose.
//
// Everything is refused with 403 until the password is changed, except the
// three operations someone in that state legitimately needs: changing the
// password, reading their own identity so the UI can route them there, and
// signing out.
//
// Cost: one indexed primary-key lookup per authenticated MVC request. The
// flag is deliberately not read from the JWT - the token is issued for up
// to eight hours, so a stale claim would keep letting the user through
// after they had changed their password, or keep blocking them after an
// administrator cleared the flag.
public class MustChangePasswordFilter : IAsyncActionFilter
{
    // AuthenticationController actions that stay reachable while the flag
    // is set. Matched against that controller specifically so an action of
    // the same name elsewhere cannot slip through.
    private static readonly HashSet<string> AllowedAuthenticationActions =
        new(StringComparer.Ordinal) { "ChangePassword", "Me", "Logout" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // Anonymous endpoints - login, refresh, password reset - are not
        // this filter's business.
        if (user?.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        if (context.ActionDescriptor is ControllerActionDescriptor descriptor
            && descriptor.ControllerTypeInfo.AsType() == typeof(AuthenticationController)
            && AllowedAuthenticationActions.Contains(descriptor.ActionName))
        {
            await next();
            return;
        }

        if (!int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            await next();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<MicroLimsDbContext>();
        var mustChangePassword = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.MustChangePassword)
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (mustChangePassword == true)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(
                "Your password must be changed before you can use MicroLIMS."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
