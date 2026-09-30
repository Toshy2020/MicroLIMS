namespace MicroLIMS.Domain.Enums;

// HPLC chain S6 - one System Suitability record per run.
// Pending -> Passed | Failed via ConfirmSstAsync. Final - a confirmed record is not re-evaluated.
public enum HplcSstStatus
{
    Pending,
    Passed,
    Failed
}
