using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// The mechanics every gated record shares: sign (which verifies the
// password), then log a lifecycle event with the signer's name captured
// at write time. Samples, Media lots, and Cryovial batches all route
// their gate transitions through here so there is exactly one
// implementation of "how a signed decision is recorded".
//
// Neither method calls SaveChangesAsync - the caller commits these
// alongside its own status changes so the signature, the event, and the
// state transition land together or not at all.
public class ReviewGateService
{
    private readonly MicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatureService;

    public ReviewGateService(MicroLimsDbContext db, IElectronicSignatureService signatureService)
    {
        _db = db;
        _signatureService = signatureService;
    }

    // Signs first: if password verification fails this throws before any
    // event is written, so a rejected signature never leaves a
    // half-recorded decision behind.
    public async Task<ElectronicSignature> SignAndLogAsync(
        string entityType, int entityId, int userId, string password,
        SignatureMeaning meaning, ReviewWorkflowEventType eventType,
        string? comment, string? ipAddress, ApprovalDecision? decision = null)
    {
        var signature = await _signatureService.SignAsync(userId, password, meaning, entityType, entityId, comment, ipAddress);

        _db.ReviewWorkflowEvents.Add(new ReviewWorkflowEvent
        {
            EntityType = entityType,
            EntityId = entityId,
            EventType = eventType,
            PerformedByUserId = userId,
            PerformedByNameSnapshot = signature.UserFullNameSnapshot,
            Comment = comment,
            Decision = decision
        });

        return signature;
    }

    // For transitions the system makes on a user's behalf rather than a
    // user signing for - currently only the automatic
    // "all tests complete, submitted for review" hop.
    public async Task LogEventAsync(
        string entityType, int entityId, int userId,
        ReviewWorkflowEventType eventType, string? comment, ApprovalDecision? decision = null)
    {
        var performedByName = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? "Unknown";

        _db.ReviewWorkflowEvents.Add(new ReviewWorkflowEvent
        {
            EntityType = entityType,
            EntityId = entityId,
            EventType = eventType,
            PerformedByUserId = userId,
            PerformedByNameSnapshot = performedByName,
            Comment = comment,
            Decision = decision
        });
    }

    public Task<List<ReviewWorkflowEvent>> GetTimelineAsync(string entityType, int entityId) =>
        _db.ReviewWorkflowEvents
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
}

// Entity type discriminators for ReviewWorkflowEvent/ElectronicSignature.
// Centralized so a typo can't silently split one record's audit trail
// across two spellings.
public static class ReviewEntityTypes
{
    public const string Sample = "Sample";
    public const string Media = "Media";
    public const string Cryovial = "Cryovial";
}
