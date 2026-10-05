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

// Dissolution results, including the staged (S1/S2/S3) acceptance.
public sealed class DissolutionRecorder : TestWorkflowSupport
{
    public DissolutionRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordDissolutionResultAsync(
        int testOrderId, DissolutionPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordDissolutionResultCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordDissolutionResultCoreAsync(
        int testOrderId, DissolutionPayload payload, int userId, string? ipAddress = null)
    {
        if (payload.MediumVolumeMl <= 0m)
            throw new InvalidOperationException("Medium volume must be greater than zero.");
        if (payload.DilutionFactor.HasValue && payload.DilutionFactor.Value <= 0m)
            throw new InvalidOperationException("Dilution factor must be greater than zero.");
        if (payload.VesselAreas == null || payload.VesselAreas.Count != 6)
            throw new InvalidOperationException("Stage 1 dissolution requires exactly 6 vessel areas.");
        if (payload.VesselAreas.Any(a => a <= 0m))
            throw new InvalidOperationException("Vessel area must be greater than zero.");

        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, payload.AnalysedAt, payload.EquipmentId, null, payload.Password, WorkflowType.Dissolution, userId);

        var resolved = await HplcDissolutionStandard.ResolveAsync(_db, order.Id);
        var std = resolved.Standard ?? throw new InvalidOperationException(resolved.Problem);

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
        decimal q = spec.LowerLimit!.Value;
        decimal lc = spec.LabelClaim!.Value;
        decimal df = payload.DilutionFactor ?? 1.0m;
        decimal v = payload.MediumVolumeMl;

        decimal s1Offset = definition.DissolutionS1Offset ?? 5m;
        decimal s2MinOffset = definition.DissolutionS2MinOffset ?? 15m;
        decimal s3MinOffset = definition.DissolutionS3MinOffset ?? 25m;
        decimal s3MaxBelow = definition.DissolutionS3MaxBelowS2Min ?? 2m;

        var offsets = new DissolutionOffsetsData(s1Offset, s2MinOffset, s3MinOffset, s3MaxBelow);
        decimal cs = std.Cs;

        var standardData = new DissolutionStandardData(
            HplcRunId: std.HplcRunId,
            RunCode: std.RunCode,
            SstCode: std.SstCode,
            StandardWeightMg: std.StandardWeightMg,
            StandardDilution: std.StandardDilution,
            StandardPurityPercent: std.PurityPercent,
            StandardMoisturePercent: std.MoisturePercent,
            StandardMeanArea: std.MeanResponse,
            Cs: cs);

        var vesselInputs = payload.VesselAreas
            .Select((area, i) => (Stage: 1, VesselIndex: i + 1, Area: area))
            .ToList();

        var calcResult = DissolutionCalculator.Calculate(
            vesselInputs,
            standardData,
            v,
            df,
            lc,
            q,
            offsets);

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

        var paramResult = new ParameterResult
        {
            TestOrderId = order.Id,
            SpecificationId = spec.Id,
            ParameterName = spec.ParameterName,
            ReportedValue = calcResult.ReportedValue,
            ReportedDisplay = calcResult.ReportedDisplay,
            Unit = spec.Unit,
            SpecLimit = canonicalLimit,
            ResultBasis = null,
            ComparisonStatus = calcResult.ComparisonStatus,
            OverRange = false,
            BelowLoq = false,
            CalculationJson = calcResult.CalculationJson,
            StageReached = calcResult.StageReached,
            IsActive = true
        };

        for (int i = 0; i < calcResult.Vessels.Count; i++)
        {
            var vessel = calcResult.Vessels[i];
            paramResult.Readings.Add(new ResultReading
            {
                Kind = ReadingKind.Vessel,
                Index = vessel.VesselIndex,
                Stage = vessel.Stage,
                Value1 = vessel.Area,
                ComputedValue = vessel.Percent,
                Passed = vessel.Passed
            });
        }

        var entry = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.Dissolution,
            EquipmentId = payload.EquipmentId,
            AnalysedAt = analysedAtUtc,
            UnitAmount = null,
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

        if (calcResult.Outcome == DissolutionStageOutcome.NextStageRequired)
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

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Dissolution complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, null, null, overallStatus);
    }

    public Task<TestWorkflowResult> RecordDissolutionStageAsync(
        int testOrderId, DissolutionStagePayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordDissolutionStageCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordDissolutionStageCoreAsync(
        int testOrderId, DissolutionStagePayload payload, int userId, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(payload.Password))
            throw new InvalidOperationException("Password is required to sign the result.");
        if (payload.VesselAreas == null || payload.VesselAreas.Count == 0)
            throw new InvalidOperationException("Vessel areas are required.");
        if (payload.VesselAreas.Any(a => a <= 0m))
            throw new InvalidOperationException("Vessel area must be greater than zero.");

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

        if (definition.WorkflowType != WorkflowType.Dissolution)
            throw new InvalidOperationException($"Test order {testOrderId} is not a Dissolution workflow.");

        await _sectionScope.EnsureTestOrderAccessAsync(userId, testOrderId);

        var entry = await _db.TestAnalyses
            .Include(e => e.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(e => e.TestOrderId == testOrderId && e.IsActive)
            ?? throw new InvalidOperationException("No active dissolution entry found for this test order.");

        var paramResult = entry.ParameterResults.FirstOrDefault(pr => pr.IsActive)
            ?? throw new InvalidOperationException("No active dissolution parameter result found.");

        if (paramResult.ComparisonStatus != ResultStatus.NextStageRequired)
            throw new InvalidOperationException("Active dissolution analysis does not require a next stage.");

        int existingVesselCount = paramResult.Readings.Count;
        int nextStage;
        if (existingVesselCount == 6)
        {
            if (payload.VesselAreas.Count != 6)
                throw new InvalidOperationException("Stage 2 dissolution requires exactly 6 vessel areas.");
            nextStage = 2;
        }
        else if (existingVesselCount == 12)
        {
            if (payload.VesselAreas.Count != 12)
                throw new InvalidOperationException("Stage 3 dissolution requires exactly 12 vessel areas.");
            nextStage = 3;
        }
        else
        {
            throw new InvalidOperationException($"Invalid existing vessel count ({existingVesselCount}) for next stage.");
        }

        if (string.IsNullOrWhiteSpace(paramResult.CalculationJson))
            throw new InvalidOperationException("Active dissolution parameter result lacks calculation data.");

        var prevCalc = JsonSerializer.Deserialize<DissolutionCalculationData>(paramResult.CalculationJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException("Failed to deserialize dissolution calculation data.");

        var allVesselInputs = new List<(int Stage, int VesselIndex, decimal Area)>();
        foreach (var r in paramResult.Readings.OrderBy(r => r.Index))
        {
            allVesselInputs.Add((r.Stage ?? 1, r.Index, r.Value1 ?? 0m));
        }

        for (int i = 0; i < payload.VesselAreas.Count; i++)
        {
            allVesselInputs.Add((nextStage, existingVesselCount + i + 1, payload.VesselAreas[i]));
        }

        var calcResult = DissolutionCalculator.Calculate(
            allVesselInputs,
            prevCalc.Standard,
            prevCalc.V,
            prevCalc.Df,
            prevCalc.Lc,
            prevCalc.Q,
            prevCalc.Offsets);

        _db.CurrentUserId = userId;
        var signature = await _signatureService.SignAsync(
            userId,
            payload.Password,
            SignatureMeaning.ResultRecorded,
            "TestOrder",
            order.Id,
            payload.Comment,
            ipAddress);

        for (int i = existingVesselCount; i < calcResult.Vessels.Count; i++)
        {
            var v = calcResult.Vessels[i];
            paramResult.Readings.Add(new ResultReading
            {
                Kind = ReadingKind.Vessel,
                Index = v.VesselIndex,
                Stage = v.Stage,
                Value1 = v.Area,
                ComputedValue = v.Percent,
                Passed = v.Passed
            });
        }

        entry.Signature = signature;
        entry.EnteredAt = _clock.UtcNow.UtcDateTime;
        if (payload.Comment != null) entry.Comment = payload.Comment;

        paramResult.StageReached = calcResult.StageReached;
        paramResult.ReportedValue = calcResult.ReportedValue;
        paramResult.ReportedDisplay = calcResult.ReportedDisplay;
        paramResult.CalculationJson = calcResult.CalculationJson;
        paramResult.ComparisonStatus = calcResult.ComparisonStatus;

        await _db.SaveChangesAsync();

        var outcomeSummary = $"{paramResult.ParameterName}: {paramResult.ReportedDisplay}";

        if (calcResult.Outcome == DissolutionStageOutcome.NextStageRequired)
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

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Dissolution complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, null, null, overallStatus);
    }
}
