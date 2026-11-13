using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// The two account changes the company directory can cause, shared by the sign-in services and the
/// leaver service so they cannot drift apart. Neither method saves: the caller sets the audit context
/// (system process name, reason), saves, and owns the transaction.
/// </summary>
internal static class DirectoryAccountActions
{
    /// <summary>
    /// The directory says the person has left or been suspended: close the account and every open session.
    /// Returns the number of sessions revoked.
    /// </summary>
    public static async Task<int> DeactivateAsync(
        User user,
        DateTime nowUtc,
        AuthenticationService auth,
        ISecurityAuditService securityAudit,
        string eventReason)
    {
        user.IsActive = false;
        user.DirectorySignInRefusedAt = nowUtc;
        var revoked = await auth.RevokeAllRefreshTokensAsync(user.Id);

        securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.UserDeactivatedByDirectory,
            SecurityEventOutcome.Success,
            ActorIsSystem: true,
            TargetUserId: user.Id,
            TargetUsername: user.Username,
            Reason: eventReason,
            Metadata: $"{{\"revokedSessionCount\":{revoked}}}"));
        return revoked;
    }

    /// <summary>
    /// A company sign-in for a linked user was refused for a directory reason (not a wrong password):
    /// remember it so the administrator sees "sign-in refused" on the Users list. Saves, attributed
    /// to the named system process.
    /// </summary>
    public static async Task MarkSignInRefusedAsync(
        IMicroLimsDbContext db,
        User user,
        DateTime nowUtc,
        string systemProcessName,
        string reason,
        CancellationToken ct)
    {
        var previousProcess = db.CurrentSystemProcessName;
        var previousReason = db.CurrentReason;
        var previousUser = db.CurrentUserId;
        db.CurrentUserId = null;
        db.CurrentSystemProcessName = systemProcessName;
        db.CurrentReason = reason;
        try
        {
            user.DirectorySignInRefusedAt = nowUtc;
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.CurrentSystemProcessName = previousProcess;
            db.CurrentReason = previousReason;
            db.CurrentUserId = previousUser;
        }
    }
}
