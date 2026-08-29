using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record PrepareMediaRequest(
    int MaterialId, decimal TotalWeight, string TotalVolume,
    int AutoclaveEquipmentId, string AutoclaveProgram, string LoadType, decimal Temperature,
    int CycleTime, int CycleNumber, decimal Ph, DateTime ExpiryDate, int UserId);

// The Media Preparation module - captures the full prepared-lot record.
// Lot number format: {Material.Code}/{seq:D2}/{yy}, sequence resets
// every year. ManufacturerLot/ManufacturerName are copied from the
// consumed Material, never caller-supplied - the analyst picks a
// Material, not a manufacturer. Nothing here is usable in routine
// testing until its auto-assigned MediaEvaluation completes Conform
// (see MediaEvaluationEngine).
//
// Every prepared lot consumes dehydrated media from the Inventory
// Materials Stock (MaterialService.ConsumeAsync) - TotalWeight grams
// are deducted from the selected Material's QuantityRemaining, guarded
// against expiry and insufficient stock, in the same SaveChangesAsync
// as the new Media row so both commit together or neither does.
public class MediaPreparationService
{
    private readonly MicroLimsDbContext _db;
    private readonly MaterialService _materialService;
    private readonly ReviewGateService _reviewGate;

    public MediaPreparationService(MicroLimsDbContext db, MaterialService materialService, ReviewGateService reviewGate)
    {
        _db = db;
        _materialService = materialService;
        _reviewGate = reviewGate;
    }

    public async Task<Media> PrepareAsync(PrepareMediaRequest request)
    {
        var autoclave = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == request.AutoclaveEquipmentId && e.Type == EquipmentType.Autoclave)
            ?? throw new InvalidOperationException("Selected equipment is not a valid autoclave.");

        // Guards + decrements the Material row in memory - not saved
        // until the SaveChangesAsync below, so a failure here leaves
        // both the stock and the media lot untouched. Reused below for
        // the media identity (Manufacturer fields, lot number prefix)
        // instead of a second lookup.
        var material = await _materialService.ConsumeAsync(request.MaterialId, MaterialType.DehydratedMedia, request.TotalWeight, request.UserId);

        // A product can have more than one MediaConfiguration row (e.g.
        // Tryptic Soy Agar's Standard vs. Extended Transfer usages - see
        // the Media Configuration Migration plan). All rows sharing a
        // Name carry the same EvaluationType and challenge organisms
        // (Phase 3 duplicated challenges across every row for exactly
        // this reason), so picking the lowest-Id row is a stable,
        // deterministic choice, not an arbitrary one, for GPT-evaluation
        // purposes specifically.
        var config = await _db.MediaConfigurations.Include(c => c.Challenges)
            .Where(c => c.Name == material.MaterialName)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"No Media Configuration exists yet for \"{material.MaterialName}\" - configure it in Laboratory Configuration before preparing a lot.");

        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1);
        var countThisYear = await _db.Media.CountAsync(m =>
            m.MaterialId == material.Id && m.PreparedAt >= yearStart && m.PreparedAt < yearEnd);

        var sequence = (countThisYear + 1).ToString("D2");
        var lotPrefix = !string.IsNullOrWhiteSpace(material.Code) ? material.Code : SanitizeForLotPrefix(material.MaterialName);
        var lotNumber = $"{lotPrefix}/{sequence}/{DateTime.UtcNow:yy}";

        var media = new Media
        {
            MaterialId = material.Id,
            LotNumber = lotNumber,
            ManufacturerLot = material.BatchNumber,
            ManufacturerName = material.ManufacturerName,
            TotalWeight = request.TotalWeight,
            TotalVolume = request.TotalVolume,
            AutoclaveEquipmentId = autoclave.Id,
            AutoclaveProgram = request.AutoclaveProgram,
            LoadType = request.LoadType,
            Temperature = request.Temperature,
            CycleTime = request.CycleTime,
            CycleNumber = request.CycleNumber,
            Ph = request.Ph,
            ExpiryDate = request.ExpiryDate,
            Status = MediaStatus.Prepared,
            PreparedByUserId = request.UserId
        };

        _db.Media.Add(media);

        // Auto-assign the Media Evaluation for this lot - EvaluationType
        // and challenge organisms now come directly off the matched
        // MediaConfiguration row via its own FK'd MediaConfigurationChallenge
        // children, not a MaterialName string match against
        // MediaChallengeSpec (the old join that silently failed for any
        // name that drifted - see the migration plan's §2/§5). Zero
        // challenges is allowed (no throw): the analyst needs to be able
        // to prepare media before master data is fully configured, but
        // the evaluation obviously can't Conform until challenges exist.
        var evaluation = new MediaEvaluation { Media = media, EvaluationType = config.EvaluationType, Status = MediaEvaluationStatus.Assigned };
        foreach (var challenge in config.Challenges)
        {
            evaluation.Challenges.Add(new MediaEvaluationChallenge
            {
                OrganismId = challenge.OrganismId,
                ChallengeRole = challenge.ChallengeRole,
                ExpectedDescription = challenge.ExpectedDescription,
                InitialInoculum = challenge.InitialInoculum ?? string.Empty
            });
        }
        _db.MediaEvaluations.Add(evaluation);

        await _db.SaveChangesAsync();
        return media;
    }

    public async Task<List<Media>> GetAllAsync() =>
        await _db.Media.Include(m => m.Material).OrderByDescending(m => m.Id).ToListAsync();

    // includeExpired: the reference-lot lookup for a new GrowthPromotion
    // evaluation (MediaEvaluationController) wants any lot that was ever
    // released, since it's citing a historical count, not asking what can
    // be pulled off the shelf right now - every other caller wants the
    // latter and leaves this false.
    public async Task<List<Media>> GetReleasedAsync(int? materialId = null, bool includeExpired = false, int? excludeId = null)
    {
        var query = _db.Media.Include(m => m.Material).Where(m => m.IsReleasedForUse);
        if (!includeExpired) query = query.Where(m => m.Status == MediaStatus.Active && m.ExpiryDate > DateTime.UtcNow);
        if (materialId.HasValue) query = query.Where(m => m.MaterialId == materialId.Value);
        if (excludeId.HasValue) query = query.Where(m => m.Id != excludeId.Value);
        return await query.OrderByDescending(m => m.Id).ToListAsync();
    }

    public async Task MarkOutOfStockAsync(int mediaId, int userId, string? comment = null)
    {
        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new InvalidOperationException($"Media lot {mediaId} not found.");

        if (!media.IsReleasedForUse || media.Status != MediaStatus.Active)
        {
            throw new InvalidOperationException($"Media lot {media.LotNumber} is not currently released for use and cannot be marked Out of Stock.");
        }

        media.Status = MediaStatus.OutOfStock;

        await _reviewGate.LogEventAsync(
            ReviewEntityTypes.Media,
            mediaId,
            userId,
            ReviewWorkflowEventType.ApprovalDecisionMade,
            comment ?? "Media lot manually marked Out of Stock.");

        await _db.SaveChangesAsync();
    }

    // Material.Code isn't guaranteed present (nullable) - falls back to
    // an alphanumeric, uppercased version of the material's name so the
    // lot number always has a usable prefix.
    private static string SanitizeForLotPrefix(string materialName) =>
        new string(materialName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
