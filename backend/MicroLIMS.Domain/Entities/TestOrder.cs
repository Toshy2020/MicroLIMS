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
    public List<Result> Results { get; set; } = new();
    public List<Incubation> Incubations { get; set; } = new();
    public List<WorkflowHistory> WorkflowHistory { get; set; } = new();
}
