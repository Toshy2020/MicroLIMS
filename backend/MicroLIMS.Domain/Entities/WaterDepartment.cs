namespace MicroLIMS.Domain.Entities;

// Water-specific department, deliberately separate from the EM
// Department entity. A sample location (WaterSamplingPoint) hangs off
// one of these, mirroring EM's Department -> Room hierarchy. Testing
// frequency lives on the sample location, not here.
public class WaterDepartment : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    // Inactive rows are kept for history but hidden from receiving and
    // preparation; a row referenced by samples is deactivated, not deleted.
    public bool IsActive { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public List<WaterSamplingPoint> SamplingPoints { get; set; } = new();
}
