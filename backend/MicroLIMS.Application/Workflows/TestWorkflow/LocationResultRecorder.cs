using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Workflows;

public sealed class LocationResultRecorder : TestWorkflowSupport
{
    public LocationResultRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordLocationResultsAsync(int testOrderId, List<LocationResultInput> inputs, int userId) =>
        UnitOfWork.RunAsync(_db, () => RecordLocationResultsCoreAsync(testOrderId, inputs, userId));

    private async Task<TestWorkflowResult> RecordLocationResultsCoreAsync(int testOrderId, List<LocationResultInput> inputs, int userId)
    {
        await _sectionScope.EnsureTestOrderAccessAsync(userId, testOrderId);

        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        RequireOrderNotFinalized(order);
        if (order.Status is ApprovalStatus.Voided or ApprovalStatus.Cancelled or ApprovalStatus.Rejected or ApprovalStatus.Approved or ApprovalStatus.Reviewed or ApprovalStatus.ResultEntered)
            throw new InvalidOperationException($"Test order {order.Id} is not open (status: {order.Status}).");

        var sampleLocations = await _db.SampleLocations
            .Include(l => l.SamplingConfiguration!).ThenInclude(c => c.WaterSamplingPoint)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart!).ThenInclude(p => p.Machine)
            .Include(l => l.WaterSamplingPoint)
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Where(l => l.TestOrderId == testOrderId)
            .ToListAsync();

        if (sampleLocations.Count == 0)
            throw new InvalidOperationException("No locations are assigned to this test order.");

        foreach (var loc in sampleLocations)
        {
            if (loc.SamplingConfiguration == null && loc.SamplingConfigurationId != null)
            {
                loc.SamplingConfiguration = await _db.SamplingConfigurations
                    .Include(c => c.WaterSamplingPoint)
                    .FirstOrDefaultAsync(c => c.Id == loc.SamplingConfigurationId);
            }
            else if (loc.SamplingConfiguration == null && loc.WaterSamplingPointId != null)
            {
                loc.SamplingConfiguration = await _db.SamplingConfigurations
                    .Include(c => c.WaterSamplingPoint)
                    .FirstOrDefaultAsync(c => c.WaterSamplingPointId == loc.WaterSamplingPointId && c.TestCode == order.TestCode);
                if (loc.SamplingConfiguration != null)
                    loc.SamplingConfigurationId = loc.SamplingConfiguration.Id;
            }

            if (loc.MachinePartConfiguration == null && loc.MachinePartConfigurationId != null)
            {
                loc.MachinePartConfiguration = await _db.MachinePartConfigurations
                    .Include(c => c.MachinePart!).ThenInclude(p => p.Machine)
                    .FirstOrDefaultAsync(c => c.Id == loc.MachinePartConfigurationId);
            }
        }

        if (inputs is null || inputs.Count == 0)
            throw new InvalidOperationException("Results are missing for all locations.");

        var duplicateInputs = inputs.GroupBy(i => i.SampleLocationId).Where(g => g.Count() > 1).ToList();
        if (duplicateInputs.Count > 0)
            throw new InvalidOperationException($"Duplicate inputs provided for location ID(s): {string.Join(", ", duplicateInputs.Select(g => g.Key))}.");

        var submitted = inputs.ToDictionary(i => i.SampleLocationId);
        var missing = sampleLocations.Where(l => !submitted.ContainsKey(l.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Results are missing for: {string.Join(", ", missing.Select(LocationName))}.");

        var extra = inputs.Where(i => !sampleLocations.Any(l => l.Id == i.SampleLocationId)).ToList();
        if (extra.Count > 0)
            throw new InvalidOperationException($"Input contains location ID(s) that do not belong to this test order: {string.Join(", ", extra.Select(i => i.SampleLocationId))}.");

        var worstStatus = ResultStatus.WithinLimits;
        var conformCount = 0;

        foreach (var location in sampleLocations)
        {
            var pointName = LocationName(location);
            var limitType = location.SamplingConfiguration?.LimitType ?? location.MachinePartConfiguration?.LimitType;
            if (!limitType.HasValue)
                throw new InvalidOperationException($"Limits for {order.TestCode} at {pointName} are not configured.");

            var input = submitted[location.Id];
            decimal? lowerLimit = location.SamplingConfiguration?.LowerLimit ?? location.MachinePartConfiguration?.LowerLimit;
            decimal? upperLimit = location.SamplingConfiguration?.UpperLimit ?? location.MachinePartConfiguration?.UpperLimit;
            bool lowerInclusive = location.SamplingConfiguration?.LowerInclusive ?? location.MachinePartConfiguration?.LowerInclusive ?? true;
            bool upperInclusive = location.SamplingConfiguration?.UpperInclusive ?? location.MachinePartConfiguration?.UpperInclusive ?? true;
            string? expectedResultText = location.SamplingConfiguration?.ExpectedResultText ?? location.MachinePartConfiguration?.ExpectedResultText;
            string unit = location.SamplingConfiguration?.Unit ?? location.MachinePartConfiguration?.Unit ?? string.Empty;

            ResultStatus status;
            string specLimitSnapshot;

            if (limitType.Value == LimitType.Qualitative)
            {
                if (string.IsNullOrWhiteSpace(input.Text))
                    throw new InvalidOperationException($"Text result is required for {pointName}.");

                var text = input.Text.Trim();
                var conforms = string.Equals(text, expectedResultText?.Trim(), StringComparison.OrdinalIgnoreCase);
                status = conforms ? ResultStatus.WithinLimits : ResultStatus.OutOfSpecification;
                specLimitSnapshot = expectedResultText?.Trim() ?? string.Empty;

                location.CalculatedResult = null;
                location.ReportedResult = text;
            }
            else
            {
                if (!input.Value.HasValue)
                    throw new InvalidOperationException($"Numeric value is required for {pointName}.");

                var spec = new Specification
                {
                    LimitType = limitType.Value,
                    LowerLimit = lowerLimit,
                    UpperLimit = upperLimit,
                    LowerInclusive = lowerInclusive,
                    UpperInclusive = upperInclusive
                };
                status = SpecificationEvaluator.Evaluate(spec, input.Value.Value);
                specLimitSnapshot = SpecificationService.BuildCanonicalSpecLimit(spec);

                location.CalculatedResult = input.Value.Value;
                location.ReportedResult = !string.IsNullOrWhiteSpace(input.Text)
                    ? input.Text.Trim()
                    : input.Value.Value.ToString(CultureInfo.InvariantCulture);
            }

            location.Status = status;
            location.SpecLimit = specLimitSnapshot;
            location.Unit = unit;
            location.EnteredAt = _clock.UtcNow.UtcDateTime;
            location.EnteredByUserId = userId;

            if (status == ResultStatus.WithinLimits) conformCount++;
            if (StatusSeverity(status) > StatusSeverity(worstStatus)) worstStatus = status;
        }

        var summary = $"{sampleLocations.Count} locations: {conformCount} conform, {sampleLocations.Count - conformCount} alert/action/spec";

        _db.Results.Add(new Result
        {
            TestOrderId = testOrderId,
            RawValue = summary,
            InterpretedValue = $"{summary} (worst: {worstStatus})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId
        });

        var openIncubation = await _db.Incubations.FirstOrDefaultAsync(i => i.TestOrderId == testOrderId && i.CompletedAt == null);
        if (openIncubation != null)
        {
            openIncubation.CompletedAt = _clock.UtcNow.UtcDateTime;
            openIncubation.CompletedByUserId = userId;
            openIncubation.Outcome = summary;
        }

        foreach (var location in sampleLocations)
            await _resultProjection.UpsertFromSampleLocationAsync(location.Id);

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Location results recorded: {summary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(summary, true, true, summary, null, null, worstStatus);
    }

    private static string LocationName(SampleLocation l) =>
        l.WaterSamplingPoint?.Code
        ?? l.SamplingConfiguration?.WaterSamplingPoint?.Code
        ?? l.MachinePartConfiguration?.MachinePart?.Name
        ?? l.RoomTestConfiguration?.Room?.Name
        ?? $"Location {l.Id}";

    private static int StatusSeverity(ResultStatus status) => status switch
    {
        ResultStatus.OutOfSpecification => 4,
        ResultStatus.ActionLimitExceeded => 3,
        ResultStatus.AlertLimitExceeded => 2,
        ResultStatus.LimitsNotConfigured => 1,
        _ => 0
    };
}
