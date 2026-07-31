namespace MicroLIMS.Application.DTOs;

public class EMResultDto
{
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string SamplingPosition { get; set; } = string.Empty;
    public int Step1Count { get; set; }
    public int Step2Count { get; set; }
    public bool IsOutOfTrend { get; set; }
}
