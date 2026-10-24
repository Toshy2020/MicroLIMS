namespace MicroLIMS.Application.DTOs;

// One figure on a laboratory section card ("3 overdue"). Tone is the status
// palette name the frontend already uses for badges: "detected" (needs
// attention now), "action" (coming up), "info" (in progress) or "pending"
// (neutral) - so the number's colour is decided here, with the rule, and the
// card only draws it.
public record SectionCounterDto(string Label, int Value, string Tone);

// The figures for one card, keyed by the card's id (see SectionCardKeys).
public record SectionCardCountersDto(string Card, IReadOnlyList<SectionCounterDto> Counters);

// Which cards the caller may open - decided by the API layer from the same
// policies that guard each page, so a card's numbers are never computed for
// someone who cannot see the page behind it.
public record SectionCardAccess(
    bool Media = false,
    bool Cryovials = false,
    bool Materials = false,
    bool Equipment = false,
    bool Solutions = false);

public static class SectionCardKeys
{
    public const string Workspace = "workspace";
    public const string FpWorkspace = "fp-workspace";
    public const string RmPmWorkspace = "rmpm-workspace";
    public const string Media = "media";
    public const string Cryovials = "cryovials";
    public const string Materials = "materials";
    public const string Equipment = "equipment";
    public const string Solutions = "solutions";
    public const string WorkingStandards = "working-standards";
    public const string Hplc = "hplc";
    public const string Gc = "gc";
    public const string Icp = "icp";
    public const string ReadingList = "reading-list";
}
