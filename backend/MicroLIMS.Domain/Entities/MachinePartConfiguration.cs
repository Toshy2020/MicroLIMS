namespace MicroLIMS.Domain.Entities;

public class MachinePartConfiguration
{
    public int Id { get; set; }
    public int MachinePartId { get; set; }
    public MachinePart? MachinePart { get; set; }
    public string TestType { get; set; } = string.Empty; // "Swab", "Rinse", or a pathogen TestCode
    public string TestCode { get; set; } = string.Empty;
    public string AlertLimit { get; set; } = string.Empty;
    public string ActionLimit { get; set; } = string.Empty;
    public string SpecLimit { get; set; } = string.Empty;
    public bool IsPathogenTest { get; set; } // true for E.coli/P.aeruginosa/Salmonella-type entries, false for Swab/Rinse TAMC
}
