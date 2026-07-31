using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Every step transition a TestOrder goes through, in order. This is
// what lets the Review/Approval screens show "workflow history" and
// what the OOT/audit review process replays.
public class WorkflowHistory
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public WorkflowStep FromStep { get; set; }
    public WorkflowStep ToStep { get; set; }
    public string? Note { get; set; }
    public int PerformedByUserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
