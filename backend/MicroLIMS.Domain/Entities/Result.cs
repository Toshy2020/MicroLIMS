using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class Result
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public string RawValue { get; set; } = string.Empty;
    public string? InterpretedValue { get; set; }
    public ResultType Type { get; set; }
    public int EnteredByUserId { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
}
