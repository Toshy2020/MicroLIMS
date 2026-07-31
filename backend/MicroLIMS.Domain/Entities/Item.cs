using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// The Master Configuration record. Everything the WorkflowEngine does on
// sample receipt is driven from this entity (Frozen Principle #1).
public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public SampleCategory Category { get; set; }
    public string SopNumber { get; set; } = string.Empty;
    public List<Specification> Specifications { get; set; } = new();
    public List<SampleTest> AssignedTests { get; set; } = new();
}
