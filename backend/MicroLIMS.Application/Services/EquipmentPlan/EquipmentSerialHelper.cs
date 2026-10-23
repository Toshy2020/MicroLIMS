namespace MicroLIMS.Application.Services.EquipmentPlan;

public static class EquipmentSerialHelper
{
    private static readonly HashSet<string> Placeholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "n/a",
        "na",
        "-",
        "--",
        "—",
        "none",
        "nil",
        "0",
        "unknown"
    };

    public static bool IsPlaceholder(string? serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return true;

        return Placeholders.Contains(serialNumber.Trim());
    }

    public static bool HasMeaningfulSerial(string? serialNumber) => !IsPlaceholder(serialNumber);
}
