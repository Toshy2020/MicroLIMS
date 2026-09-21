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
    public MeasurementCalculationData? MeasurementCalculation
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CalculationJson)) return null;
            try
            {
                return JsonSerializer.Deserialize<MeasurementCalculationData>(CalculationJson, JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    [NotMapped]
    public HplcMultiAnalyteCalculationData? HplcMultiAnalyteCalculation
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CalculationJson)) return null;
            try
            {
                // Other result types store different JSON shapes; only ours carries preparations.
                var data = JsonSerializer.Deserialize<HplcMultiAnalyteCalculationData>(CalculationJson, JsonOptions);
                return data?.Preparations is { Count: > 0 } && !string.IsNullOrEmpty(data.AnalyteName) ? data : null;
            }
            catch
            {
                return null;
            }
        }
    }

    [NotMapped]
    public string Element => HplcMultiAnalyteCalculation?.AnalyteName ?? ElementalCalculation?.Element ?? string.Empty;

    [NotMapped]
    public decimal? ReportedPpm => ElementalCalculation?.ReportedPpm;

    [NotMapped]
    public decimal? MgPerUnit => HplcMultiAnalyteCalculation?.Mpu ?? ElementalCalculation?.MgPerUnit;

    [NotMapped]
    public decimal? ResultClaim => HplcMultiAnalyteCalculation?.Result ?? ElementalCalculation?.ResultClaim;

    [NotMapped]
    public decimal? PercentLabelClaim => HplcMultiAnalyteCalculation?.PercentLabelClaim ?? ElementalCalculation?.PercentLabelClaim;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
