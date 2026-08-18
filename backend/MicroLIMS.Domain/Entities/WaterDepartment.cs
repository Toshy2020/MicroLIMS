namespace MicroLIMS.Domain.Entities;

// Water-specific department, deliberately separate from the EM
// Department entity. A sample location (WaterSamplingPoint) hangs off
// one of these, mirroring EM's Department -> Room hierarchy. Testing
// frequency lives on the sample location, not here.
public class WaterDepartment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<WaterSamplingPoint> SamplingPoints { get; set; } = new();
}
