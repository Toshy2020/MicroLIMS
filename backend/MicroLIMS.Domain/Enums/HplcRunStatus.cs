namespace MicroLIMS.Domain.Enums;

// HPLC chain S6 - HPLC Workspace (spec 5).
// Open -> Completed (every assigned sample finalized) | Abandoned (reason required). Both final.
public enum HplcRunStatus
{
    Open,
    Completed,
    Abandoned
}
