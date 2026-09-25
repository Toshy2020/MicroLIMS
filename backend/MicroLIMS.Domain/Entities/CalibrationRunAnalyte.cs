using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class CalibrationRunAnalyte
{
    public int Id { get; set; }

    public int CalibrationRunId { get; set; }
    public CalibrationRun? CalibrationRun { get; set; }

    public int TestAnalyteId { get; set; }
    public TestAnalyte? TestAnalyte { get; set; }

    // Snapshots copied from TestAnalyte
    public string Element { get; set; } = string.Empty;
    public decimal WavelengthNm { get; set; }
    public AnalyteView View { get; set; }

    public decimal CorrelationValue { get; set; }
    public CorrelationType CorrelationType { get; set; }
    public int NumberOfStandards { get; set; }
    public decimal LowestStandardMgPerL { get; set; }
    public decimal HighestStandardMgPerL { get; set; }

    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }

    public List<CalibrationRunCheck> Checks { get; set; } = new();
}
