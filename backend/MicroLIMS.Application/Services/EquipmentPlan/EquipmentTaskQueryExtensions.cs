using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.EquipmentPlan;

public static class EquipmentTaskQueryExtensions
{
    public static IQueryable<EquipmentTask> LiveTasks(this IQueryable<EquipmentTask> query) =>
        query.Where(t => t.CreatedByRevisionId == null || t.CreatedByRevision!.Status != PlanRevisionStatus.Draft);
}
