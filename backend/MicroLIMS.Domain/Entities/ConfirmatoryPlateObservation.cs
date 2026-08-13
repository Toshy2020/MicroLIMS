using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One plate reading per selected confirmatory medium per run.
public class ConfirmatoryPlateObservation
{
    public int Id { get; set; }

    public int WorkflowStepResultId { get; set; }
    public WorkflowStepResult? WorkflowStepResult { get; set; }

    public int MaterialId { get; set; }
    public Material? Material { get; set; }

    public GrowthObservation Observation { get; set; }

    // Written once at submission; never updated (ALCOA+).
    public string? ExpectedAppearanceSnapshot { get; set; }

    public int RecordedByUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
