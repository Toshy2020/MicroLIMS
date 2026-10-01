namespace MicroLIMS.Shared.Constants;

// Section codes of the two laboratories. Dashboards are per laboratory: a
// Microbiology dashboard never shows Physicochemical activity and vice versa.
public static class LaboratoryCodes
{
    public const string Microbiology = "MICRO";
    public const string Physicochemical = "FP";

    public static bool IsKnown(string? code) =>
        string.Equals(code, Microbiology, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, Physicochemical, StringComparison.OrdinalIgnoreCase);
}
