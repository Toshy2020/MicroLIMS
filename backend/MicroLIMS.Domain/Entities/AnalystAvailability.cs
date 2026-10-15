namespace MicroLIMS.Domain.Entities;

// Leave, training or secondment window for one analyst. Never deleted: a correction is an edit, and the
// automatic audit trail keeps the previous values. Windows of one user never overlap (enforced by the service).
public class AnalystAvailability
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }   // >= FromDate
    public decimal FractionAvailable { get; set; } // 0..1; 0 = absent, 0.5 = half-day
    public string Reason { get; set; } = string.Empty;
    public int EnteredByUserId { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
}
