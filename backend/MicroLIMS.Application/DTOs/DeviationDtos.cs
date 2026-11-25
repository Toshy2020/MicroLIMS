using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public sealed record RaiseDeviationRequest(string Title, string Description, DateTime OccurredAtUtc,
    string? Location, string ImmediateAction, int? SectionId, IReadOnlyList<QualityLinkRequest>? Links);

public sealed record AssessDeviationRequest(bool Accept, int ClassificationId, int OwnerUserId, DateOnly? DueDate,
    string Password);

public sealed record SaveDeviationInvestigationRequest(string ImpactAssessment, int RootCauseCategoryId,
    string RootCauseDescription, string ProposedActions);

public sealed record ApproveDeviationRequest(bool CapaRequired, string? Comment, string Password);
public sealed record DeviationReasonRequest(string Reason, string Password);
public sealed record DeviationPasswordRequest(string Password);
public sealed record ReturnDeviationRequest(string Comment);

public sealed record DeviationDto(QualityRecordDto Record, QualityDeviationStatus Status, DateTime OccurredAtUtc,
    string? Location, string ImmediateAction, int? ClassificationId, DateOnly? DueDate, string? ImpactAssessment,
    int? RootCauseCategoryId, string? RootCauseDescription, string? ProposedActions, bool? CapaRequired,
    string? RejectionReason, string? CancelReason)
{
    public int Id => Record.Id;
    public uint Version => Record.Version;

    internal static DeviationDto From(QualityRecord record, DeviationDetail detail) =>
        new(QualityRecordDto.From(record), detail.Status, detail.OccurredAtUtc, detail.Location, detail.ImmediateAction,
            record.ClassificationId, record.DueDate, detail.ImpactAssessment, detail.RootCauseCategoryId,
            detail.RootCauseDescription, detail.ProposedActions, detail.CapaRequired, detail.RejectionReason,
            record.CancelReason);
}
