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

// ICP-OES elemental assay results against the calibration run.
public sealed class ElementalAssayRecorder : TestWorkflowSupport
{
    public ElementalAssayRecorder(TestWorkflowDependencies deps) : base(deps) { }

    public Task<TestWorkflowResult> RecordElementalAssayResultAsync(
        int testOrderId, ElementalAssayPayload payload, int userId, string? ipAddress = null) =>
        UnitOfWork.RunAsync(_db, () => RecordElementalAssayResultCoreAsync(testOrderId, payload, userId, ipAddress));

    private async Task<TestWorkflowResult> RecordElementalAssayResultCoreAsync(
        int testOrderId, ElementalAssayPayload payload, int userId, string? ipAddress = null)
    {
        if (payload.UnitAmount <= 0)
            throw new InvalidOperationException("Unit amount must be greater than 0.");
        if (string.IsNullOrWhiteSpace(payload.Password))
            throw new InvalidOperationException("Password is required to sign the result.");
        if (payload.Elements == null || payload.Elements.Count == 0)
            throw new InvalidOperationException("At least one elemental result is required.");

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var analysedAtUtc = payload.AnalysedAt.Kind switch
        {
            DateTimeKind.Utc => payload.AnalysedAt,
            DateTimeKind.Local => payload.AnalysedAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(payload.AnalysedAt, DateTimeKind.Utc)
        };

        if (analysedAtUtc > nowUtc.AddMinutes(5))
            throw new InvalidOperationException("Analysis time cannot be in the future.");

        foreach (var elem in payload.Elements)
        {
            if (elem.ReportedPpm < 0)
                throw new InvalidOperationException("Reported ppm cannot be negative.");
            if (elem.OverRange && elem.BelowLoq)
                throw new InvalidOperationException("An element cannot be both OverRange and BelowLoq.");
        }

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

        if (definition.WorkflowType != WorkflowType.ElementalAssay)
            throw new InvalidOperationException($"Test order {testOrderId} is not an Elemental Assay workflow.");

        await _sectionScope.EnsureTestOrderAccessAsync(userId, testOrderId);

        var existingActive = await _db.TestAnalyses.AnyAsync(e => e.TestOrderId == testOrderId && e.IsActive);
        if (existingActive)
            throw new InvalidOperationException("An active elemental assay entry already exists for this test order.");

        if (order.Sample?.ItemId is null)
            throw new InvalidOperationException("Test order sample must be linked to an item with specifications.");

        var itemId = order.Sample.ItemId.Value;

        var specs = await _db.Specifications
            .Include(s => s.TestAnalyte)
            .Where(s => s.ItemId == itemId && s.TestCode == order.TestCode)
            .ToListAsync();

        if (specs.Count == 0)
            throw new InvalidOperationException("No specifications configured for this test and item.");

        var matrices = specs.Select(s => s.SampleMatrix).Distinct().ToList();
        if (matrices.Count != 1 || matrices[0] == null)
            throw new InvalidOperationException("All specifications for this test and item must share the same sample matrix.");

        var sampleMatrix = matrices[0]!.Value;

        if (payload.Elements.Count != specs.Count)
            throw new InvalidOperationException($"Expected {specs.Count} element results matching specifications, but received {payload.Elements.Count}.");

        var specIds = specs.Select(s => s.Id).ToHashSet();
        var suppliedSpecIds = payload.Elements.Select(e => e.SpecificationId).ToList();
        if (suppliedSpecIds.Distinct().Count() != payload.Elements.Count || !specIds.SetEquals(suppliedSpecIds))
            throw new InvalidOperationException("Every specification for this test must be supplied exactly once.");

        var specById = specs.ToDictionary(s => s.Id);
        var elementResults = new List<ParameterResult>();
        int? equipmentId = null;

        foreach (var elemInput in payload.Elements)
        {
            var spec = specById[elemInput.SpecificationId];

            var runAnalyte = await _db.CalibrationRunAnalytes
                .Include(a => a.CalibrationRun)
                .FirstOrDefaultAsync(a => a.Id == elemInput.CalibrationRunAnalyteId)
                ?? throw new InvalidOperationException($"Calibration run analyte {elemInput.CalibrationRunAnalyteId} not found.");

            if (!runAnalyte.Passed)
                throw new InvalidOperationException($"Calibration run analyte {elemInput.CalibrationRunAnalyteId} did not pass calibration.");

            var run = runAnalyte.CalibrationRun
                ?? throw new InvalidOperationException($"Calibration run for analyte {elemInput.CalibrationRunAnalyteId} not found.");

            equipmentId = run.EquipmentId;

            if (run.Status != CalibrationRunStatus.Active)
                throw new InvalidOperationException($"Calibration run {run.Code} is not active.");

            if (run.TestDefinitionId != definition.Id)
                throw new InvalidOperationException("Linked calibration run is for a different test method.");

            if (run.SectionId != definition.SectionId)
                throw new InvalidOperationException("Linked calibration run is for a different laboratory section.");

            if (runAnalyte.TestAnalyteId != spec.TestAnalyteId)
                throw new InvalidOperationException($"Calibration run analyte {elemInput.CalibrationRunAnalyteId} does not match specification '{spec.ParameterName}'.");

            int maxAgeHours = definition.CalMaxRunAgeHours ?? 24;
            var runCalibrationAtUtc = run.CalibrationAt.Kind switch
            {
                DateTimeKind.Utc => run.CalibrationAt,
                DateTimeKind.Local => run.CalibrationAt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(run.CalibrationAt, DateTimeKind.Utc)
            };

            if (analysedAtUtc < runCalibrationAtUtc || analysedAtUtc > runCalibrationAtUtc.AddHours(maxAgeHours))
                throw new InvalidOperationException($"Analysis time must be within {maxAgeHours} hours of calibration run {run.Code}.");

            decimal? mgPerUnit = null;
            decimal? resultClaim = null;
            decimal? percentLabelClaim = null;
            decimal? reportedValue = null;
            string reportedDisplay;
            string status;

            if (elemInput.OverRange)
            {
                status = "RequiresReview";
                reportedDisplay = "Over range";
            }
            else if (elemInput.BelowLoq)
            {
                status = spec.LimitType == LimitType.NotMoreThan ? "WithinLimits" : "RequiresReview";
                reportedDisplay = "<LOQ";
            }
            else
            {
                decimal c = elemInput.ReportedPpm;
                decimal wuOrVd = payload.UnitAmount;
                decimal mpu = (c * wuOrVd) / 1000m;
                decimal rc = mpu * spec.ConversionFactor;
                decimal? plc = (spec.LabelClaim.HasValue && spec.LabelClaim.Value > 0)
                    ? (rc / spec.LabelClaim.Value) * 100m
                    : null;

                decimal rv = spec.ResultBasis switch
                {
                    ResultBasis.MgPerKg => c,
                    ResultBasis.MgPerUnit => rc,
                    ResultBasis.PercentLabelClaim => plc ?? throw new InvalidOperationException($"Label claim required to compute PercentLabelClaim for specification '{spec.ParameterName}'."),
                    _ => rc
                };

                mgPerUnit = mpu;
                resultClaim = rc;
                percentLabelClaim = plc;
                reportedValue = rv;

                status = SpecificationEvaluator.Evaluate(spec, rv);

                var rounded = Math.Round(rv, 1, MidpointRounding.AwayFromZero).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                if (spec.ResultBasis == ResultBasis.PercentLabelClaim)
                {
                    reportedDisplay = $"{rounded} %";
                }
                else if (spec.ResultBasis == ResultBasis.MgPerUnit)
                {
                    var unitStr = !string.IsNullOrWhiteSpace(spec.Unit) ? spec.Unit : (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit) ? spec.LabelClaimUnit : "mg");
                    reportedDisplay = $"{rounded} {unitStr}";
                }
                else // MgPerKg
                {
                    var unitStr = !string.IsNullOrWhiteSpace(spec.Unit) ? spec.Unit : (sampleMatrix == SampleMatrix.Solid ? "mg/kg" : "mg/L");
                    reportedDisplay = $"{rounded} {unitStr}";
                }
            }

            var canonicalLimit = !string.IsNullOrWhiteSpace(spec.SpecLimit)
                ? spec.SpecLimit
                : SpecificationService.BuildCanonicalSpecLimit(spec);

            var calcData = new ElementalCalculationData(
                Element: runAnalyte.Element,
                RunCode: run.Code,
                RunAnalytePassed: runAnalyte.Passed,
                ReportedPpm: elemInput.ReportedPpm,
                OverRange: elemInput.OverRange,
                BelowLoq: elemInput.BelowLoq,
                MgPerUnit: mgPerUnit,
                ResultClaim: resultClaim,
                PercentLabelClaim: percentLabelClaim,
                ConversionFactor: spec.ConversionFactor,
                LabelClaim: spec.LabelClaim,
                LabelClaimUnit: spec.LabelClaimUnit
            );

            var calcJson = System.Text.Json.JsonSerializer.Serialize(calcData, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            var paramResult = new ParameterResult
            {
                TestOrderId = order.Id,
                SpecificationId = spec.Id,
                ValidityRecordItemId = elemInput.CalibrationRunAnalyteId,
                ParameterName = spec.ParameterName,
                ReportedValue = reportedValue,
                ReportedDisplay = reportedDisplay,
                Unit = spec.Unit,
                SpecLimit = canonicalLimit,
                ResultBasis = spec.ResultBasis,
                ComparisonStatus = status,
                OverRange = elemInput.OverRange,
                BelowLoq = elemInput.BelowLoq,
                CalculationJson = calcJson,
                StageReached = null,
                IsActive = true
            };

            elementResults.Add(paramResult);
        }

        _db.CurrentUserId = userId;
        var signature = await _signatureService.SignAsync(
            userId,
            payload.Password,
            SignatureMeaning.ResultRecorded,
            "TestOrder",
            order.Id,
            payload.Comment,
            ipAddress);

        var entry = new TestAnalysis
        {
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.ElementalAssay,
            EquipmentId = equipmentId,
            AnalysedAt = analysedAtUtc,
            UnitAmount = payload.UnitAmount,
            SampleMatrix = sampleMatrix,
            ConditionsJson = null,
            ValidityRecordType = "CalibrationRun",
            ValidityRecordId = null,
            IsActive = true,
            EnteredByUserId = userId,
            EnteredAt = _clock.UtcNow.UtcDateTime,
            Signature = signature,
            Comment = payload.Comment,
            ParameterResults = elementResults
        };

        _db.TestAnalyses.Add(entry);

        string overallStatus;
        if (elementResults.Any(r => r.ComparisonStatus == "OutOfSpecification"))
            overallStatus = "OutOfSpecification";
        else if (elementResults.Any(r => r.ComparisonStatus == "RequiresReview"))
            overallStatus = "RequiresReview";
        else if (elementResults.Any(r => r.ComparisonStatus == "WithinLimits"))
            overallStatus = "WithinLimits";
        else
            overallStatus = elementResults.First().ComparisonStatus;

        var outcomeSummary = string.Join(", ", elementResults.Select(r => $"{r.Element}: {r.ReportedDisplay}"));

        _db.Results.Add(new Result
        {
            TestOrderId = order.Id,
            RawValue = string.Join(",", elementResults.Select(r => $"{r.Element}={(r.ReportedPpm.HasValue ? r.ReportedPpm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0")}")),
            InterpretedValue = $"{outcomeSummary} ({overallStatus})",
            Type = ResultType.Numeric,
            EnteredByUserId = userId,
            EnteredAt = _clock.UtcNow.UtcDateTime
        });

        await _db.SaveChangesAsync();

        foreach (var elemResult in elementResults)
        {
            await _resultProjection.UpsertFromParameterResultAsync(elemResult.Id);
        }
        await _db.SaveChangesAsync();

        await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, $"Elemental assay complete: {outcomeSummary}");
        await _sampleReviewService.AutoSubmitForReviewIfReadyAsync(order.SampleId, userId);
        await _db.SaveChangesAsync();

        return new TestWorkflowResult(outcomeSummary, true, true, outcomeSummary, null, null, overallStatus);
    }
}
