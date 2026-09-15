using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.Configurations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;

namespace MicroLIMS.Application.Services;

public record PrepareMediaRequest(
    int MaterialId, decimal TotalWeight, string TotalVolume,
    int AutoclaveEquipmentId, string AutoclaveProgram, string LoadType, decimal Temperature,
    int CycleTime, int CycleNumber, decimal Ph, DateTime ExpiryDate, int UserId);

// The Media Preparation module - captures the full prepared-lot record.
// Lot number format: {Product.Code}/{seq:D2}/{yy} - the prefix comes from the
// product code, so a code change starts a new /01/ series (one sequence per
// code per year, continuing past the highest number already issued - see
// PreparedLotNumber). ManufacturerLot/ManufacturerName are copied from the
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

        if (material.MediaProductId is null)
            throw new InvalidOperationException(
                $"Batch {material.BatchNumber} of {material.MaterialName} isn't linked to a configured media product - edit it in Inventory > Materials Stock and choose the product before preparing.");

        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == material.MediaProductId.Value)
            ?? throw new InvalidOperationException($"Media product with ID {material.MediaProductId.Value} not found.");

        // Exactly one MediaConfiguration exists per product (unique on
        // MediaProductId), carrying its EvaluationType and challenge organisms.
        // The OrderBy(c => c.Id) is preserved for consistency with earlier phases.
        var config = await _db.MediaConfigurations.Include(c => c.Challenges)
            .Where(c => c.MediaProductId == product.Id)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"No Media Configuration exists yet for \"{product.Name}\" - configure it in Laboratory Configuration before preparing a lot.");

        var lotPrefix = product.Code;

        var media = new Media
        {
            MaterialId = material.Id,
            LotNumber = await PreparedLotNumber.NextAsync(_db.Media.Select(m => m.LotNumber), lotPrefix),
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

        // Two preparations under the same code at the same moment both
        // pick the same next number. The unique index rejects the second
        // save, which then takes the number after the one that won. A
        // second clash in a row is reported rather than retried again.
        if (!await UniqueIndexSave.TrySaveChangesAsync(_db, MediaLotConfiguration.LotNumberIndexName))
        {
            media.LotNumber = await PreparedLotNumber.NextAsync(_db.Media.Select(m => m.LotNumber), lotPrefix);
            if (!await UniqueIndexSave.TrySaveChangesAsync(_db, MediaLotConfiguration.LotNumberIndexName))
                throw new InvalidOperationException(
                    $"Lot number {media.LotNumber} was taken by another preparation at the same moment. Nothing was saved - submit the preparation again.");
        }

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
}
