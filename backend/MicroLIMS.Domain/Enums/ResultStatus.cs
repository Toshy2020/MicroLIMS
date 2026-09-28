namespace MicroLIMS.Domain.Enums;

// The verdicts a recorded result carries - SampleLocation.Status,
// CountTestReading.Status, ParameterResult.ComparisonStatus and a pathogen
// workflow's final result. Stored as the member name (text) and sent to
// the frontend as the name, so members must never be renamed.
public enum ResultStatus
{
    // Numbering starts at 1 so a status that was never set (0) is not a
    // pass: it is stored and shown as "0" and judged non-conforming.
    // Quantitative result inside every configured limit.
    WithinLimits = 1,

    // Environmental / water monitoring: over the alert or the action limit.
    AlertLimitExceeded,
    ActionLimitExceeded,

    // Fails the specification.
    OutOfSpecification,

    // There was nothing to compare the result against.
    LimitsNotConfigured,

    // A reviewer has to judge it (e.g. TNTC / uncountable plates, a value
    // that cannot be classified against the limit type).
    RequiresReview,

    // Presence/absence tests: no growth / organism found.
    Absent,
    Detected,

    // Presumptive growth still going through the confirmation chain.
    PendingConfirmation,

    // Final result of a pathogen workflow that confirmed no organism.
    NotDetected,

    // Staged tests (dissolution, disintegration, weight variation): this
    // stage did not settle the result and the next stage must be run.
    NextStageRequired,

    // A pathogen primary observation that could not be called either way.
    Inconclusive
}
