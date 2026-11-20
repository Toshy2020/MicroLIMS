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
        EvaluateCoreAsync(userId, testOrderIds, assigneeWording: false, cancellationToken);

    public Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateAssigneeAsync(
        int assigneeId, IReadOnlyCollection<int> testOrderIds, CancellationToken cancellationToken = default) =>
        EvaluateCoreAsync(assigneeId, testOrderIds, assigneeWording: true, cancellationToken);

    public async Task EnsureAllowedAsync(int userId, int testOrderId, CancellationToken cancellationToken = default) =>
        (await EvaluateAsync(userId, testOrderId, cancellationToken)).ToActionCheck().ThrowIfRefused();

    private async Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateCoreAsync(
        int userId, IReadOnlyCollection<int> testOrderIds, bool assigneeWording, CancellationToken cancellationToken)
    {
        var decisions = new Dictionary<int, CompetencyDecision>();
        if (testOrderIds.Count == 0)
            return decisions;

        var nowUtc = clock.UtcNow.UtcDateTime;
        var labToday = DateOnly.FromDateTime(clock.ToLabLocal(nowUtc));
        var orders = await db.TestOrders.AsNoTracking()
            .Where(order => testOrderIds.Contains(order.Id))
            .Select(order => new { order.Id, order.SectionId, order.TestCode, ItemId = order.Sample == null ? null : order.Sample.ItemId })
            .ToDictionaryAsync(order => order.Id, cancellationToken);

        foreach (var id in testOrderIds)
            if (!orders.ContainsKey(id))
                throw new NotFoundException(MessageCodes.TestOrderNotFound, $"Test order {id} not found.",
                    new Dictionary<string, string> { ["testOrderId"] = id.ToString(CultureInfo.InvariantCulture) });

        var sectionIds = orders.Values.Select(order => order.SectionId).Distinct().ToArray();
        var enabledSections = await db.CompetencySectionSettings.AsNoTracking()
            .Where(setting => sectionIds.Contains(setting.SectionId) && setting.GateEnabled)
            .Select(setting => setting.SectionId)
            .ToHashSetAsync(cancellationToken);
        var gated = orders.Values.Where(order => enabledSections.Contains(order.SectionId)).ToArray();
        foreach (var order in orders.Values)
            decisions.Add(order.Id, Allowed);
        if (gated.Length == 0)
            return decisions;

        var testCodes = gated.Select(order => order.TestCode).Distinct().ToArray();
        var definitions = await db.TestDefinitions.AsNoTracking()
            .Where(definition => testCodes.Contains(definition.Code))
            .Select(definition => new { definition.Code, definition.Id })
            .ToDictionaryAsync(definition => definition.Code, definition => definition.Id, cancellationToken);
        var definitionIds = definitions.Values.ToArray();
        var qualifications = await db.AnalystQualifications.AsNoTracking()
            .Where(q => q.UserId == userId && definitionIds.Contains(q.TestDefinitionId))
            .ToListAsync(cancellationToken);
        var latestByDefinition = qualifications.GroupBy(q => q.TestDefinitionId)
            .ToDictionary(group => group.Key, group => group.MaxBy(q => q.Id));

        var itemIds = gated.Select(order => order.ItemId).OfType<int>().Distinct().ToArray();
        var sops = await sopResolver.ResolveManyAsync(itemIds, cancellationToken);
        var revisionIds = sops.Values.SelectMany(sop => new[] { sop.RevisionId, sop.PreviousSupersededRevisionId })
            .OfType<int>().Distinct().ToArray();
        var assignments = await LoadAssignmentsAsync(userId, revisionIds, cancellationToken);
        string? analyst = null;
        if (assigneeWording)
        {
            analyst = await db.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => user.FullName)
                .SingleOrDefaultAsync(cancellationToken) ?? userId.ToString(CultureInfo.InvariantCulture);
        }

        foreach (var order in gated)
        {
            var reasons = new List<CompetencyReason>(2);
            var qualification = definitions.TryGetValue(order.TestCode, out var definitionId)
                ? latestByDefinition.GetValueOrDefault(definitionId)
                : null;
            if (QualificationReason(qualification, order.TestCode, labToday, analyst) is { } qualificationReason)
                reasons.Add(qualificationReason);

            CompetencyReason? sopNotChecked = null;
            if (order.ItemId is { } itemId)
            {
                var sop = sops[itemId];
                if (sop.Status != SopResolutionStatus.Effective)
                    sopNotChecked = SopNotCheckedReason(sop);
                else if (!HasAcknowledged(sop, assignments, nowUtc))
                    reasons.Add(SopNotAcknowledgedReason(sop, analyst));
            }

            decisions[order.Id] = new(reasons.Count == 0, reasons, sopNotChecked, null);
        }

        return decisions;
    }

    private sealed record Assignment(int RevisionId, TrainingAssignmentStatus Status, DateTime DueDateUtc)
    {
        public bool Acknowledged => Status is TrainingAssignmentStatus.Acknowledged or TrainingAssignmentStatus.CompletedPassed;
        public bool IsOpen => Status is TrainingAssignmentStatus.Assigned or TrainingAssignmentStatus.Reading or TrainingAssignmentStatus.KafPending;
    }

    private async Task<ILookup<int, Assignment>> LoadAssignmentsAsync(
        int userId, int[] revisionIds, CancellationToken cancellationToken)
    {
        List<Assignment> assignments = [];
        if (revisionIds.Length > 0)
        {
            assignments = await db.DocumentTrainingAssignments.AsNoTracking()
                .Where(a => a.AssignedUserId == userId && revisionIds.Contains(a.DocumentRevisionId))
                .Select(a => new Assignment(a.DocumentRevisionId, a.Status, a.DueDateUtc))
                .ToListAsync(cancellationToken);
        }
        return assignments.ToLookup(assignment => assignment.RevisionId);
    }

    private static bool HasAcknowledged(SopResolution sop, ILookup<int, Assignment> assignments, DateTime nowUtc)
    {
        if (sop.RevisionId is not { } revisionId)
            return false;

        var current = assignments[revisionId];
        return current.Any(assignment => assignment.Acknowledged)
            || (sop.PreviousSupersededRevisionId is { } previousId
                && assignments[previousId].Any(assignment => assignment.Acknowledged)
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
