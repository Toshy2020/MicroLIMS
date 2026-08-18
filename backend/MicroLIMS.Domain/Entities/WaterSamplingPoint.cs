namespace MicroLIMS.Domain.Entities;

public class WaterSamplingPoint
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string TestingFrequency { get; set; } = string.Empty;
    public List<string> AssignedTestCodes { get; set; } = new();

    public int? WaterDepartmentId { get; set; }
    public WaterDepartment? WaterDepartment { get; set; }
}
