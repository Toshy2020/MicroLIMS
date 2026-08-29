namespace MicroLIMS.Domain.Entities;

// Audit entry capturing an explicit "Return to Analyst" workflow event.
// Distinct from generic AuditLogs update-tracking so Phase 2 KPI queries
// can directly and unambiguously count returns per analyst in date ranges.
public class TestReturnEvent
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public int ReviewerUserId { get; set; }
    public User? ReviewerUser { get; set; }
    public int? AssignedAnalystId { get; set; }
    public User? AssignedAnalyst { get; set; }
    public string? Reason { get; set; }
    public DateTime ReturnedAt { get; set; } = DateTime.UtcNow;
}
