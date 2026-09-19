using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class ParameterResult
{
    public int Id { get; set; }

    public int TestAnalysisId { get; set; }
    public TestAnalysis? TestAnalysis { get; set; }

    public int TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }

    public int SpecificationId { get; set; }
    public Specification? Specification { get; set; }

    public string ParameterName { get; set; } = string.Empty;

    public decimal? ReportedValue { get; set; }
    public string ReportedDisplay { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? SpecLimit { get; set; }
    public ResultBasis? ResultBasis { get; set; }
    public string ComparisonStatus { get; set; } = string.Empty;

    public bool OverRange { get; set; }
    public bool BelowLoq { get; set; }

    public int? ValidityRecordItemId { get; set; }
    public CalibrationRunAnalyte? CalibrationRunAnalyte { get; set; }

    public string? CalculationJson { get; set; }
    public int? StageReached { get; set; }

    public bool IsActive { get; set; } = true;

    public List<ResultReading> Readings { get; set; } = new();

    [NotMapped]
    public ElementalCalculationData? ElementalCalculation
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CalculationJson)) return null;
            try
            {
                return JsonSerializer.Deserialize<ElementalCalculationData>(CalculationJson, JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    [NotMapped]
    public string Element => ElementalCalculation?.Element ?? string.Empty;

    [NotMapped]
    public decimal? ReportedPpm => ElementalCalculation?.ReportedPpm;

    [NotMapped]
    public decimal? MgPerUnit => ElementalCalculation?.MgPerUnit;

    [NotMapped]
    public decimal? ResultClaim => ElementalCalculation?.ResultClaim;

    [NotMapped]
    public decimal? PercentLabelClaim => ElementalCalculation?.PercentLabelClaim;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
