namespace MicroLIMS.Application.Helpers;

// Every administrative change to a user or role carries the administrator's
// stated reason (21 CFR 11.10(e), Annex 11 s.12/14). The reason is trimmed,
// bounded, and stored in AuditLog.Reason (see IMicroLimsDbContext.CurrentReason).
public static class AdminActionReason
{
    public const int MaxLength = 500;

    public static string Require(string? reason, string action)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException($"A reason is required to {action}.");
        if (trimmed.Length > MaxLength)
            throw new InvalidOperationException($"The reason must be {MaxLength} characters or fewer.");
        return trimmed;
    }
}
