using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Filters;

// Short-circuits with a uniform ApiResponse when model binding/validation fails,
// so controllers do not need to repeat ModelState checks.
public class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            context.Result = new BadRequestObjectResult(ApiResponse<object>.Fail("Validation failed.", errors));
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
