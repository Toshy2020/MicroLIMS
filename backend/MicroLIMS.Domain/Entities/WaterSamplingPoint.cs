namespace MicroLIMS.Domain.Entities;

public class WaterSamplingPoint : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    // Inactive rows are kept for history but hidden from receiving and
    // preparation; a row referenced by samples is deactivated, not deleted.
    public bool IsActive { get; set; } = true;
    public string Code { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string TestingFrequency { get; set; } = string.Empty;
    public List<string> AssignedTestCodes { get; set; } = new();

    public int? WaterDepartmentId { get; set; }
    public WaterDepartment? WaterDepartment { get; set; }
}
