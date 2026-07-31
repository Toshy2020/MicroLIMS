namespace MicroLIMS.Application.DTOs;

public class ResultDto
{
    public int ResultId { get; set; }
    public int TestOrderId { get; set; }
    public string RawValue { get; set; } = string.Empty;
    public string? InterpretedValue { get; set; }
    public DateTime EnteredAt { get; set; }
}
