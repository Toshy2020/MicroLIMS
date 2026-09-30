namespace MicroLIMS.Domain.Enums;

// HPLC chain S6 - a sample assigned to a run. Removed is not deleted -
// the row stays for traceability with a reason (Global Constraints:
// "Nothing hard-deleted").
public enum HplcRunSampleStatus
{
    Assigned,
    Removed
}
