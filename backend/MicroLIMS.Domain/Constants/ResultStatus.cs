namespace MicroLIMS.Domain.Constants;

// The verdicts a recorded result carries - SampleLocation.Status,
// CountTestReading.Status, ParameterResult.ComparisonStatus, ResultRecord
// and a pathogen workflow's final result. They are stored as text and sent
// to the frontend as-is, so the values are part of the data and the API
// and never change; code refers to them only through these constants.
public static class ResultStatus
{
    // Quantitative result inside every configured limit.
    public const string WithinLimits = "WithinLimits";

    // Environmental / water monitoring: over the alert or the action limit.
    public const string AlertLimitExceeded = "AlertLimitExceeded";
    public const string ActionLimitExceeded = "ActionLimitExceeded";

    // Fails the specification.
    public const string OutOfSpecification = "OutOfSpecification";

    // There was nothing to compare the result against.
    public const string LimitsNotConfigured = "LimitsNotConfigured";

    // A reviewer has to judge it (e.g. TNTC / uncountable plates, a value
    // that cannot be classified against the limit type).
    public const string RequiresReview = "RequiresReview";

    // Presence/absence tests: no growth / organism found.
    public const string Absent = "Absent";
    public const string Detected = "Detected";

    // Presumptive growth still going through the confirmation chain.
    public const string PendingConfirmation = "PendingConfirmation";

    // Final result of a pathogen workflow that confirmed no organism.
    public const string NotDetected = "NotDetected";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        WithinLimits, AlertLimitExceeded, ActionLimitExceeded, OutOfSpecification, LimitsNotConfigured,
        RequiresReview, Absent, Detected, PendingConfirmation, NotDetected
    };
}
