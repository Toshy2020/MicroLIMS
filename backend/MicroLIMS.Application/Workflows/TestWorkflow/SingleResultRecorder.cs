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

// Single-entry results: measurements, gravimetric determinations and
// qualitative (conforms / does not conform) tests.
public sealed class SingleResultRecorder : TestWorkflowSupport
{
    public SingleResultRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordMeasurementResultAsync(
        int testOrderId, MeasurementPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordMeasurementResultCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordMeasurementResultCoreAsync(
        int testOrderId, MeasurementPayload payload, int userId, string? ipAddress = null)
    {
        var suppliedSpecIds = payload.Parameters?.Select(p => p.SpecificationId).ToList() ?? new List<int>();
        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, payload.AnalysedAt, payload.EquipmentId, suppliedSpecIds, payload.Password, WorkflowType.Measurement, userId);

        if (!definition.ReplicateCount.HasValue || definition.ReplicateCount.Value < 1 || definition.ReplicateCount.Value > 30)
            throw new BusinessRuleException("Test definition does not have a valid replicate count (1-30).");

        if (!definition.EvaluationBasis.HasValue)
            throw new BusinessRuleException("Test definition does not have an evaluation basis configured.");

        int replicateCount = definition.ReplicateCount.Value;
        var evaluationBasis = definition.EvaluationBasis.Value;

        ArgumentNullException.ThrowIfNull(payload.Parameters);

        foreach (var param in payload.Parameters)
        {
            if (param.Readings == null || param.Readings.Count != replicateCount)
                throw new BusinessRuleException($"Each parameter must have exactly {replicateCount} readings.");
        }

        var parameterResults = new List<ParameterResult>();

        foreach (var param in payload.Parameters)
        {
            var spec = specById[param.SpecificationId];
            var calcResult = MeasurementCalculator.Calculate(param.Readings, evaluationBasis, spec);

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
                StageReached = null,
                IsActive = true
            };

            for (int i = 0; i < param.Readings.Count; i++)
            {
                paramResult.Readings.Add(new ResultReading
                {
                    Kind = ReadingKind.Replicate,
                    Index = i + 1,
                    Value1 = param.Readings[i]
                });
            }

            parameterResults.Add(paramResult);
        }

        return await PersistTestAnalysisAndFinalizeAsync(
            order,
            WorkflowType.Measurement,
            payload.EquipmentId,
            analysedAtUtc,
            null,
            parameterResults,
            ResultType.Numeric,
            payload.Password,
            payload.Comment,
            "Measurement",
            userId,
            ipAddress);
    }

    public Task<TestWorkflowResult> RecordGravimetricResultAsync(
        int testOrderId, GravimetricPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordGravimetricResultCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordGravimetricResultCoreAsync(
        int testOrderId, GravimetricPayload payload, int userId, string? ipAddress = null)
    {
        var suppliedSpecIds = payload.Parameters?.Select(p => p.SpecificationId).ToList() ?? new List<int>();
        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, payload.AnalysedAt, payload.EquipmentId, suppliedSpecIds, payload.Password, WorkflowType.Gravimetric, userId);

        if (definition.EquationType is not (EquationType.GravimetricLoss or EquationType.GravimetricResidue))
            throw new BusinessRuleException("Test definition equation type must be GravimetricLoss or GravimetricResidue.");

        if (!definition.ReplicateCount.HasValue || definition.ReplicateCount.Value < 1 || definition.ReplicateCount.Value > 30)
            throw new BusinessRuleException("Test definition does not have a valid replicate count (1-30).");

        int replicateCount = definition.ReplicateCount.Value;
        bool usesTare = definition.UsesTare ?? false;

        ArgumentNullException.ThrowIfNull(payload.Parameters);

        foreach (var param in payload.Parameters)
        {
            if (param.Replicates == null || param.Replicates.Count != replicateCount)
                throw new BusinessRuleException($"Each parameter must have exactly {replicateCount} replicates.");

            foreach (var rep in param.Replicates)
            {
                if (usesTare && !rep.Container.HasValue)
                    throw new BusinessRuleException("Container weight is required when tare is used.");
                if (!usesTare && rep.Container.HasValue)
                    throw new BusinessRuleException("Container weight must be null when tare is not used.");
            }
        }

        var configuredLabels = string.IsNullOrWhiteSpace(definition.ConditionFields)
            ? Array.Empty<string>()
            : definition.ConditionFields.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

        if (configuredLabels.Length > 0)
        {
            if (payload.Conditions == null)
                throw new BusinessRuleException("Conditions are required.");

            if (payload.Conditions.Count != configuredLabels.Length)
                throw new BusinessRuleException($"Expected {configuredLabels.Length} condition fields, but received {payload.Conditions.Count}.");

            foreach (var label in configuredLabels)
            {
                if (!payload.Conditions.TryGetValue(label, out var val) || string.IsNullOrWhiteSpace(val))
                    throw new BusinessRuleException($"Condition field \"{label}\" is required.");
            }

            foreach (var key in payload.Conditions.Keys)
            {
                if (!configuredLabels.Contains(key))
                    throw new BusinessRuleException($"Unexpected condition field \"{key}\".");
            }
        }
        else
        {
            if (payload.Conditions != null && payload.Conditions.Count > 0)
                throw new BusinessRuleException("No condition fields are configured for this test.");
        }

        string? conditionsJson = payload.Conditions != null && payload.Conditions.Count > 0
            ? JsonSerializer.Serialize(payload.Conditions)
            : null;

        var parameterResults = new List<ParameterResult>();

        foreach (var param in payload.Parameters)
        {
            var spec = specById[param.SpecificationId];
            var calcResult = GravimetricCalculator.Calculate(param.Replicates, definition.EquationType, spec);

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
                StageReached = null,
                IsActive = true
            };

            for (int i = 0; i < param.Replicates.Count; i++)
            {
                var rep = param.Replicates[i];
                var repDetail = calcResult.Replicates[i];
                paramResult.Readings.Add(new ResultReading
                {
                    Kind = ReadingKind.Weight,
                    Index = i + 1,
                    Value1 = rep.Container,
                    Value2 = rep.Initial,
                    Value3 = rep.Final,
                    ComputedValue = repDetail.Percent
                });
            }

            parameterResults.Add(paramResult);
        }

        return await PersistTestAnalysisAndFinalizeAsync(
            order,
            WorkflowType.Gravimetric,
            payload.EquipmentId,
            analysedAtUtc,
            conditionsJson,
            parameterResults,
            ResultType.Numeric,
            payload.Password,
            payload.Comment,
            "Gravimetric",
            userId,
            ipAddress);
    }

    // ReportedDisplay is varchar(100): fall back to a count when the list of failed containers would not fit.
    private static string PerContainerDisplay(int n, List<int> failed)
    {
        if (n == 1) return failed.Count == 0 ? "The container complies" : "The container does not comply";
        if (failed.Count == 0) return $"All {n} containers comply";
        if (failed.Count == 1) return $"Container {failed[0]} of {n} does not comply";
        var listed = $"Containers {string.Join(", ", failed)} of {n} do not comply";
        return listed.Length <= 100 ? listed : $"{failed.Count} of {n} containers do not comply";
    }

    public static (string Display, ResultStatus Status, string CanonicalLimit) BuildQualitativeOutcome(
        Specification spec, bool conforms)
    {
        var canonicalLimit = !string.IsNullOrWhiteSpace(spec.SpecLimit)
            ? spec.SpecLimit
            : SpecificationService.BuildCanonicalSpecLimit(spec);

        var display = conforms ? "Complies" : "Does not comply";
        var status = conforms ? ResultStatus.WithinLimits : ResultStatus.OutOfSpecification;
        return (display, status, canonicalLimit);
    }

    public Task<TestWorkflowResult> RecordQualitativeResultAsync(
        int testOrderId, QualitativePayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordQualitativeResultCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordQualitativeResultCoreAsync(
        int testOrderId, QualitativePayload payload, int userId, string? ipAddress = null)
    {
        var suppliedSpecIds = payload.Parameters?.Select(p => p.SpecificationId).ToList() ?? new List<int>();
        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, payload.AnalysedAt, payload.EquipmentId, suppliedSpecIds, payload.Password, WorkflowType.Qualitative, userId);

        if (definition.EquationType != EquationType.Qualitative)
            throw new BusinessRuleException("Test definition equation type must be Qualitative.");

        foreach (var spec in specById.Values)
        {
            if (spec.LimitType is not (LimitType.Qualitative or LimitType.PresenceAbsence))
                throw new BusinessRuleException($"Specification \"{spec.ParameterName}\" must have limit type Qualitative or PresenceAbsence, but was {spec.LimitType}.");
        }

        ArgumentNullException.ThrowIfNull(payload.Parameters);

        var perContainer = definition.PerContainer;
        var containerCount = payload.ContainerCount ?? 0;

        if (!perContainer && (payload.ContainerCount != null || payload.Parameters.Any(p => p.Containers is { Count: > 0 })))
            throw new BusinessRuleException("This test is not recorded per container.");

        if (perContainer && containerCount is < 1 or > 200)
            throw new BusinessRuleException("Enter the number of containers received (1 to 200).");

        foreach (var param in payload.Parameters)
        {
            if (param.Observation != null && param.Observation.Length > 500)
                throw new BusinessRuleException("Observation cannot exceed 500 characters.");

            if (perContainer)
            {
                // The conclusion is derived from the containers; the parameter-level Conforms is ignored.
                if (param.Containers == null
                    || !param.Containers.Select(c => c.Index).Order().SequenceEqual(Enumerable.Range(1, containerCount)))
                    throw new BusinessRuleException($"Record a result for each container 1 to {containerCount}.");

                foreach (var c in param.Containers)
                {
                    if (!c.Conforms && string.IsNullOrWhiteSpace(c.Observation))
                        throw new BusinessRuleException($"Describe what container {c.Index} shows.");

                    if (c.Observation != null && c.Observation.Length > 500)
                        throw new BusinessRuleException("Observation cannot exceed 500 characters.");
                }
                continue;
            }

            if (!param.Conforms && string.IsNullOrWhiteSpace(param.Observation))
                throw new BusinessRuleException("An observation is required when result does not conform.");
        }

        var parameterResults = new List<ParameterResult>();

        foreach (var param in payload.Parameters)
        {
            var spec = specById[param.SpecificationId];

            var failed = perContainer ? param.Containers!.Where(c => !c.Conforms).Select(c => c.Index).Order().ToList() : null;
            var conforms = perContainer ? failed!.Count == 0 : param.Conforms;
            var (defaultDisplay, status, canonicalLimit) = BuildQualitativeOutcome(spec, conforms);
            var display = perContainer ? PerContainerDisplay(containerCount, failed!) : defaultDisplay;

            var calcData = new QualitativeCalculationData(
                canonicalLimit, conforms, perContainer ? containerCount : null, failed);
            var calculationJson = JsonSerializer.Serialize(calcData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var paramResult = new ParameterResult
            {
                TestOrderId = order.Id,
                SpecificationId = spec.Id,
                ParameterName = spec.ParameterName,
                ReportedValue = null,
                ReportedDisplay = display,
                Unit = spec.Unit,
                SpecLimit = canonicalLimit,
                ResultBasis = null,
                ComparisonStatus = status,
                OverRange = false,
                BelowLoq = false,
                CalculationJson = calculationJson,
                StageReached = null,
                IsActive = true
            };

            if (perContainer)
            {
                foreach (var c in param.Containers!.OrderBy(c => c.Index))
                    paramResult.Readings.Add(new ResultReading { Kind = ReadingKind.Replicate, Index = c.Index, Text = c.Observation, Passed = c.Conforms });
            }
            else
            {
                paramResult.Readings.Add(new ResultReading
                {
                    Kind = ReadingKind.Replicate,
                    Index = 1,
                    Text = param.Observation,
                    Passed = param.Conforms
                });
            }

            parameterResults.Add(paramResult);
        }

        return await PersistTestAnalysisAndFinalizeAsync(
            order,
            WorkflowType.Qualitative,
            payload.EquipmentId,
            analysedAtUtc,
            null,
            parameterResults,
            ResultType.Interpretive,
            payload.Password,
            payload.Comment,
            "Qualitative",
            userId,
            ipAddress);
    }
}
