using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Helpers;

// Formats and generates sequential codes for System Suitability Runs:
// {MethodAbbreviation} S.S {seq:00}/{MM}{yyyy} (e.g. "VIT-C S.S 05/112026").
// Continuous per (MethodAbbreviation, calendar year), resetting in January.
public static class SystemSuitabilityRunCode
{
    public static Task<string> NextAsync(
        IQueryable<string> issuedCodes,
        string methodAbbreviation,
        DateTime performedAt,
        CancellationToken ct = default) =>
        NextAsync(issuedCodes, methodAbbreviation, performedAt, "S.S", ct);

    public static Task<string> NextAsync(
        IQueryable<string> issuedCodes,
        string methodAbbreviation,
        DateTime performedAtUtc,
        MicroLIMS.Application.Interfaces.ILabClock clock,
        string infix = "S.S",
        CancellationToken ct = default)
    {
        var localTime = clock.ToLabLocal(performedAtUtc);
        return NextAsync(issuedCodes, methodAbbreviation, localTime, infix, ct);
    }

    public static async Task<string> NextAsync(
        IQueryable<string> issuedCodes,
        string methodAbbreviation,
        DateTime performedAt,
        string infix,
        CancellationToken ct = default)

    {
        var yearStr = performedAt.ToString("yyyy", CultureInfo.InvariantCulture);
        var mm = performedAt.ToString("MM", CultureInfo.InvariantCulture);
        var head = $"{methodAbbreviation} {infix} ";

        List<string> sameSeries;
        try
        {
            sameSeries = await issuedCodes
                .Where(c => c.StartsWith(head) && c.EndsWith(yearStr))
                .ToListAsync(ct);
        }
        catch (InvalidOperationException)
        {
            sameSeries = issuedCodes
                .Where(c => c.StartsWith(head) && c.EndsWith(yearStr))
                .ToList();
        }

        var highest = sameSeries
            .Select(c =>
            {
                if (c.Length <= head.Length) return 0;
                var rest = c[head.Length..];
                var slashIdx = rest.LastIndexOf('/');
                if (slashIdx > 0 && int.TryParse(rest[..slashIdx], NumberStyles.None, CultureInfo.InvariantCulture, out var seq))
                {
                    var suffix = rest[(slashIdx + 1)..];
                    if (suffix.Length == 6 && suffix.EndsWith(yearStr))
                    {
                        return seq;
                    }
                }
                return 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        var nextSeq = highest + 1;
        return $"{head}{nextSeq:D2}/{mm}{yearStr}";
    }
}
