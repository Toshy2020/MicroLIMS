using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Auto-created by the WorkflowEngine when a Sample is received - never
// created manually by a user (Frozen Principle: "No manual test creation").
public class TestOrder
{
    public int Id { get; set; }
    public int SampleId { get; set; }
    public Sample? Sample { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public WorkflowStep CurrentStep { get; set; } = WorkflowStep.Waiting;
    public int? AssignedAnalystId { get; set; }

    // Set true when a RetestRetainedSample decision creates a fresh
    // TestOrder to replace this one - the workflow engine's step
    // completion check treats a step as permanently done once any result
    // row exists for it, so a retest can never reuse the same row. Old
    // superseded rows are kept (never deleted) as history.
    public bool IsSuperseded { get; set; }

    // EM only - which Room this TestOrder's checked (Room x TestType)
    // preparation selection came from. Needed so TestWorkflowEngine can
    // look up this Room's RoomTestConfiguration limits at result time
    // (the same way Product uses Specification and Water uses
    // SamplingConfiguration) instead of trusting a client-supplied limit.
    public int? RoomId { get; set; }
    public Room? Room { get; set; }

    public List<Result> Results { get; set; } = new();
    public List<Incubation> Incubations { get; set; } = new();
    public List<WorkflowHistory> WorkflowHistory { get; set; } = new();
}
