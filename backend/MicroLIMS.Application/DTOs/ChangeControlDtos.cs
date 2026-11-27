using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public sealed record ChangeControlAffectedInput(QualityLinkTargetType TargetType, int? TargetId, string? NewRecordCode);

public sealed record ProposeChangeControlRequest(string Title, string Description, string Reason, int? SectionId,
    IReadOnlyList<ChangeControlAffectedInput>? Affected, int? OwnerUserId = null);

public sealed record ChangeControlTaskInput(string Description, int ResponsibleUserId, DateOnly DueDate);

public sealed record ChangeControlChecklistAnswerInput(string ItemKey, QualityChecklistAnswerValue Answer, string? Comment);

public sealed record SaveChangeControlAssessmentRequest(string ImpactSummary, IReadOnlyList<ChangeControlTaskInput> Tasks,
    IReadOnlyList<ChangeControlChecklistAnswerInput>? Answers = null);

public sealed record ChangeControlPasswordRequest(string Password);

public sealed record ChangeControlRejectRequest(string Reason, string Password);

public sealed record CompleteChangeControlTaskRequest(string Evidence, int? EvidenceLinkId = null);

public sealed record CloseChangeControlRequest(string PostChangeReview, string? UnchangedJustification, string Password);

public sealed record ChangeControlAffectedDto(int Id, QualityLinkTargetType TargetType, int? TargetId, string? NewRecordCode,
    DateTime? ChangedAtUtc, int? ChangeAuditLogId)
{
    internal static ChangeControlAffectedDto From(QualityRecordLink link) => new(link.Id, link.TargetType, link.TargetId,
        link.NewRecordCode, link.ChangedAtUtc, link.ChangeAuditLogId);
}

public sealed record ChangeControlTaskDto(int Id, string Description, int ResponsibleUserId, DateOnly DueDate,
    DateTime? DoneAtUtc, int? DoneByUserId, string? Evidence, int? EvidenceLinkId, uint Version)
{
    internal static ChangeControlTaskDto From(QualityAction action) => new(action.Id, action.Description,
        action.ResponsibleUserId, action.DueDate, action.DoneAtUtc, action.DoneByUserId, action.Evidence,
        action.EvidenceLinkId, action.Version);
}

public sealed record ChangeControlDto(QualityRecordDto Record, ChangeControlStatus Status, string Reason,
    string? ImpactSummary, string? RejectionReason, string? PostChangeReview, string? UnchangedJustification,
    DateOnly? DueDate, string? CancelReason, IReadOnlyList<ChangeControlAffectedDto> Affected,
    IReadOnlyList<ChangeControlTaskDto> Tasks)
{
    public int Id => Record.Id;
    public uint Version => Record.Version;

    internal static ChangeControlDto From(QualityRecord record, ChangeControlDetail detail,
        IReadOnlyList<QualityRecordLink> affectedLinks, IReadOnlyList<QualityAction> tasks) =>
        new(QualityRecordDto.From(record), detail.Status, detail.Reason, detail.ImpactSummary, detail.RejectionReason,
            detail.PostChangeReview, detail.UnchangedJustification, record.DueDate, record.CancelReason,
            affectedLinks.OrderBy(l => l.Id).Select(ChangeControlAffectedDto.From).ToList(),
            tasks.OrderBy(a => a.Id).Select(ChangeControlTaskDto.From).ToList());
}
