using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using System.Net;
using System.Text.Json;

namespace MicroLIMS.API.Middleware;

// Global exception handling: converts unhandled exceptions into a
// consistent ApiResponse envelope instead of leaking stack traces, and
// records each one as an ErrorLog row on the admin monitoring page.
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
    private readonly IErrorCaptureService _errorCapture;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IErrorCaptureService errorCapture)
    {
        _next = next;
        _logger = logger;
        _errorCapture = errorCapture;
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
            await CaptureAsync(context, ex, ErrorSource.Backend, ErrorSeverity.Warning);
            await WriteResponse(context, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex) when (DatabaseErrorClassifier.IsDatabaseFailure(ex))
        {
            // Matched on the whole inner chain, not the outermost type, and
            // placed above the InvalidOperationException arm on purpose: EF
            // Core wraps a transient connection failure in a plain
            // InvalidOperationException, which would otherwise be reported
            // to the analyst as a 400 business-rule message and recorded as
            // a Backend warning instead of a Critical database outage.
            //
            // Connection failures can only be caught here - Phase 3's
            // pg_stat_* polling can never see a connection that never opened.
            _logger.LogError(ex, "Database failure ({CorrelationId})", CorrelationIdMiddleware.GetCorrelationId(context));
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await CaptureAsync(context, ex, ErrorSource.Database,
                DatabaseErrorClassifier.Classify(ex), DatabaseErrorClassifier.BuildRawContext(ex));
            await WriteResponse(context, ApiResponse<object>.Fail("An unexpected error occurred."));
        }
        catch (BadHttpRequestException ex)
        {
            // Kestrel rejecting the request itself - an oversized body, a
            // malformed chunked encoding, a request-body timeout. It
            // carries its own status, and without this arm it fell into
            // the generic 500 below: the anonymous client-error endpoint's
            // own size cap then let any caller manufacture a Critical
            // incident just by posting something too big.
            context.Response.StatusCode = ex.StatusCode;
            await CaptureAsync(context, ex, ErrorSource.Backend, ErrorSeverity.Warning);
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
            await CaptureAsync(context, ex, ErrorSource.Backend, ErrorSeverity.Warning);
            await WriteResponse(context, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception ({CorrelationId})", CorrelationIdMiddleware.GetCorrelationId(context));
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await CaptureAsync(context, ex, ErrorSource.Backend, ErrorSeverity.Critical);
            await WriteResponse(context, ApiResponse<object>.Fail("An unexpected error occurred."));
        }
    }

    // Never throws - IErrorCaptureService swallows its own failures - so
    // the response written after this call is unaffected either way.
    private Task CaptureAsync(
        HttpContext context,
        Exception exception,
        ErrorSource source,
        ErrorSeverity severity,
        string? rawContext = null)
    {
        return _errorCapture.CaptureAsync(new ErrorCaptureRequest(
            Source: source,
            Severity: severity,
            CorrelationId: CorrelationIdMiddleware.GetCorrelationId(context),
            ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            Message: exception.Message,
            // ToString() rather than StackTrace: it carries the inner
            // exception chain, which is where the real cause usually is.
            StackTrace: exception.ToString(),
            RequestPath: context.Request.Path.Value,
            HttpMethod: context.Request.Method,
            StatusCode: context.Response.StatusCode,
            UserId: ResolveUserId(context),
            RawContext: rawContext));
    }

    // This middleware runs before UseAuthentication, so HttpContext.User is
    // empty here. AuditMiddleware runs later but has already stamped the
    // request-scoped DbContext by the time an exception bubbles back out.
    // Resolved lazily and defensively: when the database is the thing that
    // failed, the context may not be constructible at all.
    private static int? ResolveUserId(HttpContext context)
    {
        try
        {
            return context.RequestServices.GetService<MicroLimsDbContext>()?.CurrentUserId;
        }
        catch
        {
            return null;
        }
    }

    private static Task WriteResponse(HttpContext context, object payload)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
