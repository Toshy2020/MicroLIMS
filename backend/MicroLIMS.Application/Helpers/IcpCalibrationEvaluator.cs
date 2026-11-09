using System.Globalization;

namespace MicroLIMS.Application.Helpers;

public record IcpCalibrationElementVerdict(string Symbol, bool Passed, string? FailureReason);

// The calibration check per element: the analyst's r against the fixed minimum. Pure, boundary inclusive.
public static class IcpCalibrationEvaluator
{
    public static IcpCalibrationElementVerdict Evaluate(string symbol, decimal? correlationR)
    {
        if (correlationR is null)
            return new(symbol, false, "r is required.");
        if (correlationR < IcpDefaults.MinCorrelation)
            return new(symbol, false, $"r {Fmt(correlationR.Value)} is below the minimum {Fmt(IcpDefaults.MinCorrelation)}.");
        return new(symbol, true, null);
    }

    // value as given, without trailing-zero padding
    private static string Fmt(decimal v) => (v / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);
}
