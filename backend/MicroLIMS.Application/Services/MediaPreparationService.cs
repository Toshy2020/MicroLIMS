using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

// One optional additive (e.g. a neutralizer). StockLot: MaterialId + Quantity in the lot's own
// unit, deducted from stock. PreparedSolution: SolutionPreparationId, recorded only.
public record MediaAdditiveInput(
    MediaAdditiveKind Kind, int? MaterialId, int? SolutionPreparationId, decimal Quantity, string? Unit);

public record PrepareMediaRequest(
    int MaterialId, decimal TotalWeight, string TotalVolume,
    int AutoclaveEquipmentId, string AutoclaveProgram, string LoadType, decimal Temperature,
    int CycleTime, int CycleNumber, decimal Ph, DateTime ExpiryDate, int UserId,
    List<MediaAdditiveInput>? Additives = null);

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
    private readonly IMicroLimsDbContext _db;
    private readonly MaterialService _materialService;
    private readonly ReviewGateService _reviewGate;
    private readonly TimeProvider _time;

    public MediaPreparationService(IMicroLimsDbContext db, MaterialService materialService, ReviewGateService reviewGate, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _materialService = materialService;
        _reviewGate = reviewGate;
    }

    public Task<MediaResponse> PrepareAsync(PrepareMediaRequest request) =>
        UnitOfWork.RunAsync(_db, async () => MediaResponse.From(await PrepareCoreAsync(request), _time.GetUtcNow().UtcDateTime));

    private async Task<Media> PrepareCoreAsync(PrepareMediaRequest request)
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

        // Optional additives (D11: none never blocks). Validated and stock-deducted in memory
        // here, saved together with the media row below.
        var additives = await BuildAdditivesAsync(request.Additives, material.SectionId, request.UserId);

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
            LotNumber = await PreparedLotNumber.NextAsync(_db.Media.Select(m => m.LotNumber), lotPrefix, _time.GetUtcNow().UtcDateTime),
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

        foreach (var additive in additives) media.Additives.Add(additive);
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
        if (!await _db.TrySaveChangesAsync(UniqueIndexNames.MediaLotNumber))
        {
            media.LotNumber = await PreparedLotNumber.NextAsync(_db.Media.Select(m => m.LotNumber), lotPrefix, _time.GetUtcNow().UtcDateTime);
            if (!await _db.TrySaveChangesAsync(UniqueIndexNames.MediaLotNumber))
                throw new InvalidOperationException(
                    $"Lot number {media.LotNumber} was taken by another preparation at the same moment. Nothing was saved - submit the preparation again.");
        }

        return media;
    }

    private async Task<List<MediaAdditive>> BuildAdditivesAsync(List<MediaAdditiveInput>? inputs, int sectionId, int userId)
    {
        var result = new List<MediaAdditive>();
        if (inputs is not { Count: > 0 }) return result;

        var seen = new HashSet<(MediaAdditiveKind, int)>();
        var now = _time.GetUtcNow().UtcDateTime;
        var order = 0;

        foreach (var input in inputs)
        {
            order++;
            if (input.Quantity <= 0)
                throw new InvalidOperationException($"Additive {order}: quantity must be greater than zero.");
            var unit = input.Unit?.Trim();
            if (string.IsNullOrEmpty(unit))
                throw new InvalidOperationException($"Additive {order}: unit is required.");
            if (unit.Length > 20)
                throw new InvalidOperationException($"Additive {order}: unit cannot exceed 20 characters.");

            if (input.Kind == MediaAdditiveKind.StockLot)
            {
                if (!input.MaterialId.HasValue)
                    throw new InvalidOperationException($"Additive {order}: choose the stock lot.");
                if (input.SolutionPreparationId.HasValue)
                    throw new InvalidOperationException($"Additive {order}: a stock lot additive cannot also name a prepared solution.");
                if (!seen.Add((input.Kind, input.MaterialId.Value)))
                    throw new InvalidOperationException($"Additive {order}: the same stock lot is listed more than once.");

                var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == input.MaterialId.Value)
                    ?? throw new NotFoundException($"Material {input.MaterialId.Value} not found.");
                if (lot.MaterialType is MaterialType.DehydratedMedia or MaterialType.LyophilizedMicroorganism)
                    throw new InvalidOperationException(
                        $"{lot.MaterialName} (batch {lot.BatchNumber}) is a {lot.MaterialType} lot and cannot be added as an additive.");
                if (lot.SectionId != sectionId)
                    throw new InvalidOperationException(
                        $"{lot.MaterialName} (batch {lot.BatchNumber}) belongs to another laboratory section than the media being prepared.");

                // Same guards as the dehydrated media: expiry, release, COA, enough stock.
                // The quantity is in the lot's own unit, so that unit is what gets recorded.
                var consumed = await _materialService.ConsumeAsync(lot.Id, null, input.Quantity, userId);
                result.Add(new MediaAdditive
                {
                    Order = order,
                    Kind = input.Kind,
                    MaterialId = consumed.Id,
                    Quantity = input.Quantity,
                    Unit = consumed.Unit.ToString(),
                    Name = consumed.MaterialName,
                    Code = Truncate(string.IsNullOrWhiteSpace(consumed.StockId) ? consumed.BatchNumber : consumed.StockId, 100)
                });
            }
            else
            {
                if (!input.SolutionPreparationId.HasValue)
                    throw new InvalidOperationException($"Additive {order}: choose the prepared solution.");
                if (input.MaterialId.HasValue)
                    throw new InvalidOperationException($"Additive {order}: a prepared solution additive cannot also name a stock lot.");
                if (!seen.Add((input.Kind, input.SolutionPreparationId.Value)))
                    throw new InvalidOperationException($"Additive {order}: the same prepared solution is listed more than once.");

                var prep = await _db.SolutionPreparations.Include(p => p.SolutionMaster)
                    .FirstOrDefaultAsync(p => p.Id == input.SolutionPreparationId.Value)
                    ?? throw new NotFoundException($"Solution preparation {input.SolutionPreparationId.Value} not found.");
                var label = prep.Code ?? $"#{prep.Id}";

                if (prep.SectionId != sectionId)
                    throw new InvalidOperationException($"Preparation {label} belongs to another laboratory section than the media being prepared.");
                var status = SolutionPreparationResponse.EffectiveStatusOf(prep, now);
                if (status == SolutionPreparationStatus.Expired)
                    throw new InvalidOperationException($"Preparation {label} expired on {prep.ExpiresAt:yyyy-MM-dd}.");
                if (status != SolutionPreparationStatus.Prepared)
                    throw new InvalidOperationException($"Preparation {label} is {status} and cannot be used.");
                if (prep.ApprovalStatus == ApprovalGateStatus.PendingReview)
                    throw new InvalidOperationException($"Preparation {label} is awaiting release.");
                if (prep.ApprovalStatus == ApprovalGateStatus.Rejected)
                    throw new InvalidOperationException($"Preparation {label} was rejected.");

                // Recorded only - picking the bottle is enough, nothing is deducted.
                result.Add(new MediaAdditive
                {
                    Order = order,
                    Kind = input.Kind,
                    SolutionPreparationId = prep.Id,
                    Quantity = input.Quantity,
                    Unit = unit,
                    Name = prep.SolutionMaster?.Name ?? string.Empty,
                    Code = Truncate(label, 100)
                });
            }
        }

        return result;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    // sectionIds: the caller's laboratory sections (null = unrestricted).
    public async Task<List<MediaResponse>> GetAllAsync(IReadOnlyCollection<int>? sectionIds = null) =>
        (await _db.Media.Include(m => m.Material).Include(m => m.Additives)
            .Where(m => sectionIds == null || sectionIds.Contains(m.Material!.SectionId))
            .OrderByDescending(m => m.Id).ToListAsync()).Select(m => MediaResponse.From(m, _time.GetUtcNow().UtcDateTime)).ToList();

    // includeExpired: the reference-lot lookup for a new GrowthPromotion
    // evaluation (MediaEvaluationController) wants any lot that was ever
    // released, since it's citing a historical count, not asking what can
    // be pulled off the shelf right now - every other caller wants the
    // latter and leaves this false.
    public async Task<List<MediaResponse>> GetReleasedAsync(int? materialId = null, bool includeExpired = false, int? excludeId = null,
        IReadOnlyCollection<int>? sectionIds = null)
    {
        var query = _db.Media.Include(m => m.Material).Include(m => m.Additives).Where(m => m.IsReleasedForUse);
        if (sectionIds != null) query = query.Where(m => sectionIds.Contains(m.Material!.SectionId));
        if (!includeExpired) query = query.Where(m => m.Status == MediaStatus.Active && m.ExpiryDate > _time.GetUtcNow().UtcDateTime);
        if (materialId.HasValue) query = query.Where(m => m.MaterialId == materialId.Value);
        if (excludeId.HasValue) query = query.Where(m => m.Id != excludeId.Value);
        return (await query.OrderByDescending(m => m.Id).ToListAsync()).Select(m => MediaResponse.From(m, _time.GetUtcNow().UtcDateTime)).ToList();
    }

    public async Task MarkOutOfStockAsync(int mediaId, int userId, string? comment = null)
    {
        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new NotFoundException($"Media lot {mediaId} not found.");

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
