using MicroLIMS.Shared.Exceptions;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Notifications;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Workflows;

// Disintegration results, including the staged retest.
public sealed class DisintegrationRecorder : TestWorkflowSupport
{
    public DisintegrationRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordDisintegrationResultAsync(
        int testOrderId, DisintegrationPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordDisintegrationResultCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordDisintegrationResultCoreAsync(
        int testOrderId, DisintegrationPayload payload, int userId, string? ipAddress = null)
    {
        if (payload.UnitMinutes == null)
            throw new InvalidOperationException("Unit minutes are required.");
        if (payload.UnitMinutes.Any(u => u.HasValue && u.Value <= 0m))
            throw new InvalidOperationException("Disintegration time must be greater than zero.");

        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, payload.AnalysedAt, payload.EquipmentId, null, payload.Password, WorkflowType.Disintegration, userId);

        int s1Units = definition.DisintegrationStage1Units ?? 6;
        int s2Units = definition.DisintegrationStage2Units ?? 12;
        int maxStage1Failures = definition.DisintegrationMaxStage1Failures ?? 2;
        int minPassTotal = definition.DisintegrationMinPassTotal ?? 16;
        var stageConfig = new DisintegrationStageConfig(s1Units, s2Units, maxStage1Failures, minPassTotal);

        if (payload.UnitMinutes.Count != s1Units)
            throw new InvalidOperationException($"Stage 1 disintegration requires exactly {s1Units} units.");

        var configuredLabels = string.IsNullOrWhiteSpace(definition.ConditionFields)
            ? Array.Empty<string>()
            : definition.ConditionFields.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

        if (configuredLabels.Length > 0)
        {
            if (payload.Conditions == null)
                throw new InvalidOperationException("Conditions are required.");

            if (payload.Conditions.Count != configuredLabels.Length)
                throw new InvalidOperationException($"Expected {configuredLabels.Length} condition fields, but received {payload.Conditions.Count}.");

            foreach (var label in configuredLabels)
            {
                if (!payload.Conditions.TryGetValue(label, out var val) || string.IsNullOrWhiteSpace(val))
                    throw new InvalidOperationException($"Condition field \"{label}\" is required.");
            }

            foreach (var key in payload.Conditions.Keys)
            {
                if (!configuredLabels.Contains(key))
                    throw new InvalidOperationException($"Unexpected condition field \"{key}\".");
            }
        }
        else
        {
            if (payload.Conditions != null && payload.Conditions.Count > 0)
                throw new InvalidOperationException("No condition fields are configured for this test.");
        }

        string? conditionsJson = payload.Conditions != null && payload.Conditions.Count > 0
            ? JsonSerializer.Serialize(payload.Conditions)
            : null;

        var spec = specById.Values.First();
        decimal limit = spec.UpperLimit!.Value;

        var evalResult = DisintegrationStageEvaluator.Evaluate(payload.UnitMinutes, limit, stageConfig);

        _db.CurrentUserId = userId;
        var signature = await _signatureService.SignAsync(
            userId,
            payload.Password,
            SignatureMeaning.ResultRecorded,
            "TestOrder",
            order.Id,
            payload.Comment,
            ipAddress);

        var canonicalLimit = !string.IsNullOrWhiteSpace(spec.SpecLimit)
            ? spec.SpecLimit
            : SpecificationService.BuildCanonicalSpecLimit(spec);

        ResultStatus comparisonStatus;
        string reportedDisplay;
        if (evalResult.Outcome == DissolutionStageOutcome.Complies)
        {
            comparisonStatus = ResultStatus.WithinLimits;
            reportedDisplay = "Complies";
        }
        else if (evalResult.Outcome == DissolutionStageOutcome.NextStageRequired)
        {
            comparisonStatus = ResultStatus.NextStageRequired;
            reportedDisplay = "Stage 2 required";
        }
        else
        {
            comparisonStatus = ResultStatus.OutOfSpecification;
            reportedDisplay = "Does not comply";
        }

        var unitDataList = new List<DisintegrationUnitData>();
        for (int i = 0; i < payload.UnitMinutes.Count; i++)
        {
            var m = payload.UnitMinutes[i];
            bool passed = m.HasValue && m.Value <= limit;
            unitDataList.Add(new DisintegrationUnitData(1, i + 1, m, passed));
        }

        var calcData = new DisintegrationCalculationData(
            LimitMinutes: limit,
            Config: new DisintegrationConfigData(s1Units, s2Units, maxStage1Failures, minPassTotal),
            Units: unitDataList,
            PassedCount: evalResult.PassedCount,
            LongestMinutes: evalResult.LongestMinutes,
            Outcome: evalResult.Outcome.ToString(),
            Reasons: evalResult.Reasons);

        string calcJson = JsonSerializer.Serialize(calcData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var paramResult = new ParameterResult
        {
            TestOrderId = order.Id,
            SpecificationId = spec.Id,
            ParameterName = spec.ParameterName,
            ReportedValue = evalResult.LongestMinutes,
            ReportedDisplay = reportedDisplay,
            Unit = spec.Unit,
            SpecLimit = canonicalLimit,
            ResultBasis = null,
            ComparisonStatus = comparisonStatus,
            OverRange = false,
            BelowLoq = false,
            CalculationJson = calcJson,
            StageReached = 1,
            IsActive = true
        };

        for (int i = 0; i < payload.UnitMinutes.Count; i++)
        {
            var m = payload.UnitMinutes[i];
            bool passed = m.HasValue && m.Value <= limit;
            paramResult.Readings.Add(new ResultReading
            {
                Kind = ReadingKind.Unit,
                Index = i + 1,
                Stage = 1,
                Value1 = m,
                Text = m.HasValue ? null : "Not disintegrated",
                ComputedValue = m,
                Passed = passed
            });
        }

        var entry = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.Disintegration,
            EquipmentId = payload.EquipmentId,
            AnalysedAt = analysedAtUtc,
            UnitAmount = null,
            SampleMatrix = null,
            ConditionsJson = conditionsJson,
            ValidityRecordType = null,
            ValidityRecordId = null,
            IsActive = true,
            EnteredByUserId = userId,
            EnteredAt = _clock.UtcNow.UtcDateTime,
            Signature = signature,
            Comment = payload.Comment,
            ParameterResults = new List<ParameterResult> { paramResult }
        };

        _db.TestAnalyses.Add(entry);
        await _db.SaveChangesAsync();

        var outcomeSummary = $"{paramResult.ParameterName}: {paramResult.ReportedDisplay}";

        if (evalResult.Outcome == DissolutionStageOutcome.NextStageRequired)
        {
            return new TestWorkflowResult(outcomeSummary, false, false, null, null, null, ResultStatus.NextStageRequired);
        }

        ResultStatus overallStatus = paramResult.ComparisonStatus;

        _db.Results.Add(new Result
        {
            TestOrderId = order.Id,
            RawValue = $"{paramResult.ParameterName}={(paramResult.ReportedValue.HasValue ? paramResult.ReportedValue.Value.ToString(CultureInfo.InvariantCulture) : paramResult.ReportedDisplay)}",
            InterpretedValue = $"{outcomeSummary} ({overallStatus})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId,
            EnteredAt = _clock.UtcNow.UtcDateTime
        });

        await _db.SaveChangesAsync();

        await _resultProjection.UpsertFromParameterResultAsync(paramResult.Id);
        await _db.SaveChangesAsync();

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Disintegration complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, null, null, overallStatus);
    }

    public Task<TestWorkflowResult> RecordDisintegrationStageAsync(
        int testOrderId, DisintegrationStagePayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordDisintegrationStageCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordDisintegrationStageCoreAsync(
        int testOrderId, DisintegrationStagePayload payload, int userId, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(payload.Password))
            throw new InvalidOperationException("Password is required to sign the result.");
        if (payload.UnitMinutes == null || payload.UnitMinutes.Count == 0)
            throw new InvalidOperationException("Unit minutes are required.");
        if (payload.UnitMinutes.Any(u => u.HasValue && u.Value <= 0m))
            throw new InvalidOperationException("Disintegration time must be greater than zero.");

        var order = await _db.TestOrders
            .Include(t => t.Results)
            .Include(t => t.Sample)
            .FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");

        RequireOrderNotFinalized(order);

        if (order.IsSuperseded)
            throw new InvalidOperationException("Cannot record result for a superseded test order.");

        var definition = await _db.TestDefinitions
            .FirstOrDefaultAsync(t => t.Code == order.TestCode)
            ?? throw new InvalidOperationException($"Test definition \"{order.TestCode}\" not found.");

        if (definition.WorkflowType != WorkflowType.Disintegration)
            throw new InvalidOperationException($"Test order {testOrderId} is not a Disintegration workflow.");

        await _sectionScope.EnsureTestOrderAccessAsync(userId, testOrderId);

        var entry = await _db.TestAnalyses
            .Include(e => e.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(e => e.TestOrderId == testOrderId && e.IsActive)
            ?? throw new InvalidOperationException("No active disintegration entry found for this test order.");

        var paramResult = entry.ParameterResults.FirstOrDefault(pr => pr.IsActive)
            ?? throw new InvalidOperationException("No active disintegration parameter result found.");

        if (paramResult.ComparisonStatus != ResultStatus.NextStageRequired)
            throw new InvalidOperationException("Active disintegration analysis does not require a next stage.");

        if (string.IsNullOrWhiteSpace(paramResult.CalculationJson))
            throw new InvalidOperationException("Active disintegration parameter result lacks calculation data.");

        var prevCalc = JsonSerializer.Deserialize<DisintegrationCalculationData>(
            paramResult.CalculationJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException("Failed to deserialize disintegration calculation data.");

        // Stage 2 is judged by the limit and stage rules snapshotted at stage 1, so a
        // Test Master or specification edit between the stages cannot change the rules.
        decimal limit = prevCalc.LimitMinutes;
        int s1Units = prevCalc.Config.Stage1Units;
        int s2Units = prevCalc.Config.Stage2Units;
        int maxStage1Failures = prevCalc.Config.MaxStage1Failures;
        int minPassTotal = prevCalc.Config.MinPassTotal;
        var stageConfig = new DisintegrationStageConfig(s1Units, s2Units, maxStage1Failures, minPassTotal);

        int existingUnitCount = paramResult.Readings.Count;
        if (existingUnitCount != s1Units)
            throw new InvalidOperationException($"Invalid existing unit count ({existingUnitCount}) for next stage.");

        if (payload.UnitMinutes.Count != s2Units)
            throw new InvalidOperationException($"Stage 2 disintegration requires exactly {s2Units} units.");

        var allUnits = new List<decimal?>();
        foreach (var r in paramResult.Readings.OrderBy(r => r.Index))
        {
            allUnits.Add(r.Value1);
        }
        allUnits.AddRange(payload.UnitMinutes);

        var evalResult = DisintegrationStageEvaluator.Evaluate(allUnits, limit, stageConfig);

        _db.CurrentUserId = userId;
        var signature = await _signatureService.SignAsync(
            userId,
            payload.Password,
            SignatureMeaning.ResultRecorded,
            "TestOrder",
            order.Id,
            payload.Comment,
            ipAddress);

        for (int i = 0; i < payload.UnitMinutes.Count; i++)
        {
            var m = payload.UnitMinutes[i];
            bool passed = m.HasValue && m.Value <= limit;
            paramResult.Readings.Add(new ResultReading
            {
                Kind = ReadingKind.Unit,
                Index = existingUnitCount + i + 1,
                Stage = 2,
                Value1 = m,
                Text = m.HasValue ? null : "Not disintegrated",
                ComputedValue = m,
                Passed = passed
            });
        }

        entry.Signature = signature;
        entry.EnteredAt = _clock.UtcNow.UtcDateTime;
        if (payload.Comment != null) entry.Comment = payload.Comment;

        var unitDataList = new List<DisintegrationUnitData>();
        foreach (var r in paramResult.Readings.OrderBy(r => r.Index))
        {
            unitDataList.Add(new DisintegrationUnitData(r.Stage ?? 1, r.Index, r.Value1, r.Passed ?? false));
        }

        var calcData = new DisintegrationCalculationData(
            LimitMinutes: limit,
            Config: new DisintegrationConfigData(s1Units, s2Units, maxStage1Failures, minPassTotal),
            Units: unitDataList,
            PassedCount: evalResult.PassedCount,
            LongestMinutes: evalResult.LongestMinutes,
            Outcome: evalResult.Outcome.ToString(),
            Reasons: evalResult.Reasons);

        paramResult.StageReached = evalResult.StageReached;
        paramResult.ReportedValue = evalResult.LongestMinutes;
        paramResult.CalculationJson = JsonSerializer.Serialize(calcData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        paramResult.ComparisonStatus = evalResult.Outcome == DissolutionStageOutcome.Complies ? ResultStatus.WithinLimits : ResultStatus.OutOfSpecification;
        paramResult.ReportedDisplay = evalResult.Outcome == DissolutionStageOutcome.Complies ? "Complies" : "Does not comply";

        await _db.SaveChangesAsync();

        var outcomeSummary = $"{paramResult.ParameterName}: {paramResult.ReportedDisplay}";
        ResultStatus overallStatus = paramResult.ComparisonStatus;

        _db.Results.Add(new Result
        {
            TestOrderId = order.Id,
            RawValue = $"{paramResult.ParameterName}={(paramResult.ReportedValue.HasValue ? paramResult.ReportedValue.Value.ToString(CultureInfo.InvariantCulture) : paramResult.ReportedDisplay)}",
            InterpretedValue = $"{outcomeSummary} ({overallStatus})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId,
            EnteredAt = _clock.UtcNow.UtcDateTime
        });

        await _db.SaveChangesAsync();

        await _resultProjection.UpsertFromParameterResultAsync(paramResult.Id);
        await _db.SaveChangesAsync();

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Disintegration complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, null, null, overallStatus);
    }
}
