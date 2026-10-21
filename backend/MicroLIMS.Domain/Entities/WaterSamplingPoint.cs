using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class WaterSamplingPoint : IVersionedEntity, IHasFrequency
{
    public int Id { get; set; }
    public uint Version { get; set; }
    // Inactive rows are kept for history but hidden from receiving and
    // preparation; a row referenced by samples is deactivated, not deleted.
    public bool IsActive { get; set; } = true;

    public FrequencyType? FrequencyType { get; set; }
    public int? FrequencyInterval { get; set; }
    public DayOfWeek? SamplingWeekday { get; set; }
    public int? FrequencyAnchorMonth { get; set; }
    public int? FrequencyWeekOfMonth { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string TestingFrequency { get; set; } = string.Empty;
    public List<string> AssignedTestCodes { get; set; } = new();

    public int? WaterDepartmentId { get; set; }
    public WaterDepartment? WaterDepartment { get; set; }
}
