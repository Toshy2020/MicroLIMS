using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

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

    public SampleAssignmentService(IMicroLimsDbContext db, ITestWorkspaceService workspaceService, IUserSectionScopeService scope, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _workspaceService = workspaceService;
        _scope = scope;
    }

    private static readonly ApprovalStatus[] ClosedStatuses =
        { ApprovalStatus.Approved, ApprovalStatus.Rejected, ApprovalStatus.Voided, ApprovalStatus.Cancelled };

    private static bool IsOpen(TestOrder t) => !t.IsSuperseded && !ClosedStatuses.Contains(t.Status);

    // A null scope is a System Administrator, who reaches every section.
    private static bool InScope(IReadOnlyList<int>? scope, int sectionId) => scope is null || scope.Contains(sectionId);

    // Whole sample: the analyst takes every open test in the sections they
    // belong to (tests in other sections keep whoever is on them). A null
    // analyst unassigns every open test.
    public async Task<SampleDto> AssignAnalystAsync(int sampleId, int? analystUserId, int actingUserId, string? reason = null)
    {
        var sample = await LoadSampleAsync(sampleId);
        var actingScope = await _scope.GetAccessibleSectionIdsAsync(actingUserId);

        var open = sample.TestOrders.Where(t => IsOpen(t) && InScope(actingScope, t.SectionId)).ToList();
        if (open.Count == 0)
            throw new InvalidOperationException("This sample has no open tests you can assign.");

        var targets = open;
        if (analystUserId is int assigneeId)
        {
            var memberOf = await _scope.GetAccessibleSectionIdsAsync(assigneeId);
            targets = open.Where(t => InScope(memberOf, t.SectionId)).ToList();

            // With no test in the assignee's sections, checking against all
            // of them throws the "not a member" reason.
            await AnalystAssignmentRules.EnsureAssignableAsync(_db, _scope, assigneeId, actingUserId,
                (targets.Count > 0 ? targets : open).Select(t => t.SectionId), _time.GetUtcNow().UtcDateTime);
        }

        return await ApplyAsync(sample, targets.Select(t => (t, analystUserId)).ToList(), actingUserId, reason);
    }

    // Per test: each listed test gets its own analyst, all in one save.
    public async Task<SampleDto> AssignTestsAsync(int sampleId, IReadOnlyList<TestAnalystAssignment> assignments, int actingUserId, string? reason = null)
    {
        if (assignments.Count == 0)
            throw new InvalidOperationException("Choose at least one test to assign.");
        if (assignments.Select(a => a.TestOrderId).Distinct().Count() != assignments.Count)
            throw new InvalidOperationException("Each test can be listed only once.");

        var sample = await LoadSampleAsync(sampleId);
        var actingScope = await _scope.GetAccessibleSectionIdsAsync(actingUserId);
        var now = _time.GetUtcNow().UtcDateTime;

        var changes = new List<(TestOrder Order, int? AnalystId)>();
        foreach (var assignment in assignments)
        {
            var order = sample.TestOrders.FirstOrDefault(t => t.Id == assignment.TestOrderId)
                ?? throw new NotFoundException($"Test {assignment.TestOrderId} is not on sample {sample.ReferenceNumber}.");

            if (!IsOpen(order))
                throw new InvalidOperationException($"{order.TestCode} is closed - its analyst can no longer be changed.");
            if (!InScope(actingScope, order.SectionId))
                throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

            if (assignment.AnalystUserId is int assigneeId)
                await AnalystAssignmentRules.EnsureAssignableAsync(_db, _scope, assigneeId, actingUserId, new[] { order.SectionId }, now);

            changes.Add((order, assignment.AnalystUserId));
        }

        return await ApplyAsync(sample, changes, actingUserId, reason);
    }

    // Everything the Assign Analyst dialog needs: each test, whether the
    // acting user may change it, and who may take it.
    public async Task<SampleAssignmentOptionsDto> GetAssignmentOptionsAsync(int sampleId, int actingUserId)
    {
        var sample = await _db.Samples
            .AsNoTracking()
            .Include(s => s.TestOrders).ThenInclude(t => t.Section)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new NotFoundException($"Sample {sampleId} not found.");

        var actingScope = await _scope.GetAccessibleSectionIdsAsync(actingUserId);
        var now = _time.GetUtcNow().UtcDateTime;

        var candidates = await AnalystAssignmentRules.Candidates(_db, actingUserId, now)
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .ToListAsync();
        var sectionsByUser = new Dictionary<int, IReadOnlyList<int>?>();
        foreach (var candidate in candidates)
            sectionsByUser[candidate.Id] = await _scope.GetAccessibleSectionIdsAsync(candidate.Id);

        var orders = sample.TestOrders.Where(t => !t.IsSuperseded).OrderBy(t => t.SectionId).ThenBy(t => t.TestCode).ToList();
        var assignedIds = orders.Where(t => t.AssignedAnalystId.HasValue).Select(t => t.AssignedAnalystId!.Value).Distinct().ToList();
        var names = await _db.Users.Where(u => assignedIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        var tests = orders.Select(t =>
        {
            var canAssign = IsOpen(t) && InScope(actingScope, t.SectionId);
            var eligible = canAssign
                ? candidates.Where(c => InScope(sectionsByUser[c.Id], t.SectionId)).Select(c => c.Id).ToList()
                : new List<int>();
            return new AssignableTestDto(
                t.Id, t.TestCode, t.SectionId, t.Section?.Name ?? string.Empty, t.Status.ToString(),
                t.AssignedAnalystId,
                t.AssignedAnalystId is int id ? names.GetValueOrDefault(id, $"User #{id}") : null,
                canAssign, eligible);
        }).ToList();

        var eligibleAnywhere = tests.SelectMany(t => t.EligibleUserIds).ToHashSet();
        var users = candidates
            .Where(c => eligibleAnywhere.Contains(c.Id))
            .Select(c => new AssignableUserDto(c.Id, c.FullName, c.Username, c.Role!.Name, c.Role.Type.ToString()))
            .ToList();

        return new SampleAssignmentOptionsDto(sample.Id, tests, users);
    }

    private async Task<Sample> LoadSampleAsync(int sampleId) =>
        await _db.Samples
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
        ?? throw new NotFoundException($"Sample {sampleId} not found.");

    // Writes the changed assignments with one GMP audit entry per test, so
    // each entry's previous value is that test's own previous analyst.
    private async Task<SampleDto> ApplyAsync(Sample sample, IReadOnlyList<(TestOrder Order, int? AnalystId)> changes, int actingUserId, string? reason)
    {
        var changed = changes.Where(c => c.Order.AssignedAnalystId != c.AnalystId).ToList();

        var userIds = changed.SelectMany(c => new[] { c.Order.AssignedAnalystId, c.AnalystId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        string Display(int? id) => id is int v ? names.GetValueOrDefault(v, $"User #{v}") : "Unassigned";

        var timestamp = _time.GetUtcNow().UtcDateTime;
        foreach (var (order, analystId) in changed)
        {
            var action = order.AssignedAnalystId == null
                ? "AssignedAnalyst"
                : (analystId == null ? "UnassignedAnalyst" : "ReassignedAnalyst");
            var newValue = string.IsNullOrWhiteSpace(reason)
                ? Display(analystId)
                : $"{Display(analystId)} | Reason: {reason.Trim()}";

            _db.AuditLogs.Add(new AuditLog
            {
                EntityName = "TestOrder",
                EntityId = order.Id.ToString(),
                Action = action,
                PreviousValue = $"{order.TestCode}: {Display(order.AssignedAnalystId)}",
                NewValue = $"{order.TestCode}: {newValue}",
                UserId = actingUserId,
                Timestamp = timestamp,
                SampleId = sample.Id,
                TestOrderId = order.Id,
                SampleReferenceNumber = sample.ReferenceNumber,
                BatchNumber = sample.BatchNumber,
                ControlNumber = sample.ControlNumber
            });

            order.AssignedAnalystId = analystId;
        }

        if (changed.Count > 0)
            await _db.SaveChangesAsync();

        var updatedSample = await _workspaceService.GetSampleAsync(sample.Id);
        return updatedSample ?? TestingWorkspaceService.ToDto(sample, nowUtc: timestamp);
    }
}
