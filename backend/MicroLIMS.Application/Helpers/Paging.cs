namespace MicroLIMS.Application.Helpers;

// Normalises every list endpoint's paging input (CLAUDE.md Rule 5.4):
// page is at least 1, and pageSize is the endpoint's default when not given
// (<= 0) and never above MaxPageSize, so no request can pull an unbounded
// page. Screens that need every row page through (frontend fetchAllPages).
public static class Paging
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize, int defaultPageSize)
    {
        if (defaultPageSize < 1 || defaultPageSize > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(defaultPageSize), $"Default page size must be 1-{MaxPageSize}.");
        return (Math.Max(page, 1), pageSize <= 0 ? defaultPageSize : Math.Min(pageSize, MaxPageSize));
    }
}
