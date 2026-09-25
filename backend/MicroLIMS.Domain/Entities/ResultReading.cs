using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class ResultReading
{
    public int Id { get; set; }

    public int ParameterResultId { get; set; }
    public ParameterResult? ParameterResult { get; set; }

    public ReadingKind Kind { get; set; }
    public int Index { get; set; }
    public int? Stage { get; set; }
    public decimal? TimePointMinutes { get; set; }
    public decimal? Value1 { get; set; }
    public decimal? Value2 { get; set; }
    public decimal? Value3 { get; set; }
    public string? Text { get; set; }
    public decimal? ComputedValue { get; set; }
    public bool? Passed { get; set; }
}
