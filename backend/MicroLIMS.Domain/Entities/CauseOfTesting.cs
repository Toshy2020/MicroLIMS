namespace MicroLIMS.Domain.Entities;

// Shared master list across all six receiving categories (Section Head
// managed) - e.g. Routine, Investigation, Retest.
public class CauseOfTesting : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
