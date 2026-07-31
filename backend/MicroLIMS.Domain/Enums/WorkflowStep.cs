namespace MicroLIMS.Domain.Enums;

// Fine-grained state tracking requested in the gap analysis (section 3).
// This is distinct from ApprovalStatus: ApprovalStatus tracks the
// review/approval lifecycle, WorkflowStep tracks where a TestOrder is
// *inside* its own frozen workflow (e.g. EM has two incubation steps).
public enum WorkflowStep
{
    Waiting,
    Running,
    Incubating,
    Ready,
    Reviewed,
    Approved
}
