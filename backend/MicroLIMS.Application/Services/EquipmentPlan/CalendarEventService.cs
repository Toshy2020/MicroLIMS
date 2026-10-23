using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.EquipmentPlan;

public record DocumentSummary(
    int Id,
    string OriginalFileName,
    string FileExtension,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    int UploadedByUserId,
    string UploadedByName,
    MaterialDocumentStatus Status);

public record CalendarEventResponse(
    int Id,
    CalendarEventType Type,
    string Title,
    DateOnly? Date,
    string? Month,
    string? Note,
    int? EquipmentInventoryId,
    string? EquipmentCode,
    CalendarEventStatus Status,
    bool ReportPending,
    IReadOnlyList<DocumentSummary> Documents,
    uint Version);

public record CreateCalendarEventRequest(
    CalendarEventType Type,
    string Title,
    DateOnly? Date,
    string? Month,
    string? Note,
    int? EquipmentInventoryId,
    bool ConfirmDuplicate = false);

public record UpdateCalendarEventRequest(
    CalendarEventType Type,
    string Title,
    DateOnly? Date,
    string? Month,
    string? Note,
    int? EquipmentInventoryId,
    bool ConfirmDuplicate = false);

public record CancelCalendarEventRequest(string Reason);

public record UploadCalendarEventDocumentRequest(
    string OriginalFileName,
    string DeclaredContentType,
    byte[] Content);

public class CalendarEventService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly MaterialDocumentFileValidator _validator;
    private readonly ILogger<CalendarEventService> _logger;
    private readonly TimeProvider _time;

    public CalendarEventService(
        IMicroLimsDbContext db,
        IFileStorageService storage,
        MaterialDocumentFileValidator? validator = null,
        ILogger<CalendarEventService>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _storage = storage;
        _validator = validator ?? new MaterialDocumentFileValidator(30 * 1024 * 1024L);
        _logger = logger ?? NullLogger<CalendarEventService>.Instance;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<CalendarEventResponse> CreateAsync(CreateCalendarEventRequest request, int userId)
    {
        ValidateEventFields(request.Title, request.Date, request.Month);

        if (!request.ConfirmDuplicate)
        {
            await CheckDuplicateEventAsync(
                excludeId: null,
                request.Type,
                request.Date,
                !string.IsNullOrWhiteSpace(request.Month) ? request.Month.Trim() : null,
                request.EquipmentInventoryId,
                request.Title);
        }

        if (request.EquipmentInventoryId.HasValue)
        {
            var exists = await _db.EquipmentInventories.AnyAsync(e => e.Id == request.EquipmentInventoryId.Value);
            if (!exists)
                throw new NotFoundException($"Equipment {request.EquipmentInventoryId.Value} not found.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entity = new CalendarEvent
        {
            EventType = request.Type,
            Title = request.Title.Trim(),
            Date = request.Date,
            Month = !string.IsNullOrWhiteSpace(request.Month) ? request.Month.Trim() : null,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            EquipmentInventoryId = request.EquipmentInventoryId,
            Status = CalendarEventStatus.Active,
            CreatedByUserId = userId,
            CreatedAt = now,
            LastModifiedByUserId = userId,
            LastModifiedAt = now
        };

        _db.CalendarEvents.Add(entity);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(entity.Id);
    }

    public async Task<CalendarEventResponse> UpdateAsync(int id, UpdateCalendarEventRequest request, int userId, uint? expectedVersion = null)
    {
        var entity = await _db.CalendarEvents
            .Include(e => e.EquipmentInventory)
            .Include(e => e.Documents)
                .ThenInclude(d => d.UploadedByUser)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"Calendar event {id} not found.");

        if (entity.Status == CalendarEventStatus.Cancelled)
            throw new BusinessRuleException("A cancelled event cannot be edited.");

        if (expectedVersion.HasValue)
            _db.ExpectedVersion = expectedVersion.Value;

        RecordVersion.EnsureCurrent(_db, entity);

        ValidateEventFields(request.Title, request.Date, request.Month);

        if (!request.ConfirmDuplicate)
        {
            await CheckDuplicateEventAsync(
                excludeId: id,
                request.Type,
                request.Date,
                !string.IsNullOrWhiteSpace(request.Month) ? request.Month.Trim() : null,
                request.EquipmentInventoryId,
                request.Title);
        }

        if (request.EquipmentInventoryId.HasValue && request.EquipmentInventoryId != entity.EquipmentInventoryId)
        {
            var exists = await _db.EquipmentInventories.AnyAsync(e => e.Id == request.EquipmentInventoryId.Value);
            if (!exists)
                throw new NotFoundException($"Equipment {request.EquipmentInventoryId.Value} not found.");
        }

        entity.EventType = request.Type;
        entity.Title = request.Title.Trim();
        entity.Date = request.Date;
        entity.Month = !string.IsNullOrWhiteSpace(request.Month) ? request.Month.Trim() : null;
        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        entity.EquipmentInventoryId = request.EquipmentInventoryId;
        entity.LastModifiedByUserId = userId;
        entity.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(entity.Id);
    }

    public async Task<CalendarEventResponse> CancelAsync(int id, string reason, int userId, uint? expectedVersion = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("A reason is required to cancel an event.");

        var entity = await _db.CalendarEvents
            .Include(e => e.EquipmentInventory)
            .Include(e => e.Documents)
                .ThenInclude(d => d.UploadedByUser)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"Calendar event {id} not found.");

        if (entity.Status == CalendarEventStatus.Cancelled)
            throw new BusinessRuleException("Event is already cancelled.");

        if (expectedVersion.HasValue)
            _db.ExpectedVersion = expectedVersion.Value;

        RecordVersion.EnsureCurrent(_db, entity);

        entity.Status = CalendarEventStatus.Cancelled;
        entity.CancelReason = reason.Trim();
        entity.LastModifiedByUserId = userId;
        entity.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        return ToResponse(entity);
    }

    public async Task<CalendarEventResponse> GetByIdAsync(int id)
    {
        var entity = await _db.CalendarEvents
            .Include(e => e.EquipmentInventory)
            .Include(e => e.Documents)
                .ThenInclude(d => d.UploadedByUser)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"Calendar event {id} not found.");

        return ToResponse(entity);
    }

    public async Task<List<CalendarEventResponse>> GetCalendarEventsAsync(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new BusinessRuleException("The end of the range is before its start.");

        var fromMonth = from.ToString("yyyy-MM");
        var toMonth = to.ToString("yyyy-MM");

        var events = await _db.CalendarEvents
            .Include(e => e.EquipmentInventory)
            .Include(e => e.Documents)
                .ThenInclude(d => d.UploadedByUser)
            .Where(e => (e.Date != null && e.Date >= from && e.Date <= to)
                     || (e.Month != null && string.Compare(e.Month, fromMonth) >= 0 && string.Compare(e.Month, toMonth) <= 0))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Month)
            .ThenBy(e => e.Id)
            .ToListAsync();

        return events.Select(ToResponse).ToList();
    }

    public Task<DocumentSummary> UploadDocumentAsync(int eventId, UploadCalendarEventDocumentRequest request, int userId) =>
        UnitOfWork.RunAsync(_db, () => UploadDocumentCoreAsync(eventId, request, userId));

    private async Task<DocumentSummary> UploadDocumentCoreAsync(int eventId, UploadCalendarEventDocumentRequest request, int userId)
    {
        var ev = await _db.CalendarEvents.FindAsync(eventId)
            ?? throw new NotFoundException($"Calendar event {eventId} not found.");

        if (ev.Status == CalendarEventStatus.Cancelled)
            throw new BusinessRuleException("Cannot attach documents to a cancelled event.");

        var firstBytes = request.Content.Take(16).ToArray();
        var error = _validator.Validate(request.OriginalFileName, request.DeclaredContentType, request.Content.Length, firstBytes);
        if (error != null)
            throw new InvalidOperationException(error);

        var ext = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
        var sha256 = Convert.ToHexString(SHA256.HashData(request.Content));
        var now = _time.GetUtcNow().UtcDateTime;

        var doc = new CalendarEventDocument
        {
            CalendarEventId = eventId,
            OriginalFileName = SanitiseFileName(request.OriginalFileName),
            StorageKey = "pending",
            FileExtension = ext,
            ContentType = NormaliseMime(request.DeclaredContentType),
            FileSizeBytes = request.Content.Length,
            ContentSha256 = sha256,
            UploadedByUserId = userId,
            UploadedAt = now,
            Status = MaterialDocumentStatus.Current
        };

        _db.CalendarEventDocuments.Add(doc);
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync();

        var storageKey = $"calendar-event-documents/{eventId}/{doc.Id}{ext}";
        try
        {
            var savedPath = await _storage.SaveAsync(storageKey, request.Content);
            doc.StorageKey = savedPath;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File storage failed for CalendarEventDocument {Id} (event {EventId}). Rolling back DB record.", doc.Id, eventId);
            _db.CalendarEventDocuments.Remove(doc);
            try { await _db.SaveChangesAsync(); } catch { /* best-effort */ }
            throw new InvalidOperationException("Document storage failed. The upload was not completed.", ex);
        }

        var userName = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? "Unknown";

        return new DocumentSummary(
            doc.Id,
            doc.OriginalFileName,
            doc.FileExtension,
            doc.ContentType,
            doc.FileSizeBytes,
            doc.UploadedAt,
            doc.UploadedByUserId,
            userName,
            doc.Status);
    }

    public async Task<(DocumentSummary Metadata, byte[] Content)> GetDocumentContentAsync(int documentId, int requestingUserId = 0)
    {
        var doc = await _db.CalendarEventDocuments
            .Include(d => d.UploadedByUser)
            .FirstOrDefaultAsync(d => d.Id == documentId)
            ?? throw new NotFoundException($"Calendar event document {documentId} not found.");

        byte[] content;
        try
        {
            content = await _storage.ReadAsync(doc.StorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File read failed for CalendarEventDocument {Id} (key: {Key}).", doc.Id, doc.StorageKey);
            throw new InvalidOperationException("The document file could not be retrieved.");
        }

        var computedHash = Convert.ToHexString(SHA256.HashData(content));
        if (!string.Equals(computedHash, doc.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "INTEGRITY FAILURE: CalendarEventDocument {Id} stored hash {Stored} does not match computed hash {Computed}.",
                doc.Id, doc.ContentSha256, computedHash);
            throw new InvalidOperationException("Document integrity check failed. The file may have been altered. Please contact your System Administrator.");
        }

        var meta = new DocumentSummary(
            doc.Id,
            doc.OriginalFileName,
            doc.FileExtension,
            doc.ContentType,
            doc.FileSizeBytes,
            doc.UploadedAt,
            doc.UploadedByUserId,
            doc.UploadedByUser?.FullName ?? "Unknown",
            doc.Status);

        return (meta, content);
    }

    private static void ValidateEventFields(string title, DateOnly? date, string? month)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessRuleException("Title is required.");

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > 200)
            throw new BusinessRuleException("Title cannot exceed 200 characters.");

        var hasDate = date.HasValue;
        var hasMonth = !string.IsNullOrWhiteSpace(month);

        if ((!hasDate && !hasMonth) || (hasDate && hasMonth))
            throw new BusinessRuleException("Exactly one of date or month must be specified.");

        if (hasMonth)
        {
            var trimmedMonth = month!.Trim();
            if (!DateTime.TryParseExact(trimmedMonth, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new BusinessRuleException("Month must be in 'yyyy-MM' format.");
        }
    }

    private async Task CheckDuplicateEventAsync(
        int? excludeId,
        CalendarEventType type,
        DateOnly? date,
        string? month,
        int? equipmentInventoryId,
        string title)
    {
        var trimmedTitle = title.Trim();

        var query = _db.CalendarEvents
            .AsNoTracking()
            .Where(e => e.Status == CalendarEventStatus.Active &&
                        e.EventType == type &&
                        e.EquipmentInventoryId == equipmentInventoryId);

        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
        }

        if (date.HasValue)
        {
            query = query.Where(e => e.Date == date.Value);
        }
        else if (!string.IsNullOrWhiteSpace(month))
        {
            query = query.Where(e => e.Month == month);
        }

        var existingTitles = await query.Select(e => e.Title).ToListAsync();

        if (existingTitles.Any(t => string.Equals(t.Trim(), trimmedTitle, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleException("A matching event already exists.");
        }
    }

    private static CalendarEventResponse ToResponse(CalendarEvent e)
    {
        var reportPending = e.EventType == CalendarEventType.ProficiencyTesting
            && !e.Documents.Any(d => d.Status == MaterialDocumentStatus.Current);

        var docs = e.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentSummary(
                d.Id,
                d.OriginalFileName,
                d.FileExtension,
                d.ContentType,
                d.FileSizeBytes,
                d.UploadedAt,
                d.UploadedByUserId,
                d.UploadedByUser?.FullName ?? "Unknown",
                d.Status))
            .ToList();

        return new CalendarEventResponse(
            e.Id,
            e.EventType,
            e.Title,
            e.Date,
            e.Month,
            e.Note,
            e.EquipmentInventoryId,
            e.EquipmentInventory?.Code,
            e.Status,
            reportPending,
            docs,
            e.Version);
    }

    private static string SanitiseFileName(string name) =>
        Path.GetFileName(name).Trim();

    private static string NormaliseMime(string mime) =>
        mime.Split(';')[0].Trim().ToLowerInvariant();
}
