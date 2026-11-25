using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record QualityLinkRequest(QualityLinkTargetType TargetType, int TargetId);
public record CreateQualityRecordRequest(string Title, string Description, int? SectionId, IReadOnlyList<QualityLinkRequest>? Links);
public record QualityLinkDto(int Id, string? Number, string? Label, string? Route, bool IsRestricted);
public record QualityRecordRefDto(int Id, string Number, QualityRecordType Type, string Title);
public record QualityRecordDto(int Id, string Number, QualityRecordType Type, string Title, string Description,
    int? SectionId, int? RaisedByUserId, DateTime RaisedAtUtc, int? OwnerUserId, uint Version)
{
    public IReadOnlyList<QualityAttachmentDto> Attachments { get; init; } = [];

    public static QualityRecordDto From(QualityRecord record) => new(record.Id, record.Number, record.Type,
        record.Title, record.Description, record.SectionId, record.RaisedByUserId, record.RaisedAtUtc,
        record.OwnerUserId, record.Version);
}

public record QualityAttachmentDto(int Id, string FileName, string ContentType, long Size, int UploadedByUserId,
    DateTime UploadedAtUtc, DateTime? VoidedAtUtc, int? VoidedByUserId, string? VoidReason);
public record QualityAttachmentContent(string FileName, string ContentType, byte[] Bytes);
public record AddQualityCommentRequest(string Text);
public record VoidQualityAttachmentRequest(string Reason);
public record QualityCommentDto(int Id, string Text, int AuthorUserId, DateTime CreatedAtUtc);
