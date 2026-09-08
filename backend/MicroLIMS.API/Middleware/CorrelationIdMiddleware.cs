namespace MicroLIMS.API.Middleware;

// Stamps every request with an id that ties together everything that went
// wrong during it - the backend exception, the database failure behind it,
// and (from Phase 4) the client-side error the user actually saw.
//
// Unrelated to AuditLog.CorrelationId, which is a Guid used to group GxP
// document-control audit events. Same word, different system.
//
// Must run before ExceptionMiddleware so the id already exists by the time
// an exception is caught.
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemsKey = "MicroLims.CorrelationId";

    // Matches ErrorLog.CorrelationId / Incident.CorrelationId varchar(100).
    private const int MaxLength = 100;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadInboundHeader(context) ?? Guid.NewGuid().ToString("N");

        context.Items[ItemsKey] = correlationId;

        // Safe to set directly: this middleware runs first, so the
        // response has not started. Echoed back so the frontend can quote
        // it when reporting a client-side error from the same action -
        // which needs WithExposedHeaders on the CORS policy to be readable.
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }

    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemsKey, out var value) && value is string id ? id : string.Empty;

    // The inbound header is caller-controlled, so it is accepted only if
    // it is a short printable token - anything else and we generate our
    // own rather than let a caller choose what lands in the database.
    private static string? ReadInboundHeader(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var values))
            return null;

        var candidate = values.ToString();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength)
            return null;

        foreach (var c in candidate)
        {
            if (c < '!' || c > '~') return null; // printable ASCII, no spaces
        }

        return candidate;
    }
}
