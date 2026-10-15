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

    public static ThawEvent Record(IMicroLimsDbContext db, Cryovial cryovial, int userId, string? notes, DateTime utcNow)
    {
        EnsureUsable(cryovial, utcNow);
        if (cryovial.VialsRemaining <= 0)
            throw new InvalidOperationException($"No vials remaining in batch {cryovial.Code}.");

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
            throw new InvalidOperationException($"The vial of {thaw.Cryovial?.Code} thawed {thaw.ThawedAt:dd-MMM-yyyy HH:mm} has been destroyed.");
        if (thaw.ThawedAt.Add(UsableFor) <= utcNow)
            throw new InvalidOperationException($"The vial of {thaw.Cryovial?.Code} thawed {thaw.ThawedAt:dd-MMM-yyyy HH:mm} is more than {UsableFor.TotalHours:0} hours old and can no longer be used - it is due for destruction.");
    }

    // Approved, not destroyed/withdrawn, not expired - required both to
    // thaw a vial and to use an already-thawed vial from the batch.
    public static void EnsureUsable(Cryovial cryovial, DateTime utcNow)
    {
        if (cryovial.ApprovalStatus != ApprovalGateStatus.Approved)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is not approved for use.");
        if (cryovial.IsDestroyed)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} has been withdrawn for destruction and cannot be used.");
        if (cryovial.ExpiryDate.Date < utcNow.Date)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is expired and cannot be used.");
    }
}
