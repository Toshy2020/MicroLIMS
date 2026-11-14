using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Predicates the analyst sources share, so no two of them can disagree about what "my test" or
// "returned to me" means. Everything here composes into SQL.
internal static class AnalystQueries
{
    // Work an analyst can still act on: not superseded, and not yet handed to review or closed.
    public static readonly Expression<Func<TestOrder, bool>> IsActiveTest = t =>
        !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress);

    // Open tests nobody holds: not superseded, not closed, not outsourced.
    public static IQueryable<TestOrder> UnassignedOpenTests(IMicroLimsDbContext db) =>
        db.TestOrders.AsNoTracking()
            .Where(t => t.AssignedAnalystId == null && t.OutsourcedToLab == null)
            .Where(IsActiveTest);

    // A test that was returned to the analyst and has had nothing recorded since (the TestReturnHelper rule,
    // decided in SQL). Mine = the latest return was to me and the test is still assigned to me.
    public static IQueryable<TestReturnEvent> MyPendingReturns(IMicroLimsDbContext db, int userId) =>
        db.TestReturnEvents.AsNoTracking().Where(e =>
            e.AssignedAnalystId == userId
            && e.TestOrder!.AssignedAnalystId == userId
            && !e.TestOrder.IsSuperseded
            && e.TestOrder.Status != ApprovalStatus.Approved
            && e.TestOrder.Status != ApprovalStatus.Rejected
            && e.TestOrder.Status != ApprovalStatus.Voided
            && e.TestOrder.Status != ApprovalStatus.Cancelled
            && !db.TestReturnEvents.Any(x => x.TestOrderId == e.TestOrderId
                && (x.ReturnedAt > e.ReturnedAt || (x.ReturnedAt == e.ReturnedAt && x.Id > e.Id)))
            && !db.CountTestReadings.Any(r => r.TestOrderId == e.TestOrderId && r.IsActive));

    // Section scope in SQL. null = every section (Sections.ViewAll reading); it never widens "mine".
    public static IQueryable<T> InSections<T>(this IQueryable<T> query, MyWorkContext ctx, Expression<Func<T, int>> sectionId)
    {
        if (ctx.ReadableSectionIds is null) return query;
        var ids = ctx.ReadableSectionIds.ToArray();
        var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { typeof(int) },
            Expression.Constant(ids), sectionId.Body);
        return query.Where(Expression.Lambda<Func<T, bool>>(contains, sectionId.Parameters));
    }

    public static Expression<Func<T, bool>> Not<T>(Expression<Func<T, bool>> predicate) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(predicate.Body), predicate.Parameters);
}
