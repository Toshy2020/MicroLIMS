using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Helpers;

// Identifiers for prepared lots - Media.LotNumber and Cryovial.Code - in
// the form {prefix}/{seq:D2}/{yy}. There is one sequence per prefix per
// year, shared by every Material batch carrying that Code (the same code
// is legitimately received again under a new batch), and the next number
// is one past the highest already issued rather than a count of lots:
// numbers issued under the earlier per-media-type counter left gaps
// (RVS/01 then RVS/03) that a count walks straight back into. Issued
// numbers are GMP identifiers cited by evaluations and incubations, so
// they are never renumbered - the sequence just continues past them.
public static class PreparedLotNumber
{
    // Material.Code isn't guaranteed present (nullable) - falls back to
    // an alphanumeric, uppercased version of the material's name so the
    // identifier always has a usable prefix.
    public static string PrefixFor(Material material) =>
        !string.IsNullOrWhiteSpace(material.Code)
            ? material.Code
            : new string(material.MaterialName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static async Task<string> NextAsync(IQueryable<string> issuedNumbers, string prefix)
    {
        var yy = DateTime.UtcNow.ToString("yy", CultureInfo.InvariantCulture);
        var head = prefix + "/";
        var tail = "/" + yy;

        var sameSeries = await issuedNumbers
            .Where(n => n.StartsWith(head) && n.EndsWith(tail))
            .ToListAsync();

        // Anything between head and tail that isn't a plain number belongs
        // to a different series whose prefix merely starts the same way
        // (e.g. a code containing "/"), so it doesn't count.
        var highest = sameSeries
            .Select(n => n.Length > head.Length + tail.Length ? n[head.Length..^tail.Length] : string.Empty)
            .Select(seq => int.TryParse(seq, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}/{highest + 1:D2}/{yy}";
    }
}
