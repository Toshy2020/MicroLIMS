using MicroLIMS.Shared.Responses;
using System.Globalization;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Competency;
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
        int assigneeId, int actingUserId, IEnumerable<int> sectionIds, DateTime nowUtc,
        IReadOnlyCollection<int>? testOrderIds = null, ICompetencyGuard? competency = null, CancellationToken ct = default)
    {
        var (check, user) = await CheckAssignableAsync(db, scope, assigneeId, actingUserId, sectionIds, nowUtc, ct);
        check.ThrowIfRefused();
        if (competency is not null && testOrderIds is { Count: > 0 })
        {
            var decisions = await competency.EvaluateAssigneeAsync(assigneeId, testOrderIds, ct);
            ThrowIfCompetencyRefused(decisions.Select(pair => (pair.Key, pair.Value)));
        }
        return user;
    }

    internal static async Task<ActionCheck> CheckCompetencyAsync(
        ICompetencyGuard competency, int userId, int testOrderId, CancellationToken ct = default) =>
        (await competency.EvaluateAsync(userId, testOrderId, ct)).ToActionCheck();

    internal static void ThrowIfCompetencyRefused(IEnumerable<(int OrderId, CompetencyDecision Decision)> decisions)
    {
        var refusals = decisions.Where(entry => !entry.Decision.Allowed).ToList();
        var errors = refusals.SelectMany(entry => entry.Decision.Reasons.Select(reason =>
            new ApiError(reason.Code, new Dictionary<string, string>(reason.Params)
            {
                ["testOrderId"] = entry.OrderId.ToString(CultureInfo.InvariantCulture)
            }, reason.Text))).ToList();
        var message = string.Join(" ", refusals.Select(entry => $"Test #{entry.OrderId}: {string.Join(" ", entry.Decision.ReasonTexts)}"));
        CompetencyBatch.ThrowIfRefused(MessageCodes.CompetencyAssignmentRefused, errors, message);
    }

    // EnsureAssignableAsync without the throw: the first reason the assignee cannot take tests in these sections,
    // and the assignee. An assignee that does not exist still throws NotFoundException.
    public static async Task<(ActionCheck Check, User User)> CheckAssignableAsync(
        IMicroLimsDbContext db, IUserSectionScopeService scope,
        int assigneeId, int actingUserId, IEnumerable<int> sectionIds, DateTime nowUtc, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == assigneeId, ct)
            ?? throw new NotFoundException(
                MessageCodes.SampleAssigneeNotFound,
                $"User {assigneeId} not found.",
                new Dictionary<string, string> { ["userId"] = assigneeId.ToString(CultureInfo.InvariantCulture) });

        if (!user.IsActive)
            return (ActionCheck.No($"User '{user.FullName}' is not active."), user);
        if (user.LockedUntil > nowUtc)
            return (ActionCheck.No($"User '{user.FullName}' is locked out."), user);

        var role = user.Role?.Type;
        if (role is null || !AssignableRoles.Contains(role.Value))
            return (ActionCheck.No(
                $"'{user.FullName}' cannot be assigned tests - only Analysts, Reviewers and Section Heads can."), user);

        if (role == RoleType.SectionHead && assigneeId != actingUserId)
            return (ActionCheck.No(
                $"'{user.FullName}' is a Section Head - a Section Head can only assign themselves."), user);

        var memberOf = await scope.GetMemberSectionIdsAsync(assigneeId, ct);
        var missing = sectionIds.Distinct().Where(s => memberOf is not null && !memberOf.Contains(s)).ToList();
        if (missing.Count > 0)
        {
            var names = await db.DocumentSections.Where(s => missing.Contains(s.Id)).Select(s => s.Name).ToListAsync(ct);
            var sections = names.Count > 0 ? string.Join(", ", names) : "the test's";
            return (ActionCheck.No(
                $"'{user.FullName}' is not a member of the {sections} laboratory section{(names.Count > 1 ? "s" : "")}."), user);
        }

        return (ActionCheck.Ok, user);
    }
}
