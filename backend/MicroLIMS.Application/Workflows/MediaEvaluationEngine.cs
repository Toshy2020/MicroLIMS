using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record RecordResultRequest(
    int ChallengeId, int UserId,
    decimal? OldMediaCount, decimal? NewMediaCount, // GrowthPromotion
    int? ReferenceMediaId, string? ReferenceMediaLabel, // GrowthPromotion - exactly one of these two
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
    Task SelectLyophilizedDiskAsync(int challengeId, int materialId, int userId);
    Task<Incubation> RecordIncubationAsync(int challengeId, int incubatorEquipmentId, int userId);
    Task<MediaEvaluationChallenge> RecordResultAsync(RecordResultRequest request);
}

public class MediaEvaluationEngine : IMediaEvaluationEngine
{
    private readonly MicroLimsDbContext _db;
    private readonly MaterialService _materialService;

    public MediaEvaluationEngine(MicroLimsDbContext db, MaterialService materialService)
    {
        _db = db;
        _materialService = materialService;
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
        challenge.LyophilizedDiskId = null; // mutually exclusive with a disk source
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

    // Alternative to SelectCryovialAsync - uses the raw LyophilizedMicroorganism
    // Material directly instead of a prepared Cryovial batch. No approval-gate
    // workflow to check (disks don't have one, unlike Cryovials) - organism
    // match, not expired, and quantity remaining are the only requirements.
    public async Task SelectLyophilizedDiskAsync(int challengeId, int materialId, int userId)
    {
        var challenge = await _db.MediaEvaluationChallenges.Include(c => c.Organism)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException($"Challenge {challengeId} not found.");

        if (challenge.LyophilizedDiskId == materialId)
            return; // already selected - no new disc to consume

        var material = await _db.Materials.Include(m => m.Organism).FirstOrDefaultAsync(m => m.Id == materialId)
            ?? throw new InvalidOperationException($"Material {materialId} not found.");

        if (material.MaterialType != MaterialType.LyophilizedMicroorganism)
            throw new InvalidOperationException($"Material {material.MaterialName} is not a lyophilized microorganism disk.");
        if (material.OrganismId != challenge.OrganismId)
            throw new InvalidOperationException($"Material {material.MaterialName} is {material.Organism?.ScientificName ?? "a different organism"}, not {challenge.Organism?.ScientificName}.");
        if (material.ExpiryDate.HasValue && material.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException($"Material {material.MaterialName} (batch {material.BatchNumber}) is expired and cannot be used.");
        if (material.QuantityRemaining <= 0)
            throw new InvalidOperationException($"Material {material.MaterialName} (batch {material.BatchNumber}) has no quantity remaining.");

        // Unlike a Cryovial (already prepared/reusable from an earlier
        // explicit step - see SelectCryovialAsync above, which deliberately
        // never decrements stock), a raw disk has no separate "prepare"
        // step: choosing one here IS the moment of physical consumption,
        // so exactly one disc comes out of Materials Stock right now.
        await _materialService.ConsumeAsync(materialId, MaterialType.LyophilizedMicroorganism, 1m, userId);

        challenge.LyophilizedDiskId = materialId;
        challenge.CryovialId = null; // mutually exclusive with a cryovial source
        await _db.SaveChangesAsync();
    }

    // Temperature/Duration hard-locked from the Media's product - never
    // client-supplied, same rule as CountTestWorkflowEngine.
    public async Task<Incubation> RecordIncubationAsync(int challengeId, int incubatorEquipmentId, int userId)
    {
        var challenge = await _db.MediaEvaluationChallenges
            .Include(c => c.MediaEvaluation!).ThenInclude(e => e.Media!).ThenInclude(m => m.Material)
            .FirstOrDefaultAsync(c => c.Id == challengeId)
            ?? throw new InvalidOperationException($"Challenge {challengeId} not found.");

        var evaluation = challenge.MediaEvaluation!;
        var config = await GetCanonicalConfigAsync(evaluation.Media!);

        var startedAt = DateTime.UtcNow;
        var incubation = new Incubation
        {
            StepName = "MediaEvaluation",
            MediaId = evaluation.MediaId,
            IncubatorEquipmentId = incubatorEquipmentId,
            StartedAt = startedAt,
            Temperature = $"{config.TemperatureMin}-{config.TemperatureMax}",
            Duration = $"{config.IncubationMinHours}-{config.IncubationMaxHours}",
            // The Duration's minimum is a hard gate, not a suggestion -
            // RecordResultAsync refuses to record a result before this
            // time, so a result can never be entered right after
            // incubation is set up.
            ExpectedReadingAt = startedAt.AddHours(config.IncubationMinHours)
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
            .Include(c => c.MediaEvaluation!).ThenInclude(e => e.Media!).ThenInclude(m => m.Material)
            .Include(c => c.Incubation)
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId)
            ?? throw new InvalidOperationException($"Challenge {request.ChallengeId} not found.");

        var evaluation = challenge.MediaEvaluation!;
        var config = await GetCanonicalConfigAsync(evaluation.Media!);

        // The incubation period is a hard gate, not a suggestion - a
        // result can't be recorded until the canonical incubation
        // duration has actually elapsed since incubation was set up.
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

                var hasLinkedReference = request.ReferenceMediaId.HasValue;
                var hasFreeTextReference = !string.IsNullOrWhiteSpace(request.ReferenceMediaLabel);
                if (hasLinkedReference == hasFreeTextReference)
                    throw new InvalidOperationException("A reference lot is required - either a linked prior lot or a free-text description, not both or neither.");

                if (hasLinkedReference)
                {
                    var referenceMediaExists = await _db.Media.AnyAsync(m => m.Id == request.ReferenceMediaId!.Value);
                    if (!referenceMediaExists)
                        throw new InvalidOperationException($"Reference media lot {request.ReferenceMediaId} not found.");
                }

                var recovery = Math.Round(request.NewMediaCount.Value / request.OldMediaCount.Value * 100, 1);
                challenge.OldMediaCount = request.OldMediaCount;
                challenge.NewMediaCount = request.NewMediaCount;
                challenge.RecoveryPercent = recovery;
                challenge.ReferenceMediaId = hasLinkedReference ? request.ReferenceMediaId : null;
                challenge.ReferenceMediaLabel = hasFreeTextReference ? request.ReferenceMediaLabel!.Trim() : null;
                challenge.Outcome = !config.RecoveryPercentMin.HasValue
                    || (recovery >= config.RecoveryPercentMin && recovery <= config.RecoveryPercentMax)
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
                evaluation.Media!.Status = MediaStatus.QuarantineFailed;
        }

        await _db.SaveChangesAsync();
        return challenge;
    }

    // A product can have more than one MediaConfiguration row (e.g.
    // Tryptic Soy Agar's Standard vs. Extended Transfer usages). For the
    // GPT/challenge evaluation specifically - a property of the prepared
    // lot itself, not of any downstream TestWorkflowStep - the lowest-Id
    // row is the confirmed canonical one (see the Media Configuration
    // Migration plan: for Tryptic Soy Agar this resolves to "Standard",
    // 1-2h @ 30-35C, not "Extended Transfer").
    private async Task<MediaConfiguration> GetCanonicalConfigAsync(Media media) =>
        await _db.MediaConfigurations
            .Where(c => c.Name == media.Material!.MaterialName)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync()
        ?? throw new InvalidOperationException($"No Media Configuration exists for \"{media.Material!.MaterialName}\".");
}
