namespace MicroLIMS.Domain.Entities;

// One element an ICP test reports. Kept by Id on update so item specifications stay linked.
public class TestDefinitionIcpElement
{
    public int Id { get; set; }
    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }
    public int DisplayOrder { get; set; }
    public string Symbol { get; set; } = string.Empty;   // "Zn", "Ca"
    public decimal? TargetPpm { get; set; }              // Assay mode only: the target concentration (> 0)
}
