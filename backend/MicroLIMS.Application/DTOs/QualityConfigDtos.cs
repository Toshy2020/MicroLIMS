using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public sealed record QualityChecklistItemDto(string Key, int Order, string Text, bool IsActive);
public sealed record SaveQualityTemplateRequest(IReadOnlyList<QualityChecklistItemDto> Items);
public sealed record QualityChecklistTemplateDto(int Id, QualityRecordType Type, int? SectionId, uint Version,
    IReadOnlyList<QualityChecklistItemDto> Items);
public sealed record SaveQualityClassificationRequest(string Name, int Order, bool IsActive);
public sealed record QualityClassificationDto(int Id, string Name, int Order, bool IsActive, uint Version);
public sealed record SaveQualityDueRuleRequest(int? ClassificationId, int? WorkingDays);
public sealed record QualityDueRuleDto(int Id, QualityRecordType Type, int? ClassificationId, int? WorkingDays, uint Version);
public sealed record SaveQualityAnswerRequest(QualityChecklistAnswerValue Answer, string? Comment);
public sealed record QualityAnswerDto(int Id, string ItemKey, string ItemText, QualityChecklistAnswerValue Answer,
    string? Comment, int AnsweredByUserId, DateTime AnsweredAtUtc, uint RecordVersion);
public sealed record RequestQualityExtensionRequest(int? ActionId, DateOnly RequestedDueDate, string Reason);
public sealed record DecideQualityExtensionRequest(bool Approve, string? Comment, string Password);
public sealed record QualityExtensionDto(int Id, int RecordId, int? ActionId, DateOnly RequestedDueDate, string Reason,
    int RequestedByUserId, DateTime RequestedAtUtc, QualityExtensionDecision? Decision, int? DecidedByUserId,
    DateTime? DecidedAtUtc, string? DecisionComment, int? SignatureId, uint Version, uint RecordVersion,
    string RequestedByName, string? DecidedByName);
public sealed record OverdueQualityRecordDto(int RecordId, string Number, string Title, DateOnly DueDate, int? OwnerUserId);
public sealed record OverdueQualityActionDto(int RecordId, string Number, int ActionId, string Description,
    DateOnly DueDate, int ResponsibleUserId);
public sealed record QualityOverdueDto(IReadOnlyList<OverdueQualityRecordDto> Records, IReadOnlyList<OverdueQualityActionDto> Actions);
public sealed record QualitySectionSettingDto(int SectionId, bool ChangeControlRequired, uint Version);
public sealed record UpdateQualitySectionSettingRequest(bool ChangeControlRequired, string Reason, string Password);
