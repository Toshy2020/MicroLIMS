using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public static class TestSectionLookup
{
    public static async Task<Dictionary<string, int>> ResolveAsync(
        MicroLimsDbContext db,
        IEnumerable<string> testCodes,
        CancellationToken ct = default)
    {
        var codeList = testCodes.Distinct(StringComparer.Ordinal).ToList();
        if (codeList.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var definitions = await db.TestDefinitions
            .AsNoTracking()
            .Where(td => codeList.Contains(td.Code))
            .Select(td => new { td.Code, td.SectionId })
            .ToListAsync(ct);

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var def in definitions)
        {
            if (codeList.Contains(def.Code, StringComparer.Ordinal))
            {
                map[def.Code] = def.SectionId;
            }
        }

        var missing = codeList.Where(c => !map.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Test code(s) {string.Join(", ", missing.Select(c => $"'{c}'"))} are not in the Test Master. Add them to the Test Master with a laboratory section before creating this test.");
        }

        return map;
    }
}
