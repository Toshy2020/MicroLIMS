using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public record GeneralAgarChallengeRequest(int MediaId, string OrganismName, int CryovialId, string Atcc, string InitialInoculum, int OldMediaResult, int NewMediaResult, bool NegativeControlGrowth, int UserId);
public record GeneralBrothChallengeRequest(int MediaId, string TurbidResult, int UserId); // "Turbid" or "Clear"
public record SelectiveChallengeRequest(int MediaId, string Panel, string OrganismName, int CryovialId, string InitialInoculum, string ObservationText, bool Passed, int UserId); // Panel = "Inhibition" or "Indication"

// Media preparation -> Sterility -> Recovery -> Release. Pass/fail
// mechanics differ entirely by MediaType.Class:
//  - GeneralAgar: Recovery% = New/Old*100 vs a threshold band; Negative
//    Control growth auto-fails the whole run.
//  - GeneralBroth: Turbid = pass, Clear = fail; no Negative Control, no Recovery%.
//  - Selective (Agar/Broth): independent Inhibition + Indication panels,
//    each analyst-judged Pass/Fail; no Negative Control (Inhibition
//    already serves that role).
public interface IGptWorkflowEngine
{
    Task<Media> AdvanceStageAsync(int mediaId, int performedByUserId);
    Task<GptChallengeResult> RecordGeneralAgarChallengeAsync(GeneralAgarChallengeRequest request);
    Task<GptChallengeResult> RecordGeneralBrothChallengeAsync(GeneralBrothChallengeRequest request);
    Task<GptChallengeResult> RecordSelectiveChallengeAsync(SelectiveChallengeRequest request);
    Task<Media> ReleaseAsync(int mediaId, int performedByUserId);
    Task<bool> IsReleasedForUseAsync(int mediaId);
}

public class GptWorkflowEngine : IGptWorkflowEngine
{
    private readonly MicroLimsDbContext _db;

    public GptWorkflowEngine(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<GptChallengeResult> RecordGeneralAgarChallengeAsync(GeneralAgarChallengeRequest request)
    {
        var media = await LoadForRecovery(request.MediaId);
        if (media.MediaType!.Class != MediaClass.GeneralAgar)
            throw new InvalidOperationException("This media type is not General Agar class.");
        await EnsureCryovialApprovedAsync(request.CryovialId);

        var recovery = request.OldMediaResult == 0 ? 0 : Math.Round((decimal)request.NewMediaResult / request.OldMediaResult * 100, 1);

        var passesThreshold = !media.MediaType.RecoveryPercentMin.HasValue
            || (recovery >= media.MediaType.RecoveryPercentMin && recovery <= media.MediaType.RecoveryPercentMax);

        var passed = !request.NegativeControlGrowth && passesThreshold;

        var result = new GptChallengeResult
        {
            MediaId = media.Id, Panel = "General", OrganismName = request.OrganismName, CryovialId = request.CryovialId,
            Atcc = request.Atcc, InitialInoculum = request.InitialInoculum,
            OldMediaResult = request.OldMediaResult, NewMediaResult = request.NewMediaResult,
            RecoveryPercent = recovery, NegativeControlGrowth = request.NegativeControlGrowth,
            Passed = passed, RecordedByUserId = request.UserId
        };
        _db.GptChallengeResults.Add(result);
        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<GptChallengeResult> RecordGeneralBrothChallengeAsync(GeneralBrothChallengeRequest request)
    {
        var media = await LoadForRecovery(request.MediaId);
        if (media.MediaType!.Class != MediaClass.GeneralBroth)
            throw new InvalidOperationException("This media type is not General Broth class.");

        var result = new GptChallengeResult
        {
            MediaId = media.Id, Panel = "General", TurbidResult = request.TurbidResult,
            Passed = request.TurbidResult.Equals("Turbid", StringComparison.OrdinalIgnoreCase),
            RecordedByUserId = request.UserId
        };
        _db.GptChallengeResults.Add(result);
        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<GptChallengeResult> RecordSelectiveChallengeAsync(SelectiveChallengeRequest request)
    {
        var media = await LoadForRecovery(request.MediaId);
        if (media.MediaType!.Class is not (MediaClass.SelectiveAgar or MediaClass.SelectiveBroth))
            throw new InvalidOperationException("This media type is not a Selective class.");
        await EnsureCryovialApprovedAsync(request.CryovialId);

        string? expectedDescription = null;
        if (request.Panel == "Indication")
        {
            expectedDescription = (await _db.Set<ExpectedIndicationResult>()
                .FirstOrDefaultAsync(e => e.MediaTypeId == media.MediaTypeId && e.OrganismName == request.OrganismName))
                ?.ExpectedDescription;
        }

        var result = new GptChallengeResult
        {
            MediaId = media.Id, Panel = request.Panel, OrganismName = request.OrganismName, CryovialId = request.CryovialId,
            InitialInoculum = request.InitialInoculum, ObservationText = request.ObservationText,
            ExpectedDescription = expectedDescription, Passed = request.Passed, RecordedByUserId = request.UserId
        };
        _db.GptChallengeResults.Add(result);
        await _db.SaveChangesAsync();
        return result;
    }

    private async Task EnsureCryovialApprovedAsync(int cryovialId)
    {
        var approved = await _db.Cryovials.AnyAsync(c => c.Id == cryovialId && c.ApprovalStatus == Domain.Enums.ApprovalGateStatus.Approved && !c.IsDestroyed);
        if (!approved)
            throw new InvalidOperationException("This cryovial is not approved for use - GPT cannot proceed with it.");
    }

    private async Task<Media> LoadForRecovery(int mediaId)
    {
        var media = await _db.Media.Include(m => m.MediaType).FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new InvalidOperationException($"Media {mediaId} not found.");
        if (media.GptStage != GptStage.Recovery)
            throw new InvalidOperationException("Challenge results can only be recorded at the Recovery stage.");
        return media;
    }

    // Automatically finds the most recent GPT-released lot of the same
    // MediaType, used as the recovery baseline for General Agar.
    public async Task<Media?> GetPreviousReleasedLotAsync(int mediaTypeId, int excludeMediaId)
    {
        return await _db.Media
            .Where(m => m.MediaTypeId == mediaTypeId && m.GptStage == GptStage.Release && m.Id != excludeMediaId)
            .OrderByDescending(m => m.PreparedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Media> AdvanceStageAsync(int mediaId, int performedByUserId)
    {
        var media = await _db.Media.Include(m => m.MediaType).Include(m => m.GptResults).FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new InvalidOperationException($"Media {mediaId} not found.");

        media.GptStage = media.GptStage switch
        {
            GptStage.Preparation => GptStage.Sterility,
            GptStage.Sterility => GptStage.Recovery,
            GptStage.Recovery => EvaluateOutcome(media),
            GptStage.Release => throw new InvalidOperationException("Media has already been released."),
            GptStage.Rejected => throw new InvalidOperationException("Media was rejected and cannot advance further."),
            _ => media.GptStage
        };

        if (media.GptStage == GptStage.Release)
            media.Status = MediaStatus.Active;

        await _db.SaveChangesAsync();
        return media;
    }

    private static GptStage EvaluateOutcome(Media media)
    {
        if (media.GptResults.Count == 0)
            throw new InvalidOperationException("At least one challenge result is required before release.");

        // A lot passes GPT only if every recorded challenge result passed,
        // regardless of media class or panel.
        return media.GptResults.All(r => r.Passed) ? GptStage.Release : GptStage.Rejected;
    }

    public async Task<Media> ReleaseAsync(int mediaId, int performedByUserId)
    {
        var media = await _db.Media.Include(m => m.GptResults).FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new InvalidOperationException($"Media {mediaId} not found.");

        if (media.GptStage != GptStage.Recovery)
            throw new InvalidOperationException("Media must complete the Recovery stage before it can be released.");

        media.GptStage = EvaluateOutcome(media);
        if (media.GptStage != GptStage.Release)
            throw new InvalidOperationException("Media failed GPT and cannot be released.");

        media.Status = MediaStatus.Active;
        await _db.SaveChangesAsync();
        return media;
    }

    public async Task<bool> IsReleasedForUseAsync(int mediaId)
    {
        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new InvalidOperationException($"Media {mediaId} not found.");
        return media.IsReleasedForUse && media.ExpiryDate > DateTime.UtcNow;
    }
}
