using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record PrepareMediaRequest(
    int MediaTypeId, int MaterialId, decimal TotalWeight, string TotalVolume,
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

    public MediaPreparationService(MicroLimsDbContext db, MaterialService materialService)
    {
        _db = db;
        _materialService = materialService;
    }

    public async Task<Media> PrepareAsync(PrepareMediaRequest request)
    {
        var mediaType = await _db.MediaTypes.FirstOrDefaultAsync(m => m.Id == request.MediaTypeId)
            ?? throw new InvalidOperationException($"Media type {request.MediaTypeId} not found.");

        var autoclave = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == request.AutoclaveEquipmentId && e.Type == EquipmentType.Autoclave)
            ?? throw new InvalidOperationException("Selected equipment is not a valid autoclave.");

        // Guards + decrements the Material row in memory - not saved
        // until the SaveChangesAsync below, so a failure here leaves
        // both the stock and the media lot untouched. Reused below for
        // the media identity (Manufacturer fields, lot number prefix)
        // instead of a second lookup.
        var material = await _materialService.ConsumeAsync(request.MaterialId, MaterialType.DehydratedMedia, request.TotalWeight, request.UserId);

        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1);
        var countThisYear = await _db.Media.CountAsync(m =>
            m.MediaTypeId == mediaType.Id && m.PreparedAt >= yearStart && m.PreparedAt < yearEnd);

        var sequence = (countThisYear + 1).ToString("D2");
        var lotPrefix = !string.IsNullOrWhiteSpace(material.Code) ? material.Code : SanitizeForLotPrefix(material.MaterialName);
        var lotNumber = $"{lotPrefix}/{sequence}/{DateTime.UtcNow:yy}";

        var media = new Media
        {
            MediaTypeId = mediaType.Id,
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
        // is derived from the class, one MediaEvaluationChallenge per
        // matching MediaChallengeSpec row for this Material. Zero specs
        // is allowed (no throw): the analyst needs to be able to prepare
        // media before master data is fully configured, but the
        // evaluation obviously can't Conform until specs exist.
        var evaluationType = mediaType.Class switch
        {
            MediaClass.GeneralAgar => EvaluationType.GrowthPromotion,
            MediaClass.SelectiveAgar or MediaClass.SelectiveBroth => EvaluationType.IndicationInhibition,
            MediaClass.GeneralBroth => EvaluationType.EnrichmentCharacteristics,
            _ => throw new InvalidOperationException($"Unhandled media class {mediaType.Class}.")
        };

        var specs = await _db.MediaChallengeSpecs
            .Where(s => s.MaterialName == material.MaterialName && s.EvaluationType == evaluationType)
            .ToListAsync();

        var evaluation = new MediaEvaluation { Media = media, EvaluationType = evaluationType, Status = MediaEvaluationStatus.Assigned };
        foreach (var spec in specs)
        {
            evaluation.Challenges.Add(new MediaEvaluationChallenge
            {
                OrganismId = spec.OrganismId,
                ChallengeRole = spec.ChallengeRole,
                ExpectedDescription = spec.ExpectedDescription,
                InitialInoculum = spec.ChallengeRole == ChallengeRole.Inhibition ? "10^3" : "10^2"
            });
        }
        _db.MediaEvaluations.Add(evaluation);

        await _db.SaveChangesAsync();
        return media;
    }

    public async Task<List<Media>> GetAllAsync() =>
        await _db.Media.Include(m => m.MediaType).OrderByDescending(m => m.Id).ToListAsync();

    public async Task<List<Media>> GetReleasedAsync(int? mediaTypeId = null)
    {
        var query = _db.Media.Include(m => m.MediaType).Include(m => m.Material)
            .Where(m => m.IsReleasedForUse && m.ExpiryDate > DateTime.UtcNow);
        if (mediaTypeId.HasValue) query = query.Where(m => m.MediaTypeId == mediaTypeId.Value);
        return await query.OrderByDescending(m => m.Id).ToListAsync();
    }

    // Material.Code isn't guaranteed present (nullable) - falls back to
    // an alphanumeric, uppercased version of the material's name so the
    // lot number always has a usable prefix.
    private static string SanitizeForLotPrefix(string materialName) =>
        new string(materialName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
