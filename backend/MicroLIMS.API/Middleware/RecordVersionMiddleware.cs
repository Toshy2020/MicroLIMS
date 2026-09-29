using System.Globalization;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.API.Middleware;

// Reads If-Match - the Version of the record the client's form was loaded
// with - onto the DbContext, where RecordVersion.EnsureCurrent compares it
// with the record being edited. Quotes are accepted (an ETag is quoted);
// anything that is not a single version number is ignored.
public class RecordVersionMiddleware
{
    private readonly RequestDelegate _next;

    public RecordVersionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, MicroLimsDbContext db)
    {
        var header = context.Request.Headers.IfMatch.ToString().Trim().Trim('"');
        if (uint.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            db.ExpectedVersion = version;

        await _next(context);
    }
}
