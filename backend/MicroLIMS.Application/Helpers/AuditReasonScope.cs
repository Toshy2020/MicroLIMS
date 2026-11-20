using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Helpers;

public static class AuditReasonScope
{
    private const int MaxReasonLength = 2000;

    public static IDisposable Append(IMicroLimsDbContext db, string? text)
    {
        var prior = db.CurrentReason;
        if (text is not null && prior?.Contains(text, StringComparison.Ordinal) != true)
        {
            var reason = string.IsNullOrEmpty(prior) ? text : $"{prior} {text}";
            db.CurrentReason = reason[..Math.Min(reason.Length, MaxReasonLength)];
        }

        return new RestoreReason(db, prior);
    }

    private sealed class RestoreReason(IMicroLimsDbContext db, string? prior) : IDisposable
    {
        public void Dispose() => db.CurrentReason = prior;
    }
}
