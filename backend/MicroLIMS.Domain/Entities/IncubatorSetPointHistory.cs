namespace MicroLIMS.Domain.Entities;

public class IncubatorSetPointHistory
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public decimal PreviousSetPoint { get; set; }
    public decimal NewSetPoint { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
