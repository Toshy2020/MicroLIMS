using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public class QualityAttachmentService(IMicroLimsDbContext db, QualityRecordCore core, IFileStorageService storage,
    IAuditEventService audit, TimeProvider clock)
{
    public Task<QualityAttachmentDto> UploadAsync(int recordId, uint version, string fileName, byte[] bytes,
        int userId, CancellationToken ct = default) => core.MutateAsync(recordId, version, userId, false, async _ =>
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        if (name.Length > 255 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            DiscussionAttachmentPolicy.Validate([(name, bytes)]) is not null)
            throw InvalidAttachment();

        var attachment = new QualityAttachment
        {
            RecordId = recordId, OriginalFileName = name, ContentType = DiscussionAttachmentPolicy.ContentTypeFor(name),
            FileExtension = Path.GetExtension(name).ToLowerInvariant(), FileSizeBytes = bytes.LongLength,
            ContentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            UploadedByUserId = userId, UploadedAtUtc = clock.GetUtcNow().UtcDateTime,
            StorageKey = await storage.SaveAsync($"quality/{recordId}/{Guid.NewGuid():N}_{name}", bytes)
        };
        db.QualityAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        return ToDto(attachment);
    }, ct);

    public async Task<QualityAttachmentContent> GetContentAsync(int recordId, int attachmentId, int userId,
        CancellationToken ct = default)
    {
        var records = await core.ReadableRecordsAsync(userId, ct);
        var attachment = await db.QualityAttachments.AsNoTracking()
            .Where(a => a.Id == attachmentId && a.RecordId == recordId && a.VoidedAtUtc == null &&
                records.Any(r => r.Id == a.RecordId))
            .Select(a => new { a.OriginalFileName, a.ContentType, a.StorageKey, a.ContentSha256 }).SingleOrDefaultAsync(ct)
            ?? throw QualityRecordCore.Missing();
        byte[] bytes;
        try
        {
            bytes = await storage.ReadAsync(attachment.StorageKey);
        }
        catch (StoredFileNotFoundException)
        {
            throw QualityRecordCore.Missing();
        }
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), attachment.ContentSha256, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException(MessageCodes.DocumentFileIntegrityDeliveryBlocked,
                "Document integrity check failed. The file may have been altered or corrupted. Delivery has been blocked.");
        await audit.RecordUserEventAsync("QualityAttachmentDownloaded", AuditActionCategory.Document,
            nameof(QualityAttachment), entityId: attachmentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken: ct);
        return new(attachment.OriginalFileName, attachment.ContentType, bytes);
    }

    public Task<QualityAttachmentDto> VoidAsync(int recordId, int attachmentId, uint version, string reason,
        int userId, CancellationToken ct = default) => core.MutateAsync(recordId, version, userId, true, async _ =>
    {
        var attachment = await db.QualityAttachments.SingleOrDefaultAsync(a => a.Id == attachmentId &&
            a.RecordId == recordId && a.VoidedAtUtc == null, ct) ?? throw QualityRecordCore.Missing();
        attachment.VoidReason = QualityRecordCore.RequiredText(reason, 4000);
        attachment.VoidedAtUtc = clock.GetUtcNow().UtcDateTime;
        attachment.VoidedByUserId = userId;
        db.SetNextAuditReason(attachment, attachment.VoidReason);
        await db.SaveChangesAsync(ct);
        return ToDto(attachment);
    }, ct);

    public static BusinessRuleException InvalidAttachment() => new(MessageCodes.QualityInvalidAttachment,
        "Choose an allowed attachment with valid file content and a size between 1 byte and 10 MB.");

    private static QualityAttachmentDto ToDto(QualityAttachment a) => new(a.Id, a.OriginalFileName, a.ContentType,
        a.FileSizeBytes, a.UploadedByUserId, a.UploadedAtUtc, a.VoidedAtUtc, a.VoidedByUserId, a.VoidReason);
}
