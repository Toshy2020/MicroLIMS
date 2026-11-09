using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class WaterPointLabFrequency : IVersionedEntity, IHasFrequency
{
    public int Id { get; set; }
    public uint Version { get; set; }

    public int WaterSamplingPointId { get; set; }
    public WaterSamplingPoint WaterSamplingPoint { get; set; } = null!;

    public int SectionId { get; set; }
    public DocumentSection Section { get; set; } = null!;

    public FrequencyType? FrequencyType { get; set; }
    public int? FrequencyInterval { get; set; }
    public DayOfWeek? SamplingWeekday { get; set; }
    public int? FrequencyAnchorMonth { get; set; }
    public int? FrequencyWeekOfMonth { get; set; }
}
