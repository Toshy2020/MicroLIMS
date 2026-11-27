using System.Text.RegularExpressions;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.API.Middleware;

public sealed partial class ChangeControlHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Change-Control";

    public async Task InvokeAsync(HttpContext context, IChangeControlContext changeControlContext)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values) && values.Count > 0)
        {
            if (values.Count != 1)
                throw InvalidHeader();

            var normalized = values[0]?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!ValidNumberPattern().IsMatch(normalized))
                throw InvalidHeader();

            changeControlContext.Number = normalized;
        }

        await next(context);
    }

    private static BusinessRuleException InvalidHeader() =>
        new(MessageCodes.ChangeControlHeaderInvalid, "The X-Change-Control header is invalid.");

    [GeneratedRegex("^[A-Z0-9-]{1,20}$")]
    private static partial Regex ValidNumberPattern();
}
