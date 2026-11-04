namespace MicroLIMS.Domain.Entities;

// Shared master list across all six receiving categories (Section Head
// managed) - e.g. Routine, Investigation, Retest.
public class CauseOfTesting : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // A sample received for this cause (e.g. "Periodic retest") gets no test
    // orders at receipt: a Section Head or Supervisor chooses its tests later.
    public bool RequiresTestSelection { get; set; }
}
