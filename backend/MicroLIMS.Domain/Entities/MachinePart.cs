namespace MicroLIMS.Domain.Entities;

public class MachinePart : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    // Inactive rows are kept for history but hidden from receiving and
    // preparation; a row referenced by samples is deactivated, not deleted.
    public bool IsActive { get; set; } = true;
    public int MachineId { get; set; }
    public Machine? Machine { get; set; }
    public string Name { get; set; } = string.Empty;

    // Swab / Rinse TAMC config (each with its own limits) plus any
    // additional pathogen tests configured for this part. Checking one
    // of these at preparation time is what generates the TestOrder.
    public List<MachinePartConfiguration> TestConfigurations { get; set; } = new();
}
