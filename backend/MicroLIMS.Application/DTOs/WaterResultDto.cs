namespace MicroLIMS.Application.DTOs;

public class WaterResultDto
{
    public int SamplingPointId { get; set; }
    public string SamplingPointCode { get; set; } = string.Empty;
    public string TestCode { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool IsOutOfSpec { get; set; }
}
