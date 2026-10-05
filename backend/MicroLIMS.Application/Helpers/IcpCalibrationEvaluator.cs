using System.Globalization;

namespace MicroLIMS.Application.Helpers;

public record IcpCalibrationElementInput(string Symbol, decimal? CorrelationR, decimal? BlankMgPerL, decimal? IcvMeasuredMgPerL);
public record IcpCalibrationElementVerdict(string Symbol, bool Passed, decimal? IcvRecoveryPercent, IReadOnlyList<string> FailureReasons);
public record IcpCheckLimits(decimal MinCorrelation, bool RequireBlank, decimal? BlankMaxMgPerL,
    bool RequireIcv, decimal? IcvNominalMgPerL, decimal? IcvLowPercent, decimal? IcvHighPercent);

// ICP calibration checks per element: r (always), blank and ICV (when required). Pure, boundaries inclusive.
public static class IcpCalibrationEvaluator
{
    public static IcpCalibrationElementVerdict Evaluate(IcpCalibrationElementInput e, IcpCheckLimits l)
    {
        var reasons = new List<string>();
        decimal? recovery = null;

        if (e.CorrelationR is null) reasons.Add("r is required.");
        else if (e.CorrelationR < l.MinCorrelation)
            reasons.Add($"r {Fmt(e.CorrelationR.Value)} is below the minimum {Fmt(l.MinCorrelation)}.");

        if (l.RequireBlank)
        {
            if (e.BlankMgPerL is null) reasons.Add("Blank is required.");
            else if (l.BlankMaxMgPerL is { } max && e.BlankMgPerL > max)
                reasons.Add($"Blank {Fmt(e.BlankMgPerL.Value)} mg/L is above the limit {Fmt(max)} mg/L.");
        }

        if (l.RequireIcv)
        {
            if (e.IcvMeasuredMgPerL is null) reasons.Add("ICV result is required.");
            else if (l.IcvNominalMgPerL is > 0m && l.IcvLowPercent is { } low && l.IcvHighPercent is { } high)
            {
                var (rec, ok) = EvaluateCcv(e.IcvMeasuredMgPerL.Value, l.IcvNominalMgPerL.Value, low, high);
                recovery = rec;
                if (!ok)
                    reasons.Add($"ICV recovery {Math.Round(rec, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture)}% is outside {Fmt(low)}-{Fmt(high)}%.");
            }
        }

        return new IcpCalibrationElementVerdict(e.Symbol, reasons.Count == 0, recovery, reasons);
    }

    public static (decimal RecoveryPercent, bool Passed) EvaluateCcv(decimal measuredMgPerL, decimal nominal, decimal low, decimal high)
    {
        var rec = measuredMgPerL / nominal * 100m;
        return (rec, rec >= low && rec <= high);
    }

    // value as given, without trailing-zero padding
    private static string Fmt(decimal v) => (v / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);
}
