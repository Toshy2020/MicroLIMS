using Microsoft.EntityFrameworkCore;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Application.Abstractions.Live;
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
        (await CheckPermissionAsync(userId, ct)).ThrowIfRefused();

        var order = await _db.TestOrders.Include(t => t.Sample).SingleOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test {testOrderId} not found.");

        var now = _time.GetUtcNow().UtcDateTime;
        var (check, taker) = await CheckTakeAsync(order, userId, now, ct);
        check.ThrowIfRefused();
        // Never null once the check is allowed; this keeps the compiler's null analysis honest.
        var user = taker ?? throw new NotFoundException($"User {userId} not found.");

        _db.AuditLogs.Add(SampleAssignmentService.BuildAssignmentAudit(order.Sample!, order, TestReleaseRules.TakenAction,
            $"{order.TestCode}: Unassigned", $"{order.TestCode}: {user.FullName}", userId, now));
        order.AssignedAnalystId = userId;
        _db.QueueSignal(LiveSignalKinds.Counters, $"section:{order.SectionId}");
        _db.QueueSignal(LiveSignalKinds.Record, $"testOrder:{order.Id}");
        _db.QueueSignal(LiveSignalKinds.Record, $"sample:{order.SampleId}");
        _db.QueueSignal(LiveSignalKinds.MyWork, $"user:{userId}");

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
        (await CheckPermissionAsync(userId, ct)).ThrowIfRefused();

        var order = await _db.TestOrders.Include(t => t.Sample).SingleOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test {testOrderId} not found.");
        (await CheckReleaseAsync(order, userId, ct)).ThrowIfRefused();

        var now = _time.GetUtcNow().UtcDateTime;
        var name = await FullNameAsync(userId, ct);
        _db.AuditLogs.Add(SampleAssignmentService.BuildAssignmentAudit(order.Sample!, order, TestReleaseRules.ReleasedAction,
            $"{order.TestCode}: {name}", $"{order.TestCode}: Unassigned", userId, now));
        order.AssignedAnalystId = null;
        _db.QueueSignal(LiveSignalKinds.Counters, $"section:{order.SectionId}");
        _db.QueueSignal(LiveSignalKinds.Record, $"testOrder:{order.Id}");
        _db.QueueSignal(LiveSignalKinds.Record, $"sample:{order.SampleId}");
        _db.QueueSignal(LiveSignalKinds.MyWork, $"user:{userId}");

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("This test was changed by someone else. Refresh and try again.");
        }
    }

    // The pre-conditions of taking a test, in the order the action applies them: the permission, the test is
    // readable, taking is switched on in its section, nobody holds it, it is open, and the user may be assigned
    // tests in that section.
    public async Task<ActionCheck> CheckTakeAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        var permission = await CheckPermissionAsync(userId, ct);
        if (!permission.Allowed) return permission;

        var order = await _db.TestOrders.AsNoTracking().SingleOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test {testOrderId} not found.");
        return (await CheckTakeAsync(order, userId, _time.GetUtcNow().UtcDateTime, ct)).Check;
    }

    // User is the taker, set once every check has passed.
    private async Task<(ActionCheck Check, User? User)> CheckTakeAsync(TestOrder order, int userId, DateTime now, CancellationToken ct)
    {
        // Readable first (a test outside the caller's reach is not theirs to see); section setting and membership follow.
        var readable = await _scope.GetReadableSectionIdsAsync(userId, ct);
        if (readable is not null && !readable.Contains(order.SectionId))
            return (ActionCheck.Denied(MessageCodes.TestNotInYourSection, "This test belongs to a laboratory section you are not assigned to."), null);

        if (!await SectionSelfAssignmentService.IsEnabledAsync(_db, order.SectionId, ct))
            return (ActionCheck.No("Taking tests is switched off in this section. Ask a Section Head to assign this test."), null);

        if (order.AssignedAnalystId is int holderId)
        {
            var holderName = await FullNameAsync(holderId, ct);
            return (ActionCheck.No(MessageCodes.TestAlreadyTaken, $"Already taken by {holderName}.",
                new Dictionary<string, string> { ["name"] = holderName }), null);
        }
        if (order.IsSuperseded || order.OutsourcedToLab is not null || !IsOpen(order))
            return (ActionCheck.No("This test is closed, replaced or outsourced and can no longer be taken."), null);

        var (assignable, user) = await AnalystAssignmentRules.CheckAssignableAsync(_db, _scope, userId, userId, new[] { order.SectionId }, now, ct);
        return (assignable, user);
    }

    // The pre-conditions of giving a test back, in the order the action applies them: the permission, the user
    // holds the test, and it is still theirs to release (taken by themselves, nothing recorded on it).
    public async Task<ActionCheck> CheckReleaseAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        var permission = await CheckPermissionAsync(userId, ct);
        if (!permission.Allowed) return permission;

        var order = await _db.TestOrders.AsNoTracking().SingleOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test {testOrderId} not found.");
        return await CheckReleaseAsync(order, userId, ct);
    }

    private async Task<ActionCheck> CheckReleaseAsync(TestOrder order, int userId, CancellationToken ct)
    {
        if (order.AssignedAnalystId != userId)
            return ActionCheck.No(MessageCodes.TestOnlyHolderCanRelease, "Only the analyst who holds this test can release it.");
        if (!(await TestReleaseRules.ReleasableAsync(_db, userId, new[] { order.Id }, ct)).Contains(order.Id))
            return ActionCheck.No(TestReleaseRules.CannotRelease);
        return ActionCheck.Ok;
    }

    private static bool IsOpen(TestOrder order) => order.Status is ApprovalStatus.Pending or ApprovalStatus.InProgress;

    // The permission is also checked at the endpoint; here it guards every other caller of the service.
    private async Task<ActionCheck> CheckPermissionAsync(int userId, CancellationToken ct)
    {
        var holds = await (
                from u in _db.Users.AsNoTracking()
                join rp in _db.RolePermissions.AsNoTracking() on u.RoleId equals rp.RoleId
                join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                where u.Id == userId && u.IsActive && p.Code == PermissionConstants.TestsSelfAssign
                select p.Id)
            .AnyAsync(ct);
        return holds ? ActionCheck.Ok : ActionCheck.Denied("You do not have permission to take or release tests.");
    }

    private async Task<string> FullNameAsync(int userId, CancellationToken ct) =>
        await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).SingleOrDefaultAsync(ct)
        ?? $"User #{userId}";
}
