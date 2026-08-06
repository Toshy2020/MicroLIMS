using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using System.Net;
using System.Text.Json;

namespace MicroLIMS.API.Middleware;

// Global exception handling: converts unhandled exceptions into a
// consistent ApiResponse envelope instead of leaking stack traces.
public class ExceptionMiddleware
{
    // Matches the camelCase policy AddJsonOptions applies to normal
    // controller responses - this middleware serializes manually
    // (bypassing MVC's formatter), so without this every error payload
    // came back PascalCase ("Message") while success payloads are
    // camelCase ("data"), breaking every frontend `err.response.data.message` read.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessRuleException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteResponse(context, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            // The services (CryovialService, MediaPreparationService,
            // MaterialService's stock guards, etc.) throw this for business
            // rule violations - "not found", "insufficient stock", "expired",
            // "must be approved first". These messages are meant to reach
            // the analyst, not be replaced with a generic 500.
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteResponse(context, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteResponse(context, ApiResponse<object>.Fail("An unexpected error occurred."));
        }
    }

    private static Task WriteResponse(HttpContext context, object payload)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
