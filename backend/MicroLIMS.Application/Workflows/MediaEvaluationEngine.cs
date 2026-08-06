using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record RecordResultRequest(
    int ChallengeId, int UserId,
    decimal? OldMediaCount, decimal? NewMediaCount, // GrowthPromotion
    bool? GrowthObserved, // Inhibition
    string? ObservedDescription, bool? ManualConform, // Indication - manual judgment, no auto string-matching (see below)
    bool? IsTurbid); // EnrichmentCharacteristics

// Media preparation -> auto-assigned MediaEvaluation -> pick cryovial(s)
// -> record incubation -> record result(s) -> Conform/NonConform. The
// three mechanics (recovery %, inhibition/indication, turbid/clear) are
// carried over unchanged from the old GptWorkflowEngine:
//  - GrowthPromotion: recovery% = new/old*100 vs the MediaType's
//    RecoveryPercentMin/Max band, inclusive.
//  - IndicationInhibition/Inhibition: Conform if no growth observed.
//  - IndicationInhibition/Indication: Conform is a manual analyst
//    judgment (ExpectedDescription is shown for reference only - the
//    old engine never auto-compared observed vs. expected text, so this
//    preserves that exactly rather than inventing a new matching rule).
//  - EnrichmentCharacteristics: Conform if turbid.
public interface IMediaEvaluationEngine
{
    Task SelectCryovialAsync(int challengeId, int cryovialId, int userId);
    Task<Incubation> RecordIncubationAsync(int challengeId, int incubatorEquipmentId, int userId);
    Task<MediaEvaluationChallenge> RecordResultAsync(RecordResultRequest request);
}

public class MediaEvaluationEngine : IMediaEvaluationEngine
{
    private readonly MicroLimsDbContext _db;

    public MediaEvaluationEngine(MicroLimsDbContext db)
    {
        _db = db;
    }

    // Validates the cryovial is Approved/not destroyed/not expired AND
    // that its OrganismId matches the challenge's - does NOT decrement
    // VialsRemaining, since thawing is a separate explicit action
    // (CryovialService.ThawVialAsync) and one thawed vial serves
    // multiple challenges.
    public async Task SelectCryovialAsync(int challengeId, int cryovialId, int userId)
    {
        var challenge = await _db.MediaEvaluationChallenges.Include(c => c.Organism)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException($"Challenge {challengeId} not found.");

        var cryovial = await _db.Cryovials.Include(c => c.Organism)
            .FirstOrDefaultAsync(c => c.Id == cryovialId)
            ?? throw new InvalidOperationException($"Cryovial {cryovialId} not found.");

        EnsureCryovialApproved(cryovial);

        if (cryovial.OrganismId != challenge.OrganismId)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is {cryovial.OrganismNameSnapshot}, not {challenge.Organism?.ScientificName}.");

        challenge.CryovialId = cryovialId;
        await _db.SaveChangesAsync();
    }

    // Same gate as CryovialService.ThawVialAsync's guards.
    private static void EnsureCryovialApproved(Cryovial cryovial)
    {
        if (cryovial.ApprovalStatus != ApprovalGateStatus.Approved || cryovial.IsDestroyed)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is not approved for use.");
        if (cryovial.ExpiryDate.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException($"Cryovial batch {cryovial.Code} is expired and cannot be used.");
    }

    // Temperature/Duration hard-locked from the Media's MediaType - never
    // client-supplied, same rule as IncubationSetupHelper/CountTestWorkflowEngine.
    public async Task<Incubation> RecordIncubationAsync(int challengeId, int incubatorEquipmentId, int userId)
    {
        var challenge = await _db.MediaEvaluationChallenges
            .Include(c => c.MediaEvaluation!).ThenInclude(e => e.Media!).ThenInclude(m => m.MediaType)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException($"Challenge {challengeId} not found.");

        var evaluation = challenge.MediaEvaluation!;
        var mediaType = evaluation.Media!.MediaType!;

        var startedAt = DateTime.UtcNow;
        var incubation = new Incubation
        {
            StepName = "MediaEvaluation",
            MediaId = evaluation.MediaId,
            IncubatorEquipmentId = incubatorEquipmentId,
            StartedAt = startedAt,
            Temperature = $"{mediaType.RequiredTemperatureMin}-{mediaType.RequiredTemperatureMax}",
            Duration = $"{mediaType.IncubationMinHours}-{mediaType.IncubationMaxHours}",
            // The Duration's minimum is a hard gate, not a suggestion -
            // RecordResultAsync refuses to record a result before this
            // time, so a result can never be entered right after
            // incubation is set up.
            ExpectedReadingAt = startedAt.AddHours(mediaType.IncubationMinHours)
        };
        _db.Incubations.Add(incubation);
        await _db.SaveChangesAsync();

        challenge.IncubationId = incubation.Id;
        if (evaluation.Status == MediaEvaluationStatus.Assigned)
            evaluation.Status = MediaEvaluationStatus.InProgress;
        await _db.SaveChangesAsync();

        return incubation;
    }

    public async Task<MediaEvaluationChallenge> RecordResultAsync(RecordResultRequest request)
    {
        var challenge = await _db.MediaEvaluationChallenges
            .Include(c => c.MediaEvaluation!).ThenInclude(e => e.Challenges)
            .Include(c => c.MediaEvaluation!).ThenInclude(e => e.Media!).ThenInclude(m => m.MediaType)
            .Include(c => c.Incubation)
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId)
            ?? throw new InvalidOperationException($"Challenge {request.ChallengeId} not found.");

        var evaluation = challenge.MediaEvaluation!;
        var mediaType = evaluation.Media!.MediaType!;

        // The incubation period is a hard gate, not a suggestion - a
        // result can't be recorded until the MediaType's minimum
        // incubation duration has actually elapsed since incubation
        // was set up.
        if (challenge.Incubation is null)
            throw new InvalidOperationException("Incubation must be recorded before a result can be entered.");
        if (DateTime.UtcNow < challenge.Incubation.ExpectedReadingAt)
            throw new InvalidOperationException(
                $"Incubation is still in progress - earliest reading time is {challenge.Incubation.ExpectedReadingAt:yyyy-MM-dd HH:mm} UTC.");

        switch (evaluation.EvaluationType, challenge.ChallengeRole)
        {
            case (EvaluationType.GrowthPromotion, _):
                if (request.OldMediaCount is null || request.NewMediaCount is null)
                    throw new InvalidOperationException("Old and new media counts are required.");
                if (request.OldMediaCount == 0)
                    throw new InvalidOperationException("Old media count cannot be zero - recovery% cannot be calculated.");

                var recovery = Math.Round(request.NewMediaCount.Value / request.OldMediaCount.Value * 100, 1);
                challenge.OldMediaCount = request.OldMediaCount;
                challenge.NewMediaCount = request.NewMediaCount;
                challenge.RecoveryPercent = recovery;
                challenge.Outcome = !mediaType.RecoveryPercentMin.HasValue
                    || (recovery >= mediaType.RecoveryPercentMin && recovery <= mediaType.RecoveryPercentMax)
                    ? EvaluationOutcome.Conform : EvaluationOutcome.NonConform;
                break;

            case (EvaluationType.IndicationInhibition, ChallengeRole.Inhibition):
                if (request.GrowthObserved is null)
                    throw new InvalidOperationException("Growth observed is required.");
                challenge.GrowthObserved = request.GrowthObserved;
                challenge.Outcome = request.GrowthObserved == false ? EvaluationOutcome.Conform : EvaluationOutcome.NonConform;
                break;

            case (EvaluationType.IndicationInhibition, ChallengeRole.Indication):
                if (request.ObservedDescription is null || request.ManualConform is null)
                    throw new InvalidOperationException("Observed description and a Conform/NonConform judgment are required.");
                challenge.ObservedDescription = request.ObservedDescription;
                challenge.Outcome = request.ManualConform.Value ? EvaluationOutcome.Conform : EvaluationOutcome.NonConform;
                break;

            case (EvaluationType.EnrichmentCharacteristics, _):
                if (request.IsTurbid is null)
                    throw new InvalidOperationException("Turbid/clear is required.");
                challenge.IsTurbid = request.IsTurbid;
                challenge.Outcome = request.IsTurbid == true ? EvaluationOutcome.Conform : EvaluationOutcome.NonConform;
                break;

            default:
                throw new InvalidOperationException("This challenge has no recognized evaluation type / challenge role combination.");
        }

        challenge.ReadAt = DateTime.UtcNow;
        challenge.ReadByUserId = request.UserId;

        // A completed evaluation QUALIFIES a lot; it no longer releases
        // it. On Conform the lot stays at ApprovalStatus.PendingReview
        // awaiting a Section Head signature (MediaReleaseService.
        // DecideAsync) - deliberately, so no one person can both produce
        // a lot and put it into routine testing. On NonConform the lot
        // is quarantined outright and never reaches the approval queue.
        if (evaluation.Challenges.All(c => c.Outcome.HasValue))
        {
            evaluation.Status = MediaEvaluationStatus.Completed;
            evaluation.Outcome = evaluation.Challenges.All(c => c.Outcome == EvaluationOutcome.Conform)
                ? EvaluationOutcome.Conform : EvaluationOutcome.NonConform;
            evaluation.CompletedAt = DateTime.UtcNow;
            evaluation.CompletedByUserId = request.UserId;

            if (evaluation.Outcome == EvaluationOutcome.NonConform)
                evaluation.Media.Status = MediaStatus.QuarantineFailed;
        }

        await _db.SaveChangesAsync();
        return challenge;
    }
}
