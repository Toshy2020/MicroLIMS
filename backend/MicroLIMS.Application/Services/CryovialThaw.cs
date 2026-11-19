using System.Globalization;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// The one place a vial comes out of a cryovial batch - shared by the
// stand-alone thaw (CryovialService.ThawVialAsync) and the thaw recorded
// from inside a media evaluation (MediaEvaluationEngine.
// ThawAndSelectVialAsync), so both apply the same gate and both leave a
// ThawEvent behind. Adds the event to the context; the caller saves.
public static class CryovialThaw
{
    // A thawed vial can serve challenges for this long after thawing.
    // After that it may no longer be used and becomes eligible for
    // destruction as biological waste (sooner if its batch is withdrawn).
    public static readonly TimeSpan UsableFor = TimeSpan.FromHours(24);

    // The analyst who thawed a vial is reminded (My Tasks) once it has
    // gone this long without being destroyed.
    public static readonly TimeSpan DestructionReminderAfter = TimeSpan.FromHours(48);

    public readonly record struct CryovialUsabilityCheck(
        bool IsApproved,
        bool IsDestroyed,
        bool IsExpired,
        int VialsRemaining)
    {
        public bool IsUsable => IsApproved && !IsDestroyed && !IsExpired && VialsRemaining > 0;
    }

    public static CryovialUsabilityCheck EvaluateUsability(
        ApprovalGateStatus approvalStatus,
        bool isDestroyed,
        DateTime expiryUtc,
        int vialsRemaining,
        DateTime utcNow) =>
        new(
            approvalStatus == ApprovalGateStatus.Approved,
            isDestroyed,
            expiryUtc.Date < utcNow.Date,
            vialsRemaining);

    public static ThawEvent Record(IMicroLimsDbContext db, Cryovial cryovial, int userId, string? notes, DateTime utcNow)
    {
        var check = EnsureUsableCore(cryovial, utcNow);
        if (check.VialsRemaining <= 0)
            throw new BusinessRuleException(
                MessageCodes.CryovialNoVialsRemaining,
                $"No vials remaining in batch {cryovial.Code}.",
                new Dictionary<string, string> { ["code"] = cryovial.Code });

        cryovial.VialsRemaining--;
        var thaw = new ThawEvent
        {
            CryovialId = cryovial.Id,
            Cryovial = cryovial,
            ThawedAt = utcNow,
            ThawedByUserId = userId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
        db.ThawEvents.Add(thaw);
        return thaw;
    }

    // Within its 24-hour window and not yet destroyed.
    public static void EnsureThawedVialUsable(ThawEvent thaw, DateTime utcNow)
    {
        if (thaw.DestructionItem is not null)
            throw new BusinessRuleException(
                MessageCodes.CryovialThawedVialDestroyed,
                $"The vial of {thaw.Cryovial?.Code} thawed {thaw.ThawedAt:dd-MMM-yyyy HH:mm} has been destroyed.",
                new Dictionary<string, string>
                {
                    ["code"] = thaw.Cryovial?.Code ?? string.Empty,
                    ["thawedAt"] = thaw.ThawedAt.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture)
                });
        if (thaw.ThawedAt.Add(UsableFor) <= utcNow)
            throw new BusinessRuleException(
                MessageCodes.CryovialThawedVialExpired,
                $"The vial of {thaw.Cryovial?.Code} thawed {thaw.ThawedAt:dd-MMM-yyyy HH:mm} is more than {UsableFor.TotalHours:0} hours old and can no longer be used - it is due for destruction.",
                new Dictionary<string, string>
                {
                    ["code"] = thaw.Cryovial?.Code ?? string.Empty,
                    ["thawedAt"] = thaw.ThawedAt.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture),
                    ["hours"] = UsableFor.TotalHours.ToString("0", CultureInfo.InvariantCulture)
                });
    }

    // Approved, not destroyed/withdrawn, not expired - required both to
    // thaw a vial and to use an already-thawed vial from the batch.
    public static void EnsureUsable(Cryovial cryovial, DateTime utcNow) =>
        EnsureUsableCore(cryovial, utcNow);

    private static CryovialUsabilityCheck EnsureUsableCore(Cryovial cryovial, DateTime utcNow)
    {
        var check = EvaluateUsability(
            cryovial.ApprovalStatus,
            cryovial.IsDestroyed,
            cryovial.ExpiryDate,
            cryovial.VialsRemaining,
            utcNow);

        if (!check.IsApproved)
            throw new BusinessRuleException(
                MessageCodes.CryovialNotApprovedForUse,
                $"Cryovial batch {cryovial.Code} is not approved for use.",
                new Dictionary<string, string> { ["code"] = cryovial.Code });
        if (check.IsDestroyed)
            throw new BusinessRuleException(
                MessageCodes.CryovialWithdrawnForDestruction,
                $"Cryovial batch {cryovial.Code} has been withdrawn for destruction and cannot be used.",
                new Dictionary<string, string> { ["code"] = cryovial.Code });
        if (check.IsExpired)
            throw new BusinessRuleException(
                MessageCodes.CryovialBatchExpired,
                $"Cryovial batch {cryovial.Code} is expired and cannot be used.",
                new Dictionary<string, string> { ["code"] = cryovial.Code });

        return check;
    }
}
