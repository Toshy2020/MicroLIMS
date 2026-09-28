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

// Standard-comparison assay (HPLC / titration) results against the
// reference standard.
public sealed class StandardComparisonRecorder : TestWorkflowSupport
{
    public StandardComparisonRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordStandardComparisonResultAsync(
        int testOrderId, StandardComparisonPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordStandardComparisonResultCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordStandardComparisonResultCoreAsync(
        int testOrderId, StandardComparisonPayload payload, int userId, string? ipAddress = null)
    {
        if (payload.Preparations == null || payload.Preparations.Count == 0)
            throw new InvalidOperationException("At least one sample preparation is required.");

        if (payload.Responses == null || payload.Responses.Count == 0)
            throw new InvalidOperationException("At least one response reading is required.");

        var (order, definition, specById, analysedAtUtc) = await ValidateTestAnalysisOrderAsync(
            testOrderId, payload.AnalysedAt, payload.EquipmentId, null, payload.Password, WorkflowType.StandardComparison, userId);

        var specs = specById.Values.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToList();

        // 1. Resolve Stage Replicates via StageReplicateResolver
        var replicateResolution = await StageReplicateResolver.ResolveForTestOrderAsync(_db, order.Id);
        if (!replicateResolution.IsConfigured)
            throw new InvalidOperationException(replicateResolution.Message ?? "Stage replicate configuration could not be resolved.");

        int expectedPreps = replicateResolution.SampleReplicates!.Value;
        if (payload.Preparations.Count != expectedPreps)
            throw new InvalidOperationException($"Expected exactly {expectedPreps} sample preparations for stage role {replicateResolution.StageRole}, but received {payload.Preparations.Count}.");

        // 2. Validate Preparations & Weigh-in Window
        for (int i = 0; i < payload.Preparations.Count; i++)
        {
            var p = payload.Preparations[i];
            if (p.TheoreticalWeightMg <= 0)
                throw new InvalidOperationException($"Sample preparation {i + 1} theoretical weight must be greater than zero.");
            if (p.ActualWeightMg <= 0)
                throw new InvalidOperationException($"Sample preparation {i + 1} actual weight must be greater than zero.");

            decimal deviation = (p.ActualWeightMg - p.TheoreticalWeightMg) / p.TheoreticalWeightMg * 100m;
            if (Math.Abs(deviation) > StandardComparisonCalculator.SampleWeighInTolerancePercent)
            {
                if (string.IsNullOrWhiteSpace(p.WeighInJustification))
                    throw new InvalidOperationException($"Weigh-in justification is required for sample preparation {i + 1} when actual weight is outside the ±{StandardComparisonCalculator.SampleWeighInTolerancePercent}% window.");
            }

            if (p.WeighInJustification?.Trim().Length > 1000)
                throw new InvalidOperationException($"Weigh-in justification for sample preparation {i + 1} must not exceed 1000 characters.");
        }

        // 3. Validate Linked System Suitability Run
        if (!order.SystemSuitabilityRunId.HasValue)
            throw new InvalidOperationException("Test order must be linked to a system suitability run before recording a standard-comparison result.");

        var run = await _db.SystemSuitabilityRuns
            .Include(r => r.Analytes)
                .ThenInclude(a => a.ReferenceStandardMaterial)
            .FirstOrDefaultAsync(r => r.Id == order.SystemSuitabilityRunId.Value)
            ?? throw new InvalidOperationException($"Linked system suitability run {order.SystemSuitabilityRunId.Value} not found.");

        if (!run.Passed)
            throw new InvalidOperationException("Linked system suitability run did not pass.");

        if (run.TestDefinitionId != definition.Id)
            throw new InvalidOperationException("Linked system suitability run is for a different test method.");

        if (run.SectionId != definition.SectionId)
            throw new InvalidOperationException("Linked system suitability run is for a different laboratory section.");

        // 4. Validate Responses (peak areas, or sample titres EP_test in mL for titration)
        bool isTitration = definition.ResponseMode == ResponseMode.TitrationVolume;
        var specAnalyteIds = specs.Select(s => s.TestAnalyteId!.Value).ToHashSet();
        if (payload.Responses.Any(r => !specAnalyteIds.Contains(r.TestAnalyteId)))
            throw new InvalidOperationException("Responses contain an analyte not configured in specifications.");

        foreach (var s in specs)
        {
            var analyteResponses = payload.Responses.Where(r => r.TestAnalyteId == s.TestAnalyteId!.Value).ToList();
            if (analyteResponses.Count != expectedPreps)
                throw new InvalidOperationException($"Expected exactly {expectedPreps} responses for analyte '{s.ParameterName}', but received {analyteResponses.Count}.");

            for (int p = 1; p <= expectedPreps; p++)
            {
                var matches = analyteResponses.Where(r => r.PreparationIndex == p).ToList();
                if (matches.Count != 1)
                    throw new InvalidOperationException($"Missing or duplicate response for analyte '{s.ParameterName}', preparation {p}.");
                if (matches[0].Response <= 0)
                    throw new InvalidOperationException($"{(isTitration ? "Titre" : "Response")} must be greater than zero for analyte '{s.ParameterName}', preparation {p}.");
            }
        }

        // 5. Calculate and store ParameterResults & ResultReadings
        var parameterResults = new List<ParameterResult>();

        foreach (var s in specs)
        {
            var runAnalyte = run.Analytes.FirstOrDefault(a => a.TestAnalyteId == s.TestAnalyteId!.Value);
            if (runAnalyte == null)
                throw new InvalidOperationException($"Linked system suitability run does not contain analyte '{s.ParameterName}'.");

            if (!runAnalyte.Passed)
                throw new InvalidOperationException($"Linked system suitability run analyte '{runAnalyte.AnalyteName}' did not pass.");

            if (runAnalyte.StandardMeanArea <= 0 || runAnalyte.StandardWeightMg <= 0 || runAnalyte.StandardPurityPercent <= 0)
                throw new InvalidOperationException($"Linked system suitability run analyte '{runAnalyte.AnalyteName}' contains invalid standard values.");

            if (!runAnalyte.TheoreticalWeightMg.HasValue || runAnalyte.TheoreticalWeightMg.Value <= 0)
                throw new InvalidOperationException($"Linked system suitability run analyte '{runAnalyte.AnalyteName}' has missing or invalid theoretical standard weight.");

            // Titration: EP_blank was titrated once with the standard and is carried on the run (SC-4).
            if (isTitration && !runAnalyte.BlankTitreMl.HasValue)
                throw new InvalidOperationException($"Linked system suitability run analyte '{runAnalyte.AnalyteName}' has no blank titre - it is not a titration run.");
            if (!isTitration && runAnalyte.BlankTitreMl.HasValue)
                throw new InvalidOperationException($"Linked system suitability run analyte '{runAnalyte.AnalyteName}' is a titration run, but this test measures peak area.");

            if (!runAnalyte.MoisturePercent.HasValue || runAnalyte.MoisturePercent.Value < 0 || runAnalyte.MoisturePercent.Value >= 100)
                throw new InvalidOperationException($"Linked system suitability run analyte '{runAnalyte.AnalyteName}' has missing or invalid moisture percent.");

            var analyteResponses = payload.Responses.Where(r => r.TestAnalyteId == s.TestAnalyteId!.Value).ToList();

            var calcResult = StandardComparisonCalculator.Calculate(
                analyteName: runAnalyte.AnalyteName,
                testAnalyteId: runAnalyte.TestAnalyteId,
                systemSuitabilityRunAnalyteId: runAnalyte.Id,
                standardTheoreticalWeightMg: runAnalyte.TheoreticalWeightMg.Value,
                standardActualWeightMg: runAnalyte.StandardWeightMg,
                standardPurityPercent: runAnalyte.StandardPurityPercent,
                moisturePercent: runAnalyte.MoisturePercent.Value,
                standardMeanArea: runAnalyte.StandardMeanArea,
                spec: s,
                preparations: payload.Preparations,
                responses: analyteResponses,
                maxPreparationRsdPercent: definition.HplcMaxPreparationRsdPercent,
                responseMode: definition.ResponseMode,
                blankTitreMl: isTitration ? runAnalyte.BlankTitreMl : null);

            var canonicalLimit = !string.IsNullOrWhiteSpace(s.SpecLimit)
                ? s.SpecLimit
                : SpecificationService.BuildCanonicalSpecLimit(s);

            var paramResult = new ParameterResult
            {
                TestOrderId = order.Id,
                SpecificationId = s.Id,
                ValidityRecordItemId = null,
                ParameterName = s.ParameterName,
                ReportedValue = calcResult.ReportedValue,
                ReportedDisplay = calcResult.ReportedDisplay,
                Unit = "%",
                SpecLimit = canonicalLimit,
                ResultBasis = null,
                ComparisonStatus = calcResult.ComparisonStatus,
                OverRange = false,
                BelowLoq = false,
                CalculationJson = calcResult.CalculationJson,
                StageReached = null,
                IsActive = true
            };

            foreach (var prepCalc in calcResult.Preparations)
            {
                paramResult.Readings.Add(new ResultReading
                {
                    Kind = isTitration ? ReadingKind.Titration : ReadingKind.Replicate,
                    Stage = prepCalc.PreparationIndex,
                    Index = 1,
                    Value1 = prepCalc.TestResponse,
                    ComputedValue = prepCalc.PercentAssay,
                    Passed = true
                });
            }

            parameterResults.Add(paramResult);
        }

        return await PersistTestAnalysisAndFinalizeAsync(
            order,
            WorkflowType.StandardComparison,
            payload.EquipmentId,
            analysedAtUtc,
            null,
            parameterResults,
            ResultType.Numeric,
            payload.Password,
            payload.Comment,
            "standard comparison",
            userId,
            ipAddress,
            validityRecordType: "SystemSuitabilityRun",
            validityRecordId: run.Id);
    }
}
