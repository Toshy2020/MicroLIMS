using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.EquipmentPlan;

public record GenerateEquipmentDraftRequest(int Year, DateOnly? GoLiveDate = null);

public record EquipmentPlanRevisionResponse(
    int Id,
    uint Version,
    int Year,
    int RevisionNumber,
    PlanRevisionStatus Status,
    string? Reason,
    DateOnly EffectiveFrom,
    DateTime GeneratedAt,
    int GeneratedByUserId,
    DateTime? ApprovedAt,
    int TaskCount)
{
    public static EquipmentPlanRevisionResponse From(EquipmentPlanRevision r, int taskCount) => new(
        r.Id,
        r.Version,
        r.Year,
        r.RevisionNumber,
        r.Status,
        r.Reason,
        r.EffectiveFrom,
        r.GeneratedAt,
        r.GeneratedByUserId,
        r.ApprovedAt,
        taskCount);
}

public record EquipmentPlanTypeTotal(EquipmentTaskType TaskType, int Instruments, int Tasks);

public record EquipmentTaskRetirementResponse(
    int TaskId,
    int EquipmentInventoryId,
    string EquipmentCode,
    EquipmentTaskType TaskType,
    int Sequence,
    DateOnly DueDate)
{
    public int OccurrenceId => TaskId;
    public string Target => EquipmentCode;
}

public record EquipmentTaskRescheduleResponse(
    int TaskId,
    int EquipmentInventoryId,
    string EquipmentCode,
    EquipmentTaskType TaskType,
    int Sequence,
    DateOnly OldDueDate,
    DateOnly NewDueDate)
{
    public int OccurrenceId => TaskId;
    public string Target => EquipmentCode;
    public DateOnly OldTargetDate => OldDueDate;
    public DateOnly NewTargetDate => NewDueDate;
}

public enum EquipmentPlanExceptionKind
{
    NoBaseline,
    NoInterval,
    RetiredInstrument
}

public record EquipmentPlanExceptionResponse(
    EquipmentPlanExceptionKind Kind,
    EquipmentTaskType? TaskType,
    int EquipmentInventoryId,
    string EquipmentCode,
    string Message)
{
    public string Target => EquipmentCode;
}



public record EquipmentTaskResponse(
    int Id,
    int EquipmentInventoryId,
    EquipmentTaskType TaskType,
    int Year,
    int Sequence,
    bool IsUnplanned,
    int? CreatedByRevisionId,
    int? RetiredByRevisionId,
    DateOnly OriginalDueDate,
    DateOnly DueDate,
    DateOnly? CompletedOn,
    int? CalibrationDocumentId,
    int? MaintenanceLogEntryId,
    string IntervalSnapshot,
    uint Version)
{
    public static EquipmentTaskResponse From(EquipmentTask t) => new(
        t.Id,
        t.EquipmentInventoryId,
        t.TaskType,
        t.Year,
        t.Sequence,
        t.IsUnplanned,
        t.CreatedByRevisionId,
        t.RetiredByRevisionId,
        t.OriginalDueDate,
        t.DueDate,
        t.CompletedOn,
        t.CalibrationDocumentId,
        t.MaintenanceLogEntryId,
        t.IntervalSnapshot,
        t.Version);
}

public record EquipmentPlanPreviewResponse(
    EquipmentPlanRevisionResponse Revision,
    List<EquipmentPlanTypeTotal> Totals,
    List<EquipmentTaskRetirementResponse> ToRetire,
    List<EquipmentTaskRescheduleResponse> ToReschedule,
    List<EquipmentPlanExceptionResponse> Exceptions,
    List<EquipmentTaskResponse> Tasks);
