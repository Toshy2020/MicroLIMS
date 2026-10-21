using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Monitoring;

public record ApprovePlanRequest(string Password, string? Reason);

public record PlanRevisionResponse(
    int Id, uint Version, int Year, int RevisionNumber, PlanRevisionStatus Status, string? Reason,
    DateOnly EffectiveFrom, DateTime GeneratedAt, int GeneratedByUserId, DateTime? ApprovedAt, int OccurrenceCount)
{
    public static PlanRevisionResponse From(MonitoringPlanRevision r, int occurrenceCount) => new(
        r.Id, r.Version, r.Year, r.RevisionNumber, r.Status, r.Reason,
        r.EffectiveFrom, r.GeneratedAt, r.GeneratedByUserId, r.ApprovedAt, occurrenceCount);
}

public record PlanCategoryTotal(MonitoringCategory Category, int Targets, int Occurrences);

public record PlanRetirementResponse(
    int OccurrenceId, MonitoringCategory Category, string Target, string PeriodKey, DateOnly WindowStart, DateOnly WindowEnd);

public record PlanExceptionResponse(PlanExceptionKind Kind, MonitoringCategory Category, string Target, string Message);

public record PlanPreviewResponse(
    PlanRevisionResponse Revision,
    List<PlanCategoryTotal> Totals,
    List<PlanRetirementResponse> ToRetire,
    List<PlanExceptionResponse> Exceptions);
