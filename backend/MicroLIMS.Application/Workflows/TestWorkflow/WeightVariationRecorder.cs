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

// Weight / content-uniformity variation results, including the staged
// retest.
public sealed class WeightVariationRecorder : TestWorkflowSupport
{
    public WeightVariationRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordWeightVariationResultAsync(
        int testOrderId, WeightVariationPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordWeightVariationResultCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordWeightVariationResultCoreAsync(
        int testOrderId, WeightVariationPayload payload, int userId, string? ipAddress = null)
    {
        if (payload.Units == null)
            throw new InvalidOperationException("Units are required.");

        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, payload.AnalysedAt, payload.EquipmentId, null, payload.Password, WorkflowType.WeightVariation, userId);

        var spec = specById.Values.First();
        if (!spec.DosageForm.HasValue)
            throw new InvalidOperationException("Specification dosage form is not configured.");

        var config = new WeightVariationConfig(
            UnitCount: definition.WvUnitCount ?? 20,
            TabletBand1MaxMg: definition.WvTabletBand1MaxMg ?? 130m,
            TabletBand1Percent: definition.WvTabletBand1Percent ?? 10m,
            TabletBand2MaxMg: definition.WvTabletBand2MaxMg ?? 324m,
            TabletBand2Percent: definition.WvTabletBand2Percent ?? 7.5m,
            TabletBand3Percent: definition.WvTabletBand3Percent ?? 5m,
            TabletMaxOutside: definition.WvTabletMaxOutside ?? 2,
            CapsuleInnerPercent: definition.WvCapsuleInnerPercent ?? 10m,
            CapsuleOuterPercent: definition.WvCapsuleOuterPercent ?? 25m,
            CapsuleS1MaxOutside: definition.WvCapsuleS1MaxOutside ?? 2,
            CapsuleS1MaxForRetest: definition.WvCapsuleS1MaxForRetest ?? 6,
            CapsuleS2ExtraUnits: definition.WvCapsuleS2ExtraUnits ?? 40,
            CapsuleS2MaxOutside: definition.WvCapsuleS2MaxOutside ?? 6);

        if (payload.Units.Count != config.UnitCount)
            throw new InvalidOperationException($"Stage 1 weight variation requires exactly {config.UnitCount} units.");

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

        var unitInputs = payload.Units
            .Select(u => new WeightVariationUnitInput(u.WeightMg, u.GrossMg, u.ShellMg))
            .ToList();

        var evalResult = WeightVariationEvaluator.Evaluate(spec.DosageForm.Value, unitInputs, config);

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

        var configData = new WeightVariationConfigData(
            config.UnitCount,
            config.TabletBand1MaxMg,
            config.TabletBand1Percent,
            config.TabletBand2MaxMg,
            config.TabletBand2Percent,
            config.TabletBand3Percent,
            config.TabletMaxOutside,
            config.CapsuleInnerPercent,
            config.CapsuleOuterPercent,
            config.CapsuleS1MaxOutside,
            config.CapsuleS1MaxForRetest,
            config.CapsuleS2ExtraUnits,
            config.CapsuleS2MaxOutside);

        var unitDataList = new List<WeightVariationUnitData>();
        for (int i = 0; i < evalResult.Units.Count; i++)
        {
            var u = evalResult.Units[i];
            unitDataList.Add(new WeightVariationUnitData(
                Stage: 1,
                UnitIndex: u.UnitIndex,
                WeightMg: u.WeightMg,
                GrossMg: u.GrossMg,
                ShellMg: u.ShellMg,
                NetMg: u.NetMg,
                DeviationPercent: u.DeviationPercent,
                Passed: u.Passed));
        }

        var calcData = new WeightVariationCalculationData(
            DosageForm: spec.DosageForm.Value,
            Config: configData,
            StepAPassed: evalResult.StepAPassed,
            MeanWeightMg: evalResult.MeanWeightMg,
            MeanGrossMg: evalResult.MeanGrossMg,
            BandPercentUsed: evalResult.BandPercentUsed,
            Units: unitDataList,
            Outcome: evalResult.Outcome.ToString(),
            Reasons: evalResult.Reasons);

        string calcJson = JsonSerializer.Serialize(calcData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var paramResult = new ParameterResult
        {
            TestOrderId = order.Id,
            SpecificationId = spec.Id,
            ParameterName = spec.ParameterName,
            ReportedValue = evalResult.MeanWeightMg,
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

        for (int i = 0; i < evalResult.Units.Count; i++)
        {
            var u = evalResult.Units[i];
            paramResult.Readings.Add(new ResultReading
            {
                Kind = ReadingKind.Unit,
                Index = u.UnitIndex,
                Stage = 1,
                Value1 = spec.DosageForm == DosageForm.Tablet ? u.WeightMg : u.GrossMg,
                Value2 = spec.DosageForm == DosageForm.Tablet ? null : u.ShellMg,
                ComputedValue = u.NetMg,
                Value3 = u.DeviationPercent,
                Passed = u.Passed
            });
        }

        var entry = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.WeightVariation,
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

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Weight variation complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, null, null, overallStatus);
    }

    public Task<TestWorkflowResult> RecordWeightVariationStageAsync(
        int testOrderId, WeightVariationStagePayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordWeightVariationStageCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordWeightVariationStageCoreAsync(
        int testOrderId, WeightVariationStagePayload payload, int userId, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(payload.Password))
            throw new InvalidOperationException("Password is required to sign the result.");
        if (payload.Units == null || payload.Units.Count == 0)
            throw new InvalidOperationException("Units are required.");

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

        if (definition.WorkflowType != WorkflowType.WeightVariation)
            throw new InvalidOperationException($"Test order {testOrderId} is not a WeightVariation workflow.");

        await _sectionScope.EnsureTestOrderAccessAsync(userId, testOrderId);

        var entry = await _db.TestAnalyses
            .Include(e => e.ParameterResults)
                .ThenInclude(pr => pr.Readings)
            .FirstOrDefaultAsync(e => e.TestOrderId == testOrderId && e.IsActive)
            ?? throw new InvalidOperationException("No active weight variation entry found for this test order.");

        var paramResult = entry.ParameterResults.FirstOrDefault(pr => pr.IsActive)
            ?? throw new InvalidOperationException("No active weight variation parameter result found.");

        if (paramResult.ComparisonStatus != ResultStatus.NextStageRequired)
            throw new InvalidOperationException("Active weight variation analysis does not require a next stage.");

        if (string.IsNullOrWhiteSpace(paramResult.CalculationJson))
            throw new InvalidOperationException("Active weight variation parameter result lacks calculation data.");

        var prevCalc = JsonSerializer.Deserialize<WeightVariationCalculationData>(
            paramResult.CalculationJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException("Failed to deserialize weight variation calculation data.");

        if (prevCalc.DosageForm == DosageForm.Tablet)
            throw new InvalidOperationException("Tablets do not support Stage 2 evaluation.");

        // Stage 2 is judged by the criteria and dosage form snapshotted at stage 1
        var config = new WeightVariationConfig(
            UnitCount: prevCalc.Config.UnitCount,
            TabletBand1MaxMg: prevCalc.Config.TabletBand1MaxMg,
            TabletBand1Percent: prevCalc.Config.TabletBand1Percent,
            TabletBand2MaxMg: prevCalc.Config.TabletBand2MaxMg,
            TabletBand2Percent: prevCalc.Config.TabletBand2Percent,
            TabletBand3Percent: prevCalc.Config.TabletBand3Percent,
            TabletMaxOutside: prevCalc.Config.TabletMaxOutside,
            CapsuleInnerPercent: prevCalc.Config.CapsuleInnerPercent,
            CapsuleOuterPercent: prevCalc.Config.CapsuleOuterPercent,
            CapsuleS1MaxOutside: prevCalc.Config.CapsuleS1MaxOutside,
            CapsuleS1MaxForRetest: prevCalc.Config.CapsuleS1MaxForRetest,
            CapsuleS2ExtraUnits: prevCalc.Config.CapsuleS2ExtraUnits,
            CapsuleS2MaxOutside: prevCalc.Config.CapsuleS2MaxOutside);

        int existingUnitCount = paramResult.Readings.Count;
        if (existingUnitCount != config.UnitCount)
            throw new InvalidOperationException($"Invalid existing unit count ({existingUnitCount}) for next stage.");

        if (payload.Units.Count != config.CapsuleS2ExtraUnits)
            throw new InvalidOperationException($"Stage 2 weight variation requires exactly {config.CapsuleS2ExtraUnits} units.");

        var allUnits = new List<WeightVariationUnitInput>();
        foreach (var r in paramResult.Readings.OrderBy(r => r.Index))
        {
            allUnits.Add(new WeightVariationUnitInput(WeightMg: null, GrossMg: r.Value1, ShellMg: r.Value2));
        }
        foreach (var u in payload.Units)
        {
            allUnits.Add(new WeightVariationUnitInput(u.WeightMg, u.GrossMg, u.ShellMg));
        }

        var evalResult = WeightVariationEvaluator.Evaluate(prevCalc.DosageForm, allUnits, config);

        _db.CurrentUserId = userId;
        var signature = await _signatureService.SignAsync(
            userId,
            payload.Password,
            SignatureMeaning.ResultRecorded,
            "TestOrder",
            order.Id,
            payload.Comment,
            ipAddress);

        // Update Stage 1 readings with new deviation and pass against 60-unit mean
        var stage1Readings = paramResult.Readings.OrderBy(r => r.Index).ToList();
        for (int i = 0; i < config.UnitCount; i++)
        {
            var r = stage1Readings[i];
            var evalUnit = evalResult.Units[i];
            r.Value3 = evalUnit.DeviationPercent;
            r.Passed = evalUnit.Passed;
        }

        // Append Stage 2 readings
        for (int i = 0; i < payload.Units.Count; i++)
        {
            int overallIndex = config.UnitCount + i;
            var evalUnit = evalResult.Units[overallIndex];
            paramResult.Readings.Add(new ResultReading
            {
                Kind = ReadingKind.Unit,
                Index = evalUnit.UnitIndex,
                Stage = 2,
                Value1 = evalUnit.GrossMg,
                Value2 = evalUnit.ShellMg,
                ComputedValue = evalUnit.NetMg,
                Value3 = evalUnit.DeviationPercent,
                Passed = evalUnit.Passed
            });
        }

        entry.Signature = signature;
        entry.EnteredAt = _clock.UtcNow.UtcDateTime;
        if (payload.Comment != null) entry.Comment = payload.Comment;

        var unitDataList = new List<WeightVariationUnitData>();
        for (int i = 0; i < evalResult.Units.Count; i++)
        {
            var u = evalResult.Units[i];
            int stage = i < config.UnitCount ? 1 : 2;
            unitDataList.Add(new WeightVariationUnitData(
                Stage: stage,
                UnitIndex: u.UnitIndex,
                WeightMg: u.WeightMg,
                GrossMg: u.GrossMg,
                ShellMg: u.ShellMg,
                NetMg: u.NetMg,
                DeviationPercent: u.DeviationPercent,
                Passed: u.Passed));
        }

        var calcData = new WeightVariationCalculationData(
            DosageForm: prevCalc.DosageForm,
            Config: prevCalc.Config,
            StepAPassed: prevCalc.StepAPassed,
            MeanWeightMg: evalResult.MeanWeightMg,
            MeanGrossMg: evalResult.MeanGrossMg,
            BandPercentUsed: evalResult.BandPercentUsed,
            Units: unitDataList,
            Outcome: evalResult.Outcome.ToString(),
            Reasons: evalResult.Reasons);

        paramResult.StageReached = evalResult.StageReached;
        paramResult.ReportedValue = evalResult.MeanWeightMg;
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

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Weight variation complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, null, null, overallStatus);
    }
}
