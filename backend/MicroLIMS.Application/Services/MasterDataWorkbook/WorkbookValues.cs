using System.Globalization;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// How values are written to the workbook on download, and how a workbook
// value is compared with the database's value to decide "unchanged".
public static partial class WorkbookValues
{
    public static string Fmt(string? v) => v ?? string.Empty;
    public static string Fmt(decimal? v) => v?.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty;
    public static string Fmt(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    public static string Fmt(bool? v) => v is null ? string.Empty : v.Value ? "Yes" : "No";
    public static string Fmt(DateTime? v) => v?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
    public static string Fmt<T>(T? v) where T : struct, Enum => v?.ToString() ?? string.Empty;
    public static string FmtList(IEnumerable<string> values) => string.Join(";", values);

    // Case-insensitive, trimmed, pipe-joined: the identity of a row.
    public static string Key(params string?[] parts) =>
        string.Join("|", parts.Select(p => (p ?? string.Empty).Trim().ToUpperInvariant()));

    public static string Normalise(string? value, bool isList)
    {
        var text = (value ?? string.Empty).Trim();
        if (isList)
            return string.Join(";", text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        if (text.Length == 0) return text;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d.ToString("G29", CultureInfo.InvariantCulture);
        return text.ToLowerInvariant() switch
        {
            "true" or "yes" => "yes",
            "false" or "no" => "no",
            _ => text,
        };
    }
}
