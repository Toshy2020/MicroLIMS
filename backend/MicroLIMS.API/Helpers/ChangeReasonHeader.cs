using Microsoft.AspNetCore.Http;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Helpers;

// Reads X-Change-Reason (URL-encoded UTF-8) from the HTTP request so existing
// PUT/DELETE/freeze/reinstate endpoints accept a reason without changing DTOs.
public static class ChangeReasonHeader
{
    public const string HeaderName = "X-Change-Reason";

    public static string? Read(HttpRequest? request)
    {
        if (request == null || !request.Headers.TryGetValue(HeaderName, out var values))
            return null;

        var raw = values.FirstOrDefault();
        if (raw is null)
            return null;

        try
        {
            return Uri.UnescapeDataString(raw);
        }
        catch (UriFormatException)
        {
            const string msg = "Invalid change reason header encoding.";
            throw new FieldValidationException(msg, new[] { new FieldError(ChangeReasonGuard.FieldName, msg) });
        }
    }
}
