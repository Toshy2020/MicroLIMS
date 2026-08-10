using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class WorkflowStepResult
{
    public int Id { get; set; }

    // The step attempt this result closes. One result per incubation.
    public int IncubationId { get; set; }
    public Incubation? Incubation { get; set; }

    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }

    public string StepName { get; set; } = string.Empty;
    public StepType StepType { get; set; }

    public GrowthObservation? SelectivePlatingObservation { get; set; }

    // Written once, at submission, from MediaChallengeSpec.ExpectedDescription.
    // Never updated afterwards (ALCOA+ Original and Contemporaneous).
    public string? ExpectedAppearanceSnapshot { get; set; }

    public ConfirmatoryResult? ConfirmatoryResult { get; set; }

    public string? BiochemicalResultText { get; set; }

    // No Attachment entity exists yet - unmapped forward hook, no FK.
    public int? BiochemicalAttachmentId { get; set; }

    // Analyst submitted Detected straight off confirmatory plating.
    public bool SkippedBiochemical { get; set; }

    // Set when a reviewer returns the result for biochemical confirmation.
    public bool RequiresBiochemical { get; set; }
    public string? ReturnReason { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public int? ReturnedByUserId { get; set; }

    public int SubmittedByUserId { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ConfirmatoryMediaSelection> Selections { get; set; } = new();
    public List<ConfirmatoryPlateObservation> ConfirmatoryObservations { get; set; } = new();
}
