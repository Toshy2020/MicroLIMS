using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Live;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.Notifications;

namespace MicroLIMS.Application.Services;

// Puts analysts on a sample's tests. The analyst lives on each TestOrder,
// so one sample can have a different analyst per test. Who may be assigned
// is AnalystAssignmentRules; the acting user only ever changes tests in
// laboratory sections they are a member of.
public class SampleAssignmentService
{
    private readonly IMicroLimsDbContext _db;
    private readonly ITestWorkspaceService _workspaceService;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;
    private readonly INotificationPublisher _publisher;
    private readonly ICompetencyGuard? _competency;

    public SampleAssignmentService(IMicroLimsDbContext db, ITestWorkspaceService workspaceService, IUserSectionScopeService scope, TimeProvider? timeProvider = null, INotificationPublisher? publisher = null, ICompetencyGuard? competency = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _competency = competency;
        _workspaceService = workspaceService;
        _scope = scope;
        _publisher = publisher ?? new NotificationPublisher(db, _time);
    }

    internal const string CannotReassignWithResultMessage = "Tests with a result cannot be reassigned.";

    private static readonly ApprovalStatus[] ClosedStatuses =
        { ApprovalStatus.Approved, ApprovalStatus.Rejected, ApprovalStatus.Voided, ApprovalStatus.Cancelled };

    private static bool IsClosed(TestOrder t) => t.IsSuperseded || ClosedStatuses.Contains(t.Status);

    // A test order's result is complete and submitted once its status leaves Pending/InProgress
    // (ResultEntered, Reviewed, RetestRequested, etc.) or its step reaches Ready or later.
    // Intermediate readings/steps saved while InProgress do not block reassignment.
    internal static bool HasResult(TestOrder t) =>
        t.Status is not (ApprovalStatus.Pending or ApprovalStatus.InProgress)
        || t.CurrentStep is WorkflowStep.Ready or WorkflowStep.Reviewed or WorkflowStep.Approved;

    private static bool IsOpen(TestOrder t) => !IsClosed(t) && !HasResult(t);

    // Assignment is a write, so the scope is membership for everyone (never null).
    private static bool InScope(IReadOnlyList<int>? scope, int sectionId) => scope is null || scope.Contains(sectionId);

    // Whole sample: the analyst takes every open test (no submitted result) in the sections
    // they belong to (tests in other sections keep whoever is on them). A null analyst
    // unassigns every open test without a submitted result.
    public async Task<SampleDto> AssignAnalystAsync(
        int sampleId, int? analystUserId, int actingUserId, string? reason = null, CancellationToken ct = default)
    {
        var sample = await LoadSampleAsync(sampleId, ct);
        var changes = await PrepareSampleAssignmentAsync(sample, analystUserId, actingUserId, ct: ct);
        await ApplyAsync(sample, changes, actingUserId, reason, ct);
        return await LoadDtoAsync(sample);
    }

    private static (List<TestOrder> Assignable, ActionCheck Check) EvaluateSampleAssignment(
        Sample sample,
        IReadOnlyList<int>? actingScope)
    {
        var inScope = sample.TestOrders
            .Where(t => !IsClosed(t) && InScope(actingScope, t.SectionId))
            .ToList();
        var assignable = inScope
            .Where(t => !HasResult(t))
            .ToList();

        if (assignable.Count > 0)
            return (assignable, ActionCheck.Ok);

        var check = inScope.Count > 0
            ? ActionCheck.No(MessageCodes.SampleCannotReassignWithResult, CannotReassignWithResultMessage)
            : ActionCheck.No(MessageCodes.SampleNoOpenTestsToAssign, "This sample has no open tests you can assign.");

        return (assignable, check);
    }

    // Whether the acting user can assign anything on the sample: it has an open test without a submitted
    // result in a section they belong to. Who is put on the tests is the user's choice, and
    // AnalystAssignmentRules judges it in the action. The sample carries its TestOrders.
    internal async Task<ActionCheck> CheckAssignAsync(Sample sample, int actingUserId, CancellationToken ct = default)
    {
        var actingScope = await _scope.GetMemberSectionIdsAsync(actingUserId, ct);
        return EvaluateSampleAssignment(sample, actingScope).Check;
    }

    internal async Task<List<(TestOrder Order, int? AnalystId)>> PrepareSampleAssignmentAsync(
        Sample sample,
        int? analystUserId,
        int actingUserId,
        IReadOnlyList<int>? actingScope = null,
        IReadOnlyList<int>? assigneeScope = null,
        CancellationToken ct = default)
    {
        actingScope ??= await _scope.GetMemberSectionIdsAsync(actingUserId, ct);

        var (open, check) = EvaluateSampleAssignment(sample, actingScope);
        check.ThrowIfRefused();

        var targets = open;
        if (analystUserId is int assigneeId)
        {
            var memberOf = assigneeScope ?? await _scope.GetMemberSectionIdsAsync(assigneeId, ct);
            targets = open.Where(t => InScope(memberOf, t.SectionId)).ToList();

            // With no test in the assignee's sections, checking against all
            // of them throws the "not a member" reason.
            await AnalystAssignmentRules.EnsureAssignableAsync(_db, _scope, assigneeId, actingUserId,
                (targets.Count > 0 ? targets : open).Select(t => t.SectionId), _time.GetUtcNow().UtcDateTime,
                testOrderIds: targets.Select(t => t.Id).ToArray(), competency: _competency, ct: ct);
        }

        return targets.Select(t => (t, analystUserId)).ToList();
    }

    // Per test: each listed test gets its own analyst, all in one save.
    public async Task<SampleDto> AssignTestsAsync(
        int sampleId, IReadOnlyList<TestAnalystAssignment> assignments, int actingUserId, string? reason = null, CancellationToken ct = default)
    {
        if (assignments.Count == 0)
            throw new BusinessRuleException(
                MessageCodes.SampleChooseAtLeastOneTestToAssign,
                "Choose at least one test to assign.");
        if (assignments.Select(a => a.TestOrderId).Distinct().Count() != assignments.Count)
            throw new BusinessRuleException(
                MessageCodes.SampleDuplicateTestInAssignment,
                "Each test can be listed only once.");

        var sample = await LoadSampleAsync(sampleId, ct);
        var actingScope = await _scope.GetMemberSectionIdsAsync(actingUserId, ct);
        var now = _time.GetUtcNow().UtcDateTime;

        var changes = new List<(TestOrder Order, int? AnalystId)>();
        var refused = new List<BusinessRuleException>();
        foreach (var assignment in assignments)
        {
            var order = sample.TestOrders.FirstOrDefault(t => t.Id == assignment.TestOrderId)
                ?? throw new NotFoundException(
                    MessageCodes.TestNotFoundOnSample,
                    $"Test {assignment.TestOrderId} is not on sample {sample.ReferenceNumber}.",
                    new Dictionary<string, string>
                    {
                        ["testOrderId"] = assignment.TestOrderId.ToString(),
                        ["referenceNumber"] = sample.ReferenceNumber
                    });

            if (IsClosed(order))
                throw new BusinessRuleException(
                    MessageCodes.TestClosedCannotChangeAnalyst,
                    $"{order.TestCode} is closed - its analyst can no longer be changed.",
                    new Dictionary<string, string> { ["testCode"] = order.TestCode });
            if (!InScope(actingScope, order.SectionId))
                throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
            if (HasResult(order))
                throw new BusinessRuleException(MessageCodes.SampleCannotReassignWithResult, CannotReassignWithResultMessage);

            if (assignment.AnalystUserId is int assigneeId)
            {
                try
                {
                    await AnalystAssignmentRules.EnsureAssignableAsync(_db, _scope, assigneeId, actingUserId,
                        [order.SectionId], now, testOrderIds: [order.Id], competency: _competency, ct: ct);
                }
                catch (BusinessRuleException error) when (error.Code == MessageCodes.CompetencyAssignmentRefused)
                {
                    refused.Add(error);
                }
            }
            changes.Add((order, assignment.AnalystUserId));
        }

        CompetencyBatch.ThrowIfRefused(MessageCodes.CompetencyAssignmentRefused,
            refused.SelectMany(error => error.ErrorDetails ?? []).ToList(), string.Join(" ", refused.Select(error => error.Message)));

        await ApplyAsync(sample, changes, actingUserId, reason, ct);
        return await LoadDtoAsync(sample);
    }

    // Everything the Assign Analyst dialog needs: each test, whether the
    // acting user may change it, and who may take it.
    public async Task<SampleAssignmentOptionsDto> GetAssignmentOptionsAsync(int sampleId, int actingUserId, CancellationToken ct = default)
    {
        var sample = await _db.Samples
            .AsNoTracking()
            .Include(s => s.TestOrders).ThenInclude(t => t.Section)
            .FirstOrDefaultAsync(s => s.Id == sampleId, ct)
            ?? throw new NotFoundException(
                MessageCodes.SampleNotFound,
                $"Sample {sampleId} not found.",
                new Dictionary<string, string> { ["sampleId"] = sampleId.ToString() });

        var actingScope = await _scope.GetMemberSectionIdsAsync(actingUserId, ct);
        var now = _time.GetUtcNow().UtcDateTime;

        var candidates = await AnalystAssignmentRules.Candidates(_db, actingUserId, now)
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
        var sectionsByUser = new Dictionary<int, IReadOnlyList<int>?>();
        foreach (var candidate in candidates)
            sectionsByUser[candidate.Id] = await _scope.GetMemberSectionIdsAsync(candidate.Id, ct);

        var orders = sample.TestOrders.Where(t => !t.IsSuperseded).OrderBy(t => t.SectionId).ThenBy(t => t.TestCode).ToList();
        var assignedIds = orders.Where(t => t.AssignedAnalystId.HasValue).Select(t => t.AssignedAnalystId!.Value).Distinct().ToList();
        var names = await _db.Users.Where(u => assignedIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var openOrders = orders.Where(order => IsOpen(order) && InScope(actingScope, order.SectionId)).ToArray();
        var candidateIds = candidates.Where(candidate => openOrders.Any(order => InScope(sectionsByUser[candidate.Id], order.SectionId)))
            .Select(candidate => candidate.Id).ToArray();
        var decisions = _competency is null
            ? null
            : await _competency.EvaluateCandidatesAsync(candidateIds, openOrders.Select(order => order.Id).ToArray(), ct);

        var tests = orders.Select(t =>
        {
            var canAssign = IsOpen(t) && InScope(actingScope, t.SectionId);
            var eligible = canAssign
                ? candidates.Where(c => InScope(sectionsByUser[c.Id], t.SectionId)).Select(c => c.Id).ToList()
                : new List<int>();
            var refusals = new List<AssignmentCompetencyRefusalDto>();
            foreach (var userId in eligible)
            {
                if (decisions?.GetValueOrDefault((userId, t.Id)) is { Allowed: false } refusal)
                    refusals.AddRange(refusal.Reasons.Select(reason => new AssignmentCompetencyRefusalDto(userId, reason.Code, reason.Params, reason.Text)));
            }
            var refusedUsers = refusals.Select(refusal => refusal.UserId).ToHashSet();
            return new AssignableTestDto(
                t.Id, t.TestCode, t.SectionId, t.Section?.Name ?? string.Empty, t.Status.ToString(),
                t.AssignedAnalystId,
                t.AssignedAnalystId is int id ? names.GetValueOrDefault(id, $"User #{id}") : null,
                canAssign, eligible.OrderBy(refusedUsers.Contains).ToList(), refusals);
        }).ToList();

        var eligibleAnywhere = tests.SelectMany(t => t.EligibleUserIds).ToHashSet();
        var refusedAnywhere = tests.SelectMany(t => t.CompetencyRefusals).Select(r => r.UserId).ToHashSet();
        var users = candidates
            .Where(c => eligibleAnywhere.Contains(c.Id))
            .OrderBy(c => refusedAnywhere.Contains(c.Id))
            .Select(c => new AssignableUserDto(c.Id, c.FullName, c.Username, c.Role!.Name, c.Role.Type.ToString()))
            .ToList();

        return new SampleAssignmentOptionsDto(sample.Id, tests, users);
    }

    private async Task<Sample> LoadSampleAsync(int sampleId, CancellationToken ct = default) =>
        await _db.Samples
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId, ct)
        ?? throw new NotFoundException(
            MessageCodes.SampleNotFound,
            $"Sample {sampleId} not found.",
            new Dictionary<string, string> { ["sampleId"] = sampleId.ToString() });

    // The one audit shape for a change of a test's analyst; Take and Release (TestSelfAssignmentService) write it too.
    // Added to the same DbContext as the assignment itself, so both commit or neither does.
    internal static AuditLog BuildAssignmentAudit(Sample sample, TestOrder order, string action, string previousValue, string newValue,
        int actingUserId, DateTime timestampUtc) => new()
    {
        EntityName = "TestOrder",
        EntityId = order.Id.ToString(),
        Action = action,
        PreviousValue = previousValue,
        NewValue = newValue,
        UserId = actingUserId,
        Timestamp = timestampUtc,
        SampleId = sample.Id,
        TestOrderId = order.Id,
        SampleReferenceNumber = sample.ReferenceNumber,
        BatchNumber = sample.BatchNumber,
        ControlNumber = sample.ControlNumber
    };

    // Writes the changed assignments with one GMP audit entry per test, so
    // each entry's previous value is that test's own previous analyst.
    internal async Task ApplyAsync(
        Sample sample,
        IReadOnlyList<(TestOrder Order, int? AnalystId)> changes,
        int actingUserId,
        string? reason,
        CancellationToken ct = default)
    {
        var changed = changes.Where(c => c.Order.AssignedAnalystId != c.AnalystId).ToList();

        var userIds = changed.SelectMany(c => new[] { c.Order.AssignedAnalystId, c.AnalystId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string Display(int? id) => id is int v ? names.GetValueOrDefault(v, $"User #{v}") : "Unassigned";

        var timestamp = _time.GetUtcNow().UtcDateTime;
        foreach (var (order, analystId) in changed)
        {
            var previousAnalystId = order.AssignedAnalystId;
            var action = previousAnalystId == null
                ? "AssignedAnalyst"
                : (analystId == null ? "UnassignedAnalyst" : "ReassignedAnalyst");
            var newValue = string.IsNullOrWhiteSpace(reason)
                ? Display(analystId)
                : $"{Display(analystId)} | Reason: {reason.Trim()}";

            _db.AuditLogs.Add(BuildAssignmentAudit(sample, order, action,
                $"{order.TestCode}: {Display(previousAnalystId)}", $"{order.TestCode}: {newValue}", actingUserId, timestamp));

            order.AssignedAnalystId = analystId;

            if (previousAnalystId is int prevId && prevId != analystId)
            {
                await _publisher.ResolveAsync($"TestAssigned:test:{order.Id}:user:{prevId}", ct);
                _db.QueueSignal(LiveSignalKinds.MyWork, $"user:{prevId}");
            }

            if (analystId is int assigneeId)
            {
                _db.QueueSignal(LiveSignalKinds.MyWork, $"user:{assigneeId}");
                if (assigneeId != actingUserId)
                {
                    await _publisher.PublishAsync(new NotificationRequest(
                        Type: "TestAssigned",
                        UserIds: new[] { assigneeId },
                        Message: $"Test {order.TestCode} for sample {sample.ReferenceNumber} was assigned to you.",
                        TargetType: "test-step",
                        TargetId: order.Id,
                        SectionId: order.SectionId,
                        DedupeKey: $"TestAssigned:test:{order.Id}:user:{assigneeId}",
                        SampleId: sample.Id,
                        TestOrderId: order.Id), ct);
                }
            }

            _db.QueueSignal(LiveSignalKinds.Counters, $"section:{order.SectionId}");
            _db.QueueSignal(LiveSignalKinds.Record, $"testOrder:{order.Id}");
            _db.QueueSignal(LiveSignalKinds.Record, $"sample:{sample.Id}");
        }

        if (changed.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private async Task<SampleDto> LoadDtoAsync(Sample sample)
    {
        var updatedSample = await _workspaceService.GetSampleAsync(sample.Id);
        return updatedSample ?? TestingWorkspaceService.ToDto(sample, nowUtc: _time.GetUtcNow().UtcDateTime);
    }
}
