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

    // Approved, not destroyed, not expired - required both to thaw a vial
    // and to use an already-thawed vial from the batch.
    public static void EnsureUsable(Cryovial cryovial, DateTime utcNow)
    {
        if (cryovial.ApprovalStatus != ApprovalGateStatus.Approved)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is not approved for use.");
        if (cryovial.IsDestroyed)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} has been destroyed and cannot be used.");
        if (cryovial.ExpiryDate.Date < utcNow.Date)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is expired and cannot be used.");
    }
}
