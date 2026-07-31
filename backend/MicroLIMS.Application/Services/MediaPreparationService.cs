using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record PrepareMediaRequest(
    int MediaTypeId, int MaterialId, string ManufacturerLot, string ManufacturerName, decimal TotalWeight, string TotalVolume,
    int AutoclaveEquipmentId, string AutoclaveProgram, string LoadType, decimal Temperature,
    int CycleTime, int CycleNumber, decimal Ph, DateTime ExpiryDate, int UserId);

// The Media Preparation module - captures the full prepared-lot record.
// Lot number format: {MediaType.Code}/{seq:D2}/{yy}, sequence resets
// every year. Nothing here is usable in routine testing until it also
// passes GPT (see GptWorkflowEngine).
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
        // both the stock and the media lot untouched.
        await _materialService.ConsumeAsync(request.MaterialId, MaterialType.DehydratedMedia, request.TotalWeight, request.UserId);

        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1);
        var countThisYear = await _db.Media.CountAsync(m =>
            m.MediaTypeId == mediaType.Id && m.PreparedAt >= yearStart && m.PreparedAt < yearEnd);

        var sequence = (countThisYear + 1).ToString("D2");
        var lotNumber = $"{mediaType.Code}/{sequence}/{DateTime.UtcNow:yy}";

        var media = new Media
        {
            MediaTypeId = mediaType.Id,
            LotNumber = lotNumber,
            ManufacturerLot = request.ManufacturerLot,
            ManufacturerName = request.ManufacturerName,
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
            GptStage = GptStage.Preparation
        };

        _db.Media.Add(media);
        await _db.SaveChangesAsync();
        return media;
    }

    public async Task<List<Media>> GetAllAsync() =>
        await _db.Media.Include(m => m.MediaType).OrderByDescending(m => m.Id).ToListAsync();

    public async Task<List<Media>> GetReleasedAsync(int? mediaTypeId = null)
    {
        var query = _db.Media.Include(m => m.MediaType)
            .Where(m => m.GptStage == GptStage.Release && m.Status == MediaStatus.Active && m.ExpiryDate > DateTime.UtcNow);
        if (mediaTypeId.HasValue) query = query.Where(m => m.MediaTypeId == mediaTypeId.Value);
        return await query.OrderByDescending(m => m.Id).ToListAsync();
    }
}
