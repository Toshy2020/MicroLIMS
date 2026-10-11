using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

public class MediaStorageService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ReviewGateService _reviewGate;
    private readonly TimeProvider _time;

    public MediaStorageService(
        IMicroLimsDbContext db,
        ReviewGateService reviewGate,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _reviewGate = reviewGate;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<List<MediaStorageEntryResponse>> GetStorageEntriesAsync(int mediaId)
    {
        var mediaExists = await _db.Media.AnyAsync(m => m.Id == mediaId);
        if (!mediaExists)
            throw new NotFoundException($"Media lot {mediaId} not found.");

        var entries = await _db.MediaStorageEntries
            .Include(e => e.Equipment)
            .Where(e => e.MediaId == mediaId)
            .OrderBy(e => e.Id)
            .ToListAsync();

        return entries.Select(MediaStorageEntryResponse.From).ToList();
    }

    public Task<MediaStorageEntryResponse> AddStorageEntryAsync(int mediaId, int userId, AddMediaStorageEntryRequest request) =>
        UnitOfWork.RunAsync(_db, () => AddStorageEntryCoreAsync(mediaId, userId, request));

    private async Task<MediaStorageEntryResponse> AddStorageEntryCoreAsync(int mediaId, int userId, AddMediaStorageEntryRequest request)
    {
        if (request == null)
            throw new InvalidOperationException("Request payload is required.");

        if (!Enum.TryParse<MediaStorageForm>(request.Form, true, out var form))
            throw new InvalidOperationException($"Invalid media storage form '{request.Form}'. Valid values are Plates, AgarBottles, BrothBottles, Tubes.");

        if (request.Quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero.");

        var hasEquipment = request.EquipmentId.HasValue;
        var hasLocationText = !string.IsNullOrWhiteSpace(request.LocationText);
        if ((hasEquipment && hasLocationText) || (!hasEquipment && !hasLocationText))
            throw new InvalidOperationException("Exactly one of EquipmentId or LocationText must be provided.");

        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new NotFoundException($"Media lot {mediaId} not found.");

        if (!media.IsReleasedForUse || media.Status != MediaStatus.Active)
            throw new InvalidOperationException($"Media lot {media.LotNumber} is not currently released for use and active.");

        EquipmentInventory? equipment = null;
        string? normalizedLocationText = null;
        string locationDisplay;

        if (hasEquipment)
        {
            equipment = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == request.EquipmentId!.Value)
                ?? throw new NotFoundException($"Equipment {request.EquipmentId!.Value} not found.");

            if (equipment.Status != EquipmentOperationalStatus.InService)
                throw new InvalidOperationException($"Equipment {equipment.Code} is not currently in service.");

            locationDisplay = $"{equipment.InstrumentType}, {equipment.ManufacturerName} ({equipment.Code})";
        }
        else
        {
            normalizedLocationText = request.LocationText!.Trim();
            locationDisplay = normalizedLocationText;
        }

        var now = _time.GetUtcNow().UtcDateTime;

        var existingEntry = await _db.MediaStorageEntries
            .Include(e => e.Equipment)
            .FirstOrDefaultAsync(e => e.MediaId == mediaId &&
                                      e.Form == form &&
                                      e.EquipmentId == request.EquipmentId &&
                                      e.LocationText == normalizedLocationText);

        MediaStorageEntry entryToReturn;
        if (existingEntry != null)
        {
            existingEntry.Quantity += request.Quantity;
            existingEntry.UpdatedAt = now;
            entryToReturn = existingEntry;
        }
        else
        {
            var newEntry = new MediaStorageEntry
            {
                MediaId = mediaId,
                Form = form,
                Quantity = request.Quantity,
                EquipmentId = request.EquipmentId,
                Equipment = equipment,
                LocationText = normalizedLocationText,
                CreatedAt = now,
                CreatedByUserId = userId,
                UpdatedAt = now
            };
            _db.MediaStorageEntries.Add(newEntry);
            entryToReturn = newEntry;
        }

        var comment = $"Added {request.Quantity} {form} to storage: {locationDisplay}.";
        await _reviewGate.LogEventAsync(
            ReviewEntityTypes.Media,
            mediaId,
            userId,
            ReviewWorkflowEventType.StorageLocationChanged,
            comment);

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        return MediaStorageEntryResponse.From(entryToReturn);
    }

    public Task<MediaStorageEntryResponse> MoveStorageEntryAsync(int mediaId, int entryId, int userId, MoveMediaStorageEntryRequest request) =>
        UnitOfWork.RunAsync(_db, () => MoveStorageEntryCoreAsync(mediaId, entryId, userId, request));

    private async Task<MediaStorageEntryResponse> MoveStorageEntryCoreAsync(int mediaId, int entryId, int userId, MoveMediaStorageEntryRequest request)
    {
        if (request == null)
            throw new InvalidOperationException("Request payload is required.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A reason is required to move media storage.");

        if (request.Quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero.");

        var hasEquipment = request.EquipmentId.HasValue;
        var hasLocationText = !string.IsNullOrWhiteSpace(request.LocationText);
        if ((hasEquipment && hasLocationText) || (!hasEquipment && !hasLocationText))
            throw new InvalidOperationException("Exactly one of EquipmentId or LocationText must be provided.");

        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == mediaId)
            ?? throw new NotFoundException($"Media lot {mediaId} not found.");

        if (!media.IsReleasedForUse || media.Status != MediaStatus.Active)
            throw new InvalidOperationException($"Media lot {media.LotNumber} is not currently released for use and active.");

        var sourceEntry = await _db.MediaStorageEntries
            .Include(e => e.Equipment)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MediaId == mediaId)
            ?? throw new NotFoundException($"Media storage entry {entryId} not found for media lot {mediaId}.");

        if (request.Quantity > sourceEntry.Quantity)
            throw new InvalidOperationException($"Cannot move {request.Quantity} units; only {sourceEntry.Quantity} available in entry {entryId}.");

        EquipmentInventory? targetEquipment = null;
        string? normalizedTargetLocationText = null;
        string toDisplay;

        if (hasEquipment)
        {
            targetEquipment = await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == request.EquipmentId!.Value)
                ?? throw new NotFoundException($"Equipment {request.EquipmentId!.Value} not found.");

            if (targetEquipment.Status != EquipmentOperationalStatus.InService)
                throw new InvalidOperationException($"Equipment {targetEquipment.Code} is not currently in service.");

            toDisplay = $"{targetEquipment.InstrumentType}, {targetEquipment.ManufacturerName} ({targetEquipment.Code})";
        }
        else
        {
            normalizedTargetLocationText = request.LocationText!.Trim();
            toDisplay = normalizedTargetLocationText;
        }

        string fromDisplay = sourceEntry.Equipment != null
            ? $"{sourceEntry.Equipment.InstrumentType}, {sourceEntry.Equipment.ManufacturerName} ({sourceEntry.Equipment.Code})"
            : sourceEntry.LocationText ?? "Unknown";

        if (sourceEntry.EquipmentId == request.EquipmentId && sourceEntry.LocationText == normalizedTargetLocationText)
            throw new InvalidOperationException("Destination storage location must be different from the source storage location.");

        var now = _time.GetUtcNow().UtcDateTime;

        var targetEntry = await _db.MediaStorageEntries
            .Include(e => e.Equipment)
            .FirstOrDefaultAsync(e => e.MediaId == mediaId &&
                                      e.Form == sourceEntry.Form &&
                                      e.EquipmentId == request.EquipmentId &&
                                      e.LocationText == normalizedTargetLocationText &&
                                      e.Id != sourceEntry.Id);

        MediaStorageEntry entryToReturn;
        if (targetEntry != null)
        {
            targetEntry.Quantity += request.Quantity;
            targetEntry.UpdatedAt = now;

            if (request.Quantity == sourceEntry.Quantity)
            {
                _db.MediaStorageEntries.Remove(sourceEntry);
            }
            else
            {
                sourceEntry.Quantity -= request.Quantity;
                sourceEntry.UpdatedAt = now;
            }

            entryToReturn = targetEntry;
        }
        else
        {
            if (request.Quantity == sourceEntry.Quantity)
            {
                sourceEntry.EquipmentId = request.EquipmentId;
                sourceEntry.Equipment = targetEquipment;
                sourceEntry.LocationText = normalizedTargetLocationText;
                sourceEntry.UpdatedAt = now;
                entryToReturn = sourceEntry;
            }
            else
            {
                sourceEntry.Quantity -= request.Quantity;
                sourceEntry.UpdatedAt = now;

                var newEntry = new MediaStorageEntry
                {
                    MediaId = mediaId,
                    Form = sourceEntry.Form,
                    Quantity = request.Quantity,
                    EquipmentId = request.EquipmentId,
                    Equipment = targetEquipment,
                    LocationText = normalizedTargetLocationText,
                    CreatedAt = now,
                    CreatedByUserId = userId,
                    UpdatedAt = now
                };
                _db.MediaStorageEntries.Add(newEntry);
                entryToReturn = newEntry;
            }
        }

        var comment = $"Moved {request.Quantity} {sourceEntry.Form} from \"{fromDisplay}\" to \"{toDisplay}\". Reason: {request.Reason.Trim()}";
        await _reviewGate.LogEventAsync(
            ReviewEntityTypes.Media,
            mediaId,
            userId,
            ReviewWorkflowEventType.StorageLocationChanged,
            comment);

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        return MediaStorageEntryResponse.From(entryToReturn);
    }
}
