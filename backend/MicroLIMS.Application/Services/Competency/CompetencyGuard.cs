using System.Globalization;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services.Competency;

public sealed class CompetencyGuard(IMicroLimsDbContext db, ILabClock clock, ItemSopResolver sopResolver) : ICompetencyGuard
{
    private static readonly CompetencyDecision Allowed = new(true, [], null, null);

    public async Task<CompetencyDecision> EvaluateAsync(
        int userId, int testOrderId, CancellationToken cancellationToken = default) =>
        (await EvaluateManyAsync(userId, [testOrderId], cancellationToken))[testOrderId];

    public Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateManyAsync(
        int userId, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default) =>
        EvaluateCoreAsync(userId, testOrderIds, assigneeWording: false, ignoreGate: false, cancellationToken);

    public Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateAssigneeAsync(
        int assigneeId, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default) =>
        EvaluateCoreAsync(assigneeId, testOrderIds, assigneeWording: true, ignoreGate: false, cancellationToken);

    public Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateIgnoringGateAsync(
        int userId, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default) =>
        EvaluateCoreAsync(userId, testOrderIds, assigneeWording: true, ignoreGate: true, cancellationToken);

    public async Task EnsureAllowedAsync(int userId, int testOrderId, CancellationToken cancellationToken = default) =>
        (await EvaluateAsync(userId, testOrderId, cancellationToken)).ToActionCheck().ThrowIfRefused();

    private async Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateCoreAsync(
        int userId, IReadOnlyCollection<int> testOrderIds, bool assigneeWording, bool ignoreGate, CancellationToken cancellationToken)
    {
        var decisions = await EvaluateOrdersAsync([userId], testOrderIds, assigneeWording, ignoreGate, cancellationToken);
        return decisions.ToDictionary(pair => pair.Key.TestOrderId, pair => pair.Value);
    }

    public Task<IReadOnlyDictionary<(int UserId, int TestOrderId), CompetencyDecision>> EvaluateCandidatesAsync(
        IReadOnlyCollection<int> userIds, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default) =>
        EvaluateOrdersAsync(userIds, testOrderIds, assigneeWording: true, ignoreGate: false, cancellationToken);

    public async Task<IReadOnlyDictionary<CompetencySubject, CompetencyDecision>> EvaluateSubjectsAsync(
        int userId, IReadOnlyCollection<CompetencySubject> subjects, bool assigneeWording,
        CancellationToken cancellationToken = default)
    {
        var (decisions, _) = await EvaluateSubjectsCoreAsync([userId], subjects, assigneeWording, ignoreGate: false, cancellationToken);
        return decisions.ToDictionary(pair => pair.Key.Subject, pair => pair.Value);
    }

    private async Task<IReadOnlyDictionary<(int UserId, int TestOrderId), CompetencyDecision>> EvaluateOrdersAsync(
        IReadOnlyCollection<int> userIds, IReadOnlyCollection<int> testOrderIds, bool assigneeWording, bool ignoreGate,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0 || testOrderIds.Count == 0)
            return new Dictionary<(int, int), CompetencyDecision>();

        var orders = await db.TestOrders.AsNoTracking()
            .Where(order => testOrderIds.Contains(order.Id))
            .Select(order => new { order.Id, Subject = new CompetencySubject(order.TestCode, order.SectionId,
                order.Sample == null ? null : order.Sample.ItemId) })
            .ToDictionaryAsync(order => order.Id, order => order.Subject, cancellationToken);

        foreach (var id in testOrderIds)
            if (!orders.ContainsKey(id))
                throw new NotFoundException(MessageCodes.TestOrderNotFound, $"Test order {id} not found.",
                    new Dictionary<string, string> { ["testOrderId"] = id.ToString(CultureInfo.InvariantCulture) });

        var (decisions, gated) = await EvaluateSubjectsCoreAsync(userIds, orders.Values, assigneeWording, ignoreGate, cancellationToken);
        var gatedOrderIds = orders.Where(order => gated.Contains(order.Value)).Select(order => order.Key).ToArray();
        var overrides = await LoadOverridesAsync(gatedOrderIds, cancellationToken);

        return (from userId in userIds.Distinct()
                from order in orders
                let decision = decisions[(userId, order.Value)]
                select (Key: (userId, order.Key), Decision: overrides.TryGetValue(order.Key, out var grant) && grant.AnalystId == userId
                    ? Allowed with { ViaOverrideId = grant.OverrideId, SopNotChecked = decision.SopNotChecked }
                    : decision))
            .ToDictionary(pair => pair.Key, pair => pair.Decision);
    }

    private async Task<Dictionary<int, (int AnalystId, int OverrideId)>> LoadOverridesAsync(
        int[] testOrderIds, CancellationToken cancellationToken)
    {
        if (testOrderIds.Length == 0)
            return [];

        return await db.CompetencyOverrides.AsNoTracking()
            .Where(row => testOrderIds.Contains(row.TestOrderId)
                && row.EndedAtUtc == null && !row.TestOrder.IsSuperseded
                && row.TestOrder.Status != ApprovalStatus.Reviewed && row.TestOrder.Status != ApprovalStatus.Approved
                && row.TestOrder.Status != ApprovalStatus.Rejected && row.TestOrder.Status != ApprovalStatus.Voided
                && row.TestOrder.Status != ApprovalStatus.Cancelled)
            .ToDictionaryAsync(row => row.TestOrderId, row => (row.AnalystUserId, row.Id), cancellationToken);
    }

    private async Task<(Dictionary<(int UserId, CompetencySubject Subject), CompetencyDecision> Decisions, HashSet<CompetencySubject> Gated)> EvaluateSubjectsCoreAsync(
        IReadOnlyCollection<int> userIds, IReadOnlyCollection<CompetencySubject> subjects, bool assigneeWording, bool ignoreGate,
        CancellationToken cancellationToken)
    {
        var uniqueSubjects = subjects.Distinct().ToArray();
        var decisions = (from userId in userIds.Distinct()
                         from subject in uniqueSubjects
                         select (userId, subject)).ToDictionary(key => key, _ => Allowed);
        if (decisions.Count == 0)
            return (decisions, []);

        var gated = uniqueSubjects;
        if (!ignoreGate)
        {
            var sectionIds = uniqueSubjects.Select(subject => subject.SectionId).Distinct().ToArray();
            var enabledSections = await db.CompetencySectionSettings.AsNoTracking()
                .Where(setting => sectionIds.Contains(setting.SectionId) && setting.GateEnabled)
                .Select(setting => setting.SectionId)
                .ToHashSetAsync(cancellationToken);
            gated = uniqueSubjects.Where(subject => enabledSections.Contains(subject.SectionId)).ToArray();
        }
        if (gated.Length == 0)
            return (decisions, []);

        var nowUtc = clock.UtcNow.UtcDateTime;
        var labToday = DateOnly.FromDateTime(clock.ToLabLocal(nowUtc));
        var testCodes = gated.Select(subject => subject.TestCode).Distinct().ToArray();
        var definitions = await db.TestDefinitions.AsNoTracking()
            .Where(definition => testCodes.Contains(definition.Code))
            .Select(definition => new { definition.Code, definition.Id })
            .ToDictionaryAsync(definition => definition.Code, definition => definition.Id, cancellationToken);
        var definitionIds = definitions.Values.ToArray();
        var qualifications = await db.AnalystQualifications.AsNoTracking()
            .Where(q => userIds.Contains(q.UserId) && definitionIds.Contains(q.TestDefinitionId))
            .ToListAsync(cancellationToken);
        var latest = qualifications.GroupBy(q => (q.UserId, q.TestDefinitionId))
            .ToDictionary(group => group.Key, group => group.MaxBy(q => q.Id));

        var itemIds = gated.Select(subject => subject.ItemId).OfType<int>().Distinct().ToArray();
        var sops = await sopResolver.ResolveManyAsync(itemIds, cancellationToken);
        var revisionIds = sops.Values.SelectMany(sop => new[] { sop.RevisionId, sop.PreviousSupersededRevisionId })
            .OfType<int>().Distinct().ToArray();
        var assignments = await LoadAssignmentsAsync(userIds, revisionIds, cancellationToken);
        var names = assigneeWording
            ? await db.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken)
            : null;

        foreach (var userId in userIds.Distinct())
        {
            var analyst = names?.GetValueOrDefault(userId, userId.ToString(CultureInfo.InvariantCulture));
            foreach (var subject in gated)
            {
                var qualification = definitions.TryGetValue(subject.TestCode, out var definitionId)
                    ? latest.GetValueOrDefault((userId, definitionId))
                    : null;
                var sop = subject.ItemId is int itemId ? sops[itemId] : null;
                decisions[(userId, subject)] = Decide(subject, qualification, sop, assignments, userId, analyst, labToday, nowUtc);
            }
        }

        return (decisions, gated.ToHashSet());
    }

    private CompetencyDecision Decide(CompetencySubject subject, AnalystQualification? qualification, SopResolution? sop,
        ILookup<(int UserId, int RevisionId), Assignment> assignments, int userId, string? analyst, DateOnly labToday, DateTime nowUtc)
    {
        var reasons = new List<CompetencyReason>(2);
        if (QualificationReason(qualification, subject.TestCode, labToday, analyst) is { } qualificationReason)
            reasons.Add(qualificationReason);

        CompetencyReason? sopNotChecked = null;
        if (sop is not null)
        {
            if (sop.Status != SopResolutionStatus.Effective)
                sopNotChecked = SopNotCheckedReason(sop);
            else if (!HasAcknowledged(sop, assignments, userId, nowUtc))
                reasons.Add(SopNotAcknowledgedReason(sop, analyst));
        }

        return new(reasons.Count == 0, reasons, sopNotChecked, null);
    }

    private sealed record Assignment(int UserId, int RevisionId, TrainingAssignmentStatus Status, DateTime DueDateUtc)
    {
        public bool Acknowledged => Status is TrainingAssignmentStatus.Acknowledged or TrainingAssignmentStatus.CompletedPassed;
        public bool IsOpen => Status is TrainingAssignmentStatus.Assigned or TrainingAssignmentStatus.Reading or TrainingAssignmentStatus.KafPending;
    }

    private async Task<ILookup<(int, int), Assignment>> LoadAssignmentsAsync(
        IReadOnlyCollection<int> userIds, int[] revisionIds, CancellationToken cancellationToken)
    {
        List<Assignment> assignments = [];
        if (revisionIds.Length > 0)
        {
            assignments = await db.DocumentTrainingAssignments.AsNoTracking()
                .Where(a => userIds.Contains(a.AssignedUserId) && revisionIds.Contains(a.DocumentRevisionId))
                .Select(a => new Assignment(a.AssignedUserId, a.DocumentRevisionId, a.Status, a.DueDateUtc))
                .ToListAsync(cancellationToken);
        }
        return assignments.ToLookup(assignment => (assignment.UserId, assignment.RevisionId));
    }

    private static bool HasAcknowledged(SopResolution sop, ILookup<(int, int), Assignment> assignments, int userId, DateTime nowUtc)
    {
        if (sop.RevisionId is not { } revisionId)
            return false;

        var current = assignments[(userId, revisionId)];
        return current.Any(assignment => assignment.Acknowledged)
            || (sop.PreviousSupersededRevisionId is { } previousId
                && assignments[(userId, previousId)].Any(assignment => assignment.Acknowledged)
                && current.Any(assignment => assignment.IsOpen && assignment.DueDateUtc >= nowUtc));
    }

    private CompetencyReason? QualificationReason(AnalystQualification? qualification, string test, DateOnly labToday, string? analyst)
    {
        var parameters = new Dictionary<string, string> { ["test"] = test };
        if (analyst is not null)
            parameters.Add("analyst", analyst);

        if (qualification is null)
            return analyst is null
                ? new(MessageCodes.CompetencyNotQualified, $"You are not qualified for {test}.", parameters)
                : new(MessageCodes.CompetencyAssigneeNotQualified, $"{analyst} is not qualified for {test}.", parameters);

        if (qualification.WithdrawnOn is { } withdrawnOn)
        {
            var date = FormatDate(DateOnly.FromDateTime(clock.ToLabLocal(withdrawnOn)));
            parameters.Add("date", date);
            return analyst is null
                ? new(MessageCodes.CompetencyQualificationWithdrawn, $"Your qualification for {test} was withdrawn on {date}.", parameters)
                : new(MessageCodes.CompetencyAssigneeQualificationWithdrawn, $"{analyst}'s qualification for {test} was withdrawn on {date}.", parameters);
        }

        if (qualification.IsCurrentOn(labToday))
            return null;

        if (qualification.ExpiresOn is { } expiresOn)
        {
            var date = FormatDate(expiresOn);
            parameters.Add("date", date);
            return analyst is null
                ? new(MessageCodes.CompetencyQualificationExpired, $"Your qualification for {test} expired on {date}.", parameters)
                : new(MessageCodes.CompetencyAssigneeQualificationExpired, $"{analyst}'s qualification for {test} expired on {date}.", parameters);
        }

        return null;
    }

    private static string FormatDate(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static CompetencyReason SopNotAcknowledgedReason(SopResolution sop, string? analyst)
    {
        var parameters = new Dictionary<string, string>
        {
            ["documentCode"] = sop.DocumentCode ?? string.Empty,
            ["revision"] = sop.RevisionNumber ?? string.Empty
        };
        if (analyst is null)
            return new(MessageCodes.CompetencySopNotAcknowledged,
                $"You have not acknowledged {sop.DocumentCode} revision {sop.RevisionNumber}.", parameters);

        parameters.Add("analyst", analyst);
        return new(MessageCodes.CompetencyAssigneeSopNotAcknowledged,
            $"{analyst} has not acknowledged {sop.DocumentCode} revision {sop.RevisionNumber}.", parameters);
    }

    private static CompetencyReason SopNotCheckedReason(SopResolution sop) => sop.Status switch
    {
        SopResolutionStatus.NoSopNumber => new(MessageCodes.CompetencySopNotCheckedNoSopNumber,
            "SOP not checked: the item has no SOP number.", new Dictionary<string, string>()),
        SopResolutionStatus.NotFound => new(MessageCodes.CompetencySopNotCheckedNotFound,
            $"SOP not checked: {sop.SopNumber} was not found in Document Control.",
            new Dictionary<string, string> { ["sopNumber"] = sop.SopNumber ?? string.Empty }),
        SopResolutionStatus.Ambiguous => new(MessageCodes.CompetencySopNotCheckedAmbiguous,
            $"SOP not checked: more than one active document matches {sop.SopNumber}.",
            new Dictionary<string, string> { ["sopNumber"] = sop.SopNumber ?? string.Empty }),
        _ => new(MessageCodes.CompetencySopNotCheckedNoEffectiveRevision,
            $"SOP not checked: {sop.DocumentCode} has no effective revision.",
            new Dictionary<string, string> { ["documentCode"] = sop.DocumentCode ?? string.Empty })
    };
}
