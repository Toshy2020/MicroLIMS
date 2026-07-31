using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Filters;

// Example custom authorization filter for cases [Authorize(Roles=...)]
// cannot express (e.g. "Section Head can only edit Items in their own department").
public class AuthorizationFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail("Not authenticated."));
        }
    }
}
