namespace MicroLIMS.Application.Helpers;

// How one recorded result stands against its limits. The single rule the
// Certificate of Analysis and the OOS outcome propagation share - there
// used to be a copy in the browser and one in SampleApprovalService, and
// they disagreed about results with no status.
public enum ResultConformance
{
    Conforms,
    DoesNotConform,
    LimitsNotConfigured,

    // Nothing was recorded, or the result was never evaluated. Never
    // certified as conforming.
    NoResult
}

public static class ResultConformanceRules
{
    // Status of a count reading, a location result or a parameter result.
    public static ResultConformance FromStatus(string? status) => status switch
    {
        null or "" => ResultConformance.NoResult,
        "WithinLimits" or "Absent" => ResultConformance.Conforms,
        "LimitsNotConfigured" => ResultConformance.LimitsNotConfigured,
        _ => ResultConformance.DoesNotConform
    };

    // A detection test: finding the organism is the failure.
    public static ResultConformance FromDetection(bool organismDetected) =>
        organismDetected ? ResultConformance.DoesNotConform : ResultConformance.Conforms;

    // A bare qualitative result value.
    public static ResultConformance FromResultValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? ResultConformance.NoResult
        : string.Equals(value, "Detected", StringComparison.OrdinalIgnoreCase) ? ResultConformance.DoesNotConform
        : ResultConformance.Conforms;
}
