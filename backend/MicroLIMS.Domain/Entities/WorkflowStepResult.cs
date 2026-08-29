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

    public bool? IsSharedSessionStep { get; set; }

    public GrowthObservation? SelectivePlatingObservation { get; set; }

    // Written once, at submission, from MediaChallengeSpec.ExpectedDescription.
    // Never updated afterwards (ALCOA+ Original and Contemporaneous).
    public string? ExpectedAppearanceSnapshot { get; set; }

    public ConfirmatoryResult? ConfirmatoryResult { get; set; }

    public string? BiochemicalResultText { get; set; }

    // No Attachment entity exists yet - unmapped forward hook, no FK.
    public int? BiochemicalAttachmentId { get; set; }

    // The analyst's explicit interpretation of BiochemicalResultText -
    // required at submission time by TestWorkflowEngine.SubmitBiochemicalAsync
    // (nullable here only so historical pre-this-field rows don't need a
    // literal schema default). Never inferred from the free-text result:
    // an earlier version of this code hardcoded the final result to
    // "Detected" regardless of what the biochemical result actually said,
    // which produced a real false-positive result once this step became
    // reachable after an Inconclusive confirmatory reading.
    public bool? BiochemicalOrganismDetected { get; set; }

    // Analyst submitted Detected straight off confirmatory plating.
    public bool SkippedBiochemical { get; set; }

    // The decision point after an all-conforming confirmatory result.
    // Recorded for BOTH branches (not just the skip), because "the
    // analyst chose to proceed to biochemical confirmation" is itself a
    // GMP decision that needs a contemporaneous record. Non-null also
    // means the decision has already been taken, which is what makes
    // RecordAnalystDecisionAsync single-shot.
    public AnalystDecision? AnalystDecision { get; set; }
    public DateTime? AnalystDecisionAtUtc { get; set; }
    public int? AnalystDecisionByUserId { get; set; }

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
