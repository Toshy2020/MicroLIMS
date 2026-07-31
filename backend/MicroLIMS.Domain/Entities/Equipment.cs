using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// General Section Head equipment list (Autoclave, Incubator, LAF
// Cabinet, etc.). Incubator-type equipment additionally carries a set
// point temperature and calibration due date.
public class Equipment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // e.g. INC-002, LAF-037
    public EquipmentType Type { get; set; }
    public string? Location { get; set; }

    // Incubator-only fields (null for other equipment types).
    public decimal? SetPointTemperature { get; set; }
    public DateTime? CalibrationDueDate { get; set; }
}
