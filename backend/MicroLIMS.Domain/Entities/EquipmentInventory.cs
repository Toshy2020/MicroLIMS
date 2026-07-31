using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Equipment register under Inventory - every instrument in the
// Microbiology lab (incubators, balances, pipettes, pH meters, LAFs,
// etc.) with serial number, firmware, and calibration due date. Mirrors
// the paper/Excel "List of instruments & equipment in QC laboratories".
//
// Deliberately a separate entity from Domain.Entities.Equipment, which
// is the small, workflow-linked master-data list (Incubator/Autoclave/
// LafCabinet/BiologicalSafetyCabinet/WaterBath/Other) that Media
// Preparation and GPT incubation steps select from. Merging the two
// would force every ruler, pipette, and thermometer into that narrow
// EquipmentType enum and risk breaking the FK the workflow engines
// depend on. This register is a pure asset/calibration list; nothing
// in the workflow engines reads from it.
public class EquipmentInventory
{
    public int Id { get; set; }

    public string InstrumentType { get; set; } = string.Empty; // free text - too varied for a fixed enum (Incubator, Pipette, pH meter, Ruler, ...)
    public string ManufacturerName { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. INC-F-ML-F-01-003
    public string Location { get; set; } = string.Empty;

    public DateTime? CalibrationDueDate { get; set; }
    public EquipmentOperationalStatus Status { get; set; } = EquipmentOperationalStatus.InService;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    // Not mapped - purely a display convenience for the list/print view.
    public bool IsCalibrationOverdue => CalibrationDueDate.HasValue && CalibrationDueDate.Value.Date < DateTime.UtcNow.Date;
}
