using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Immutable status transition history for an instrument in the Equipment Register.
// Retains the full traceability of every operational status change (InService,
// OutOfService, Retired) along with the mandatory comment explaining the reason.
//
// Strictly append-only from the application UI; never edited or deleted.
public class EquipmentStatusHistory
{
    public int Id { get; set; }

    // FK to the equipment instrument record — Restrict delete.
    public int EquipmentInventoryId { get; set; }
    public EquipmentInventory EquipmentInventory { get; set; } = null!;

    public EquipmentOperationalStatus PreviousStatus { get; set; }
    public EquipmentOperationalStatus NewStatus { get; set; }

    // Mandatory comment/reason for the transition (e.g. "Sent for calibration", "Returned to service").
    public string Comment { get; set; } = string.Empty;

    public int ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
