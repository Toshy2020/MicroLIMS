using System.Linq.Expressions;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Helpers;

/// <summary>
/// Normalises server-side sort parameters against explicit typed key selectors
/// and appends a stable Id tie-breaker so paged results never overlap.
/// </summary>
public static class ServerSort
{
    public static bool ParseAscending(string? sortDir, bool fallbackDescending = false)
    {
        if (string.IsNullOrWhiteSpace(sortDir))
            return !fallbackDescending;

        var normalized = sortDir.Trim().ToLowerInvariant();
        return normalized switch
        {
            "asc" => true,
            "desc" => false,
            _ => throw new BusinessRuleException(
                MessageCodes.TableViewInvalidSortDirection,
                $"Invalid sort direction '{sortDir}'. Expected 'asc' or 'desc'.",
                new Dictionary<string, string> { ["sortDir"] = sortDir ?? string.Empty })
        };
    }

    public static IOrderedQueryable<TEntity> ApplyOrder<TEntity, TKey>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TKey>> keySelector,
        bool ascending,
        Expression<Func<TEntity, int>> idSelector) =>
        ascending
            ? query.OrderBy(keySelector).ThenBy(idSelector)
            : query.OrderByDescending(keySelector).ThenBy(idSelector);

    public static IOrderedQueryable<TEntity> ApplyOrderWithGroup<TEntity, TKey>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, int>> groupSelector,
        Expression<Func<TEntity, TKey>> keySelector,
        bool ascending,
        Expression<Func<TEntity, int>> idSelector)
    {
        var grouped = query.OrderBy(groupSelector);
        var sorted = ascending
            ? grouped.ThenBy(keySelector)
            : grouped.ThenByDescending(keySelector);
        return sorted.ThenBy(idSelector);
    }
}
