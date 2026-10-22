using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Who may be put on a test as its analyst. Analysts, Reviewers and Section
// Heads all run tests, but a Section Head is only ever assigned by
// themselves - no one else, not even another Section Head, puts one on a
// test. The assignee must also be a member of the test's laboratory
// section. Every path that sets TestOrder.AssignedAnalystId on someone
// else's say-so checks here, so the rule lives in one place.
public static class AnalystAssignmentRules
{
    public static readonly IReadOnlyList<RoleType> AssignableRoles =
        new[] { RoleType.Analyst, RoleType.Reviewer, RoleType.SectionHead };

    // Active, unlocked users the acting user may assign: every Analyst and
    // Reviewer, plus the acting user when they are a Section Head.
    public static IQueryable<User> Candidates(IMicroLimsDbContext db, int actingUserId, DateTime nowUtc) =>
        db.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive && (u.LockedUntil == null || u.LockedUntil <= nowUtc) && u.Role != null
                && (u.Role.Type == RoleType.Analyst
                    || u.Role.Type == RoleType.Reviewer
                    || (u.Role.Type == RoleType.SectionHead && u.Id == actingUserId)));

    // Returns the assignee, or throws with the reason they can't take tests
    // in these sections when the acting user is the one assigning them.
    public static async Task<User> EnsureAssignableAsync(
        IMicroLimsDbContext db, IUserSectionScopeService scope,
        int assigneeId, int actingUserId, IEnumerable<int> sectionIds, DateTime nowUtc)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == assigneeId)
            ?? throw new NotFoundException($"User {assigneeId} not found.");

        if (!user.IsActive)
            throw new InvalidOperationException($"User '{user.FullName}' is not active.");
        if (user.LockedUntil > nowUtc)
            throw new InvalidOperationException($"User '{user.FullName}' is locked out.");

        var role = user.Role?.Type;
        if (role is null || !AssignableRoles.Contains(role.Value))
            throw new InvalidOperationException(
                $"'{user.FullName}' cannot be assigned tests - only Analysts, Reviewers and Section Heads can.");

        if (role == RoleType.SectionHead && assigneeId != actingUserId)
            throw new InvalidOperationException(
                $"'{user.FullName}' is a Section Head - a Section Head can only assign themselves.");

        var memberOf = await scope.GetAccessibleSectionIdsAsync(assigneeId);
        var missing = sectionIds.Distinct().Where(s => memberOf is not null && !memberOf.Contains(s)).ToList();
        if (missing.Count > 0)
        {
            var names = await db.DocumentSections.Where(s => missing.Contains(s.Id)).Select(s => s.Name).ToListAsync();
            var sections = names.Count > 0 ? string.Join(", ", names) : "the test's";
            throw new InvalidOperationException(
                $"'{user.FullName}' is not a member of the {sections} laboratory section{(names.Count > 1 ? "s" : "")}.");
        }

        return user;
    }
}
