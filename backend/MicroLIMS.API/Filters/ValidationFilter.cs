using System.Text.Json;
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

            var fieldErrors = new List<FieldError>();
            foreach (var (key, entry) in context.ModelState)
            {
                if (entry is null || entry.Errors.Count == 0)
                {
                    continue;
                }

                var field = ToCamelCaseFieldPath(key);
                foreach (var error in entry.Errors)
                {
                    fieldErrors.Add(new FieldError(field, error.ErrorMessage));
                }
            }

            context.Result = new BadRequestObjectResult(
                ApiResponse<object>.Fail("Validation failed.", errors, fieldErrors));
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    private static string ToCamelCaseFieldPath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var trimmed = key.StartsWith("request.", StringComparison.OrdinalIgnoreCase)
            ? key["request.".Length..]
            : key;

        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        var segments = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i] = JsonNamingPolicy.CamelCase.ConvertName(segments[i]);
        }

        return string.Join('.', segments);
    }
}
