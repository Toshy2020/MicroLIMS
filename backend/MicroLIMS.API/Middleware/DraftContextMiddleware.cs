using System.Globalization;
using System.Text.RegularExpressions;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.API.Middleware;

// A bench tablet keeps an unsaved entry as a local draft and, when the analyst
// finally saves it, sends the draft's start time in X-Draft-Started-At. This
// middleware turns that into a plain-language SourceContext for the audit rows
// of the request, so a reviewer can see the entry began earlier on a device.
//
// The device clock is not trusted and not used: the text says "not verified",
// the time is stored as stated (normalised to UTC; a future or very old time is
// kept, never corrected or rejected), and the audit Timestamp stays the server
// clock. A missing, malformed or over-long header changes nothing.
//
// The DbContext is scoped, so it arrives in InvokeAsync, never the constructor
// (middleware is a singleton; CLAUDE.md Rule 5.5). Must run after
// authentication, with the audit pipeline. Saves nothing itself.
public partial class DraftContextMiddleware
{
    public const string HeaderName = "X-Draft-Started-At";

    private const int MaxHeaderLength = 64;

    // The offset is mandatory: without one the device did not say which clock it
    // meant, and the parser would silently assume the server's local zone.
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T.*([Zz]|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoWithOffset();

    private readonly RequestDelegate _next;

    public DraftContextMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context, MicroLimsDbContext db)
    {
        var sourceContext = Describe(context.Request.Headers[HeaderName].ToString());
        if (sourceContext is not null)
            db.CurrentSourceContext = sourceContext;

        return _next(context);
    }

    internal static string? Describe(string? header)
    {
        if (string.IsNullOrWhiteSpace(header) || header.Length > MaxHeaderLength)
            return null;

        var candidate = header.Trim();
        if (!IsoWithOffset().IsMatch(candidate) ||
            !DateTimeOffset.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startedAt))
            return null;

        var utc = startedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return $"Submitted from a device draft started at {utc} UTC (device clock, not verified)";
    }
}
