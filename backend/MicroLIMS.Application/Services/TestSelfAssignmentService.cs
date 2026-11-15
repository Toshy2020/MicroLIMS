using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// An analyst takes an unassigned test in their own section, and gives it back while nothing is recorded on it.
// The assignment and its audit row are saved together in one transaction under the row's xmin token, so when two
// analysts press Take together exactly one save wins and the other leaves no trace.
public sealed class TestSelfAssignmentService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public TestSelfAssignmentService(IMicroLimsDbContext db, IUserSectionScopeService scope, TimeProvider? time = null)
    {
        _db = db;
        _scope = scope;
        _time = time ?? TimeProvider.System;
    }

    public async Task TakeAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await EnsurePermissionAsync(userId, ct);

        var order = await _db.TestOrders.Include(t => t.Sample).SingleOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test {testOrderId} not found.");

        // Readable first (a test outside the caller's reach is not theirs to see); section setting and membership follow.
        var readable = await _scope.GetReadableSectionIdsAsync(userId, ct);
        if (readable is not null && !readable.Contains(order.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        if (!await SectionSelfAssignmentService.IsEnabledAsync(_db, order.SectionId, ct))
            throw new BusinessRuleException("Taking tests is switched off in this section. Ask a Section Head to assign this test.");

        if (order.AssignedAnalystId is int holderId)
            throw new BusinessRuleException($"Already taken by {await FullNameAsync(holderId, ct)}.");
        if (order.IsSuperseded || order.OutsourcedToLab is not null || !IsOpen(order))
            throw new BusinessRuleException("This test is closed, replaced or outsourced and can no longer be taken.");

        var now = _time.GetUtcNow().UtcDateTime;
        var user = await AnalystAssignmentRules.EnsureAssignableAsync(_db, _scope, userId, userId, new[] { order.SectionId }, now);

        _db.AuditLogs.Add(SampleAssignmentService.BuildAssignmentAudit(order.Sample!, order, TestReleaseRules.TakenAction,
            $"{order.TestCode}: Unassigned", $"{order.TestCode}: {user.FullName}", userId, now));
        order.AssignedAnalystId = userId;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone changed the row after it was read. The failed save rolled back the assignment and its audit row.
            var winner = await _db.TestOrders.AsNoTracking().Where(t => t.Id == testOrderId).Select(t => t.AssignedAnalystId).SingleOrDefaultAsync(ct);
            throw winner is int winnerId
                ? new BusinessRuleException($"Already taken by {await FullNameAsync(winnerId, ct)}.")
                : new BusinessRuleException("This test was changed by someone else. Refresh and try again.");
        }
    }

    public async Task ReleaseAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await EnsurePermissionAsync(userId, ct);

        var order = await _db.TestOrders.Include(t => t.Sample).SingleOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test {testOrderId} not found.");
        if (order.AssignedAnalystId != userId)
            throw new BusinessRuleException("Only the analyst who holds this test can release it.");
        if (!(await TestReleaseRules.ReleasableAsync(_db, userId, new[] { testOrderId }, ct)).Contains(testOrderId))
            throw new BusinessRuleException(TestReleaseRules.CannotRelease);

        var now = _time.GetUtcNow().UtcDateTime;
        var name = await FullNameAsync(userId, ct);
        _db.AuditLogs.Add(SampleAssignmentService.BuildAssignmentAudit(order.Sample!, order, TestReleaseRules.ReleasedAction,
            $"{order.TestCode}: {name}", $"{order.TestCode}: Unassigned", userId, now));
        order.AssignedAnalystId = null;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("This test was changed by someone else. Refresh and try again.");
        }
    }

    private static bool IsOpen(TestOrder order) => order.Status is ApprovalStatus.Pending or ApprovalStatus.InProgress;

    // The permission is also checked at the endpoint; here it guards every other caller of the service.
    private async Task EnsurePermissionAsync(int userId, CancellationToken ct)
    {
        var holds = await (
                from u in _db.Users.AsNoTracking()
                join rp in _db.RolePermissions.AsNoTracking() on u.RoleId equals rp.RoleId
                join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                where u.Id == userId && u.IsActive && p.Code == PermissionConstants.TestsSelfAssign
                select p.Id)
            .AnyAsync(ct);
        if (!holds)
            throw new UnauthorizedAccessException("You do not have permission to take or release tests.");
    }

    private async Task<string> FullNameAsync(int userId, CancellationToken ct) =>
        await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).SingleOrDefaultAsync(ct)
        ?? $"User #{userId}";
}
