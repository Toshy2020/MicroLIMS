namespace MicroLIMS.Application.Services;

// Narrows a user's accessible sections down to the single laboratory the
// workspace page is asking for (?labSectionId=), so a user who belongs to
// both Microbiology and Finished Product only sees the one they picked
// instead of both mixed together on one page. It never widens access: a
// lab outside the caller's existing scope is refused, not silently ignored.
public static class LabScope
{
    public static IReadOnlyList<int>? Narrow(IReadOnlyList<int>? scope, int? labSectionId)
    {
        if (!labSectionId.HasValue)
        {
            return scope;
        }

        // scope == null means an admin (unrestricted); narrowing just picks the one lab.
        if (scope != null && !scope.Contains(labSectionId.Value))
        {
            throw new UnauthorizedAccessException("The requested laboratory is outside your accessible sections.");
        }

        return new[] { labSectionId.Value };
    }
}
