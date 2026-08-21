namespace MicroLIMS.Domain.Entities;

public class AutoclaveProgram
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public string ProgramCode { get; set; } = string.Empty; // e.g. P01
    public string ProgramName { get; set; } = string.Empty; // e.g. Prepared Media
    public string LoadType { get; set; } = string.Empty;    // e.g. Media, Glassware, Biohazard Waste
    public decimal Temperature { get; set; }                // e.g. 121.0
    public int CycleTimeMinutes { get; set; }               // e.g. 15
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    public List<AutoclaveProgramHistory> History { get; set; } = new();
}
