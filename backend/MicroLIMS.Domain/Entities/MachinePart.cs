namespace MicroLIMS.Domain.Entities;

public class MachinePart
{
    public int Id { get; set; }
    public int MachineId { get; set; }
    public Machine? Machine { get; set; }
    public string Name { get; set; } = string.Empty;

    // Swab / Rinse TAMC config (each with its own limits) plus any
    // additional pathogen tests configured for this part. Checking one
    // of these at preparation time is what generates the TestOrder.
    public List<MachinePartConfiguration> TestConfigurations { get; set; } = new();
}
