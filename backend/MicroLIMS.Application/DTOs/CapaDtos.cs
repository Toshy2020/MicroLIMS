using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public sealed record CreateCapaRequest(string Title, string Description, int? SectionId, int? SourceRecordId,
    IReadOnlyList<QualityLinkRequest>? Links, int OwnerUserId);

public sealed record CapaActionInput(QualityActionKind Kind, string Description, int ResponsibleUserId, DateOnly DueDate);

public sealed record SaveCapaPlanRequest(IReadOnlyList<CapaActionInput> Actions, string EffectivenessCriteria,
    string EffectivenessMethod, DateOnly EffectivenessPlannedDate);

public sealed record CompleteCapaActionRequest(string Evidence, int? EvidenceLinkId);

public sealed record SignCapaEffectivenessRequest(CapaEffectiveness Result, string Findings, int? NewCapaId, string Password);

public sealed record CapaPasswordRequest(string Password);
public sealed record ReturnCapaPlanRequest(string Comment);
public sealed record CapaReasonRequest(string Reason, string Password);

public sealed record CapaActionDto(int Id, QualityActionKind Kind, string Description, int ResponsibleUserId,
    DateOnly DueDate, DateTime? DoneAtUtc, int? DoneByUserId, string? Evidence, int? EvidenceLinkId, uint Version)
{
    internal static CapaActionDto From(QualityAction action) => new(action.Id, action.Kind, action.Description,
        action.ResponsibleUserId, action.DueDate, action.DoneAtUtc, action.DoneByUserId, action.Evidence,
        action.EvidenceLinkId, action.Version);
}

public sealed record CapaDto(QualityRecordDto Record, CapaStatus Status, int? SourceRecordId,
    string? EffectivenessCriteria, string? EffectivenessMethod, DateOnly? EffectivenessPlannedDate,
    CapaEffectiveness? EffectivenessResult, string? EffectivenessFindings, int? SuccessorCapaId,
    DateOnly? DueDate, string? CancelReason, IReadOnlyList<CapaActionDto> Actions)
{
    public int Id => Record.Id;
    public uint Version => Record.Version;

    internal static CapaDto From(QualityRecord record, CapaDetail detail, IReadOnlyList<QualityAction> actions) =>
        new(QualityRecordDto.From(record), detail.Status, detail.SourceRecordId, detail.EffectivenessCriteria,
            detail.EffectivenessMethod, detail.EffectivenessPlannedDate, detail.EffectivenessResult,
            detail.EffectivenessFindings, detail.SuccessorCapaId, record.DueDate, record.CancelReason,
            actions.OrderBy(a => a.Id).Select(CapaActionDto.From).ToList());
}
