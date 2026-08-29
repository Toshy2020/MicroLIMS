namespace MicroLIMS.Domain.Entities;

// Generalized sampling configuration for Water sampling points (and
// reusable for any future point-based sampling domain).
public class SamplingConfiguration
{
    public int Id { get; set; }
    public int WaterSamplingPointId { get; set; }
    public WaterSamplingPoint? WaterSamplingPoint { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string AlertLimit { get; set; } = string.Empty;
    public string ActionLimit { get; set; } = string.Empty;
    public string SpecLimit { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}
