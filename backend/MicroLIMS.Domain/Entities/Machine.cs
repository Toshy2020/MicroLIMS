namespace MicroLIMS.Domain.Entities;

public class Machine : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    // Inactive rows are kept for history but hidden from receiving and
    // preparation; a row referenced by samples is deactivated, not deleted.
    public bool IsActive { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public List<MachinePart> Parts { get; set; } = new();
}
