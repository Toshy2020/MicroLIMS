using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record ItemDocumentDto(
    int Id,
    int ItemId,
    ItemDocumentType DocumentType,
    string OriginalFileName,
    string Version,
    DateTime? EffectiveDate,
    long FileSizeBytes,
    int UploadedByUserId,
    string UploadedByUserName,
    DateTime UploadedAt,
    MaterialDocumentStatus Status,
    int? SupersededByDocumentId,
    DateTime? SupersededAt
);

public record UploadItemDocumentRequest(
    ItemDocumentType DocumentType,
    string Version,
    DateTime? EffectiveDate
);
