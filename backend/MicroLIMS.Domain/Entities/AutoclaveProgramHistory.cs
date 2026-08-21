namespace MicroLIMS.Domain.Entities;

public class AutoclaveProgramHistory
{
    public int Id { get; set; }
    public int AutoclaveProgramId { get; set; }
    public AutoclaveProgram AutoclaveProgram { get; set; } = null!;
    public string Action { get; set; } = string.Empty; // "Created", "Updated", "StatusChanged"
    public string ProgramCode { get; set; } = string.Empty;
    public string PreviousProgramName { get; set; } = string.Empty;
    public string NewProgramName { get; set; } = string.Empty;
    public string PreviousLoadType { get; set; } = string.Empty;
    public string NewLoadType { get; set; } = string.Empty;
    public decimal PreviousTemperature { get; set; }
    public decimal NewTemperature { get; set; }
    public int PreviousCycleTimeMinutes { get; set; }
    public int NewCycleTimeMinutes { get; set; }
    public bool PreviousIsActive { get; set; }
    public bool NewIsActive { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
