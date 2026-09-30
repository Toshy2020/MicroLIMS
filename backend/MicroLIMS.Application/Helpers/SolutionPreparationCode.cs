using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

// Sequential codes for Solution Preparation completions (HPLC chain S4,
// spec 4): Mobile Phase "MP-{ABBR} {nn}/{MM}/{yyyy}" (series per method
// abbreviation), Diluent "DL-{nn}/{MM}/{yyyy}", Titrant "VS-{nn}/{MM}/{yyyy}".
// Continuous per (head, lab-local calendar year), resetting in January -
// same "highest + 1 over issued codes" approach as SystemSuitabilityRunCode,
// protected at the database by a unique index on SolutionPreparation.Code.
public static class SolutionPreparationCode
{
    public static string Head(SolutionType type, string? methodAbbreviation) => type switch
    {
        SolutionType.MobilePhase => string.IsNullOrWhiteSpace(methodAbbreviation)
            ? throw new InvalidOperationException("A method abbreviation is required for a mobile phase code.")
            : $"MP-{methodAbbreviation} ",
        SolutionType.Diluent => "DL-",
        SolutionType.Titrant => "VS-",
        _ => throw new InvalidOperationException($"Unknown solution type '{type}'.")
    };

    public static async Task<string> NextAsync(
        IQueryable<string> issuedCodes,
        string head,
        DateTime labLocal,
        CancellationToken ct = default)
    {
        var yearStr = labLocal.ToString("yyyy", CultureInfo.InvariantCulture);
        var mm = labLocal.ToString("MM", CultureInfo.InvariantCulture);
        var yearSuffix = "/" + yearStr;

        List<string> sameSeries;
        try
        {
            sameSeries = await issuedCodes
                .Where(c => c.StartsWith(head) && c.EndsWith(yearSuffix))
                .ToListAsync(ct);
        }
        catch (InvalidOperationException)
        {
            sameSeries = issuedCodes
                .Where(c => c.StartsWith(head) && c.EndsWith(yearSuffix))
                .ToList();
        }

        var highest = sameSeries
            .Select(c =>
            {
                if (c.Length <= head.Length) return 0;
                var rest = c[head.Length..];
                var parts = rest.Split('/');
                if (parts.Length == 3 && parts[2] == yearStr
                    && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var seq))
                {
                    return seq;
                }
                return 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        var nextSeq = highest + 1;
        return $"{head}{nextSeq:D2}/{mm}/{yearStr}";
    }
}
