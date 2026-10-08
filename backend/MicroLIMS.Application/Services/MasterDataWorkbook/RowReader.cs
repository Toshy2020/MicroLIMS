using System.Globalization;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// A problem with one cell or row of the workbook. The message is shown to
// the lab as-is, next to the sheet, row and column.
public class RowError : Exception
{
    public RowError(string? column, string message) : base(message) { Column = column; }
    public string? Column { get; }
}

// Typed access to one workbook row. Every value arrives as invariant text
// (see IWorkbookFormat); this is the only place it is parsed.
public sealed class RowReader
{
    private readonly WorkbookRow _row;

    public RowReader(WorkbookRow row) { _row = row; }

    public int RowNumber => _row.RowNumber;

    public string? OptionalText(string column) =>
        _row.Cells.TryGetValue(column, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    public string Text(string column) =>
        OptionalText(column) ?? throw new RowError(column, $"{column} is required.");

    public decimal? Decimal(string column)
    {
        var text = OptionalText(column);
        if (text is null) return null;
        if (text.Contains(','))
            throw new RowError(column, $"{column} '{text}' uses a comma. Use a dot as the decimal separator, for example 0.5.");
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : throw new RowError(column, $"{column} '{text}' is not a number.");
    }

    public int? Int(string column)
    {
        var d = Decimal(column);
        if (d is null) return null;
        return d == decimal.Truncate(d.Value) ? (int)d.Value : throw new RowError(column, $"{column} must be a whole number.");
    }

    public int RequiredInt(string column) => Int(column) ?? throw new RowError(column, $"{column} is required.");

    public bool? Bool(string column) => OptionalText(column)?.ToLowerInvariant() switch
    {
        null => null,
        "yes" or "true" => true,
        "no" or "false" => false,
        var other => throw new RowError(column, $"{column} '{other}' must be Yes or No."),
    };

    public T Enum<T>(string column) where T : struct, System.Enum =>
        OptionalEnum<T>(column) ?? throw new RowError(column, $"{column} is required. Allowed: {string.Join(", ", System.Enum.GetNames<T>())}.");

    public T? OptionalEnum<T>(string column) where T : struct, System.Enum
    {
        var text = OptionalText(column);
        if (text is null) return null;
        // Names only: a number would bind to whatever value happens to have it.
        if (!int.TryParse(text, out _) && System.Enum.TryParse<T>(text, ignoreCase: true, out var value) && System.Enum.IsDefined(value))
            return value;
        throw new RowError(column, $"{column} '{text}' is not allowed. Allowed: {string.Join(", ", System.Enum.GetNames<T>())}.");
    }

    public IReadOnlyList<string> List(string column) =>
        (OptionalText(column) ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    public DateTime? Date(string column)
    {
        var text = OptionalText(column);
        if (text is null) return null;
        return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : throw new RowError(column, $"{column} '{text}' must be a date written as yyyy-MM-dd.");
    }
}
