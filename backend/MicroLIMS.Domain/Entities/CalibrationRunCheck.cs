using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class CalibrationRunCheck
{
    public int Id { get; set; }

    public int CalibrationRunAnalyteId { get; set; }
    public CalibrationRunAnalyte? CalibrationRunAnalyte { get; set; }

    public CalibrationCheckType CheckType { get; set; }
    public int SequencePosition { get; set; }

    public decimal? NominalMgPerL { get; set; }
    public decimal MeasuredMgPerL { get; set; }
    public decimal? RecoveryPercent { get; set; }
    public bool Passed { get; set; }
}
