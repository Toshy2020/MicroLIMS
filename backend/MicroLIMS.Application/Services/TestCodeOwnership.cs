using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services;

public static class TestCodeOwnership
{
    public static async Task<HashSet<string>?> GetEditableCodesAsync(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        int userId,
        CancellationToken ct = default)
    {
        if (userId == 0) return null;

        var scopeIds = await scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scopeIds is null) return null; // System Administrator - unrestricted

        var userAreas = await scope.GetPhyschemAreasAsync(userId, ct);

        var tests = await db.TestDefinitions
            .AsNoTracking()
            .Where(t => scopeIds.Contains(t.SectionId))
            .Select(t => new { t.Code, t.PhyschemArea })
            .ToListAsync(ct);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var test in tests)
        {
            if (!test.PhyschemArea.HasValue || userAreas.Any(a => PhyschemAreas.Includes(test.PhyschemArea.Value, a)))
            {
                result.Add(test.Code);
            }
        }

        return result;
    }

    public static async Task<string[]> GetEditableCodesOrAllActiveAsync(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        int userId,
        CancellationToken ct = default)
    {
        var editable = await GetEditableCodesAsync(db, scope, userId, ct);
        if (editable is null)
        {
            return await db.TestDefinitions
                .AsNoTracking()
                .Where(t => t.IsActive)
                .Select(t => t.Code)
                .ToArrayAsync(ct);
        }

        return editable.ToArray();
    }

    public static async Task EnsureCanEditAsync(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        int userId,
        IEnumerable<string> codes,
        string what,
        CancellationToken ct = default)
    {
        if (userId == 0) return;

        var codeList = codes?.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (codeList is null || codeList.Count == 0) return;

        var scopeIds = await scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scopeIds is null) return; // System Administrator - unrestricted

        var userAreas = await scope.GetPhyschemAreasAsync(userId, ct);

        var testDefs = await db.TestDefinitions
            .AsNoTracking()
            .Include(t => t.Section)
            .Where(t => codeList.Contains(t.Code))
            .ToListAsync(ct);

        var byCode = testDefs.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var code in codeList)
        {
            if (!byCode.TryGetValue(code, out var testDef))
            {
                throw new InvalidOperationException($"Test code '{code}' is not in the Test Master.");
            }

            var labName = testDef.Section?.Name ?? "another laboratory section";

            if (!scopeIds.Contains(testDef.SectionId))
            {
                throw new UnauthorizedAccessException(
                    $"{what} for test {code} belongs to {labName} - only its Section Head can change it.");
            }

            if (testDef.PhyschemArea.HasValue && !userAreas.Any(a => PhyschemAreas.Includes(testDef.PhyschemArea.Value, a)))
            {
                throw new UnauthorizedAccessException(
                    $"{what} for test {code} belongs to {labName} - only its Section Head can change it.");
            }
        }
    }

    public static Task EnsureCanEditAsync(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        int userId,
        string code,
        string what,
        CancellationToken ct = default) =>
        EnsureCanEditAsync(db, scope, userId, new[] { code }, what, ct);
}
