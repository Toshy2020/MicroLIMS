using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class SpecificationService
{
    private readonly MicroLimsDbContext _db;

    public SpecificationService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<Specification>> GetForItemAsync(int itemId) =>
        await _db.Specifications
            .Include(s => s.Stages)
            .Where(s => s.ItemId == itemId)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();

    public async Task<Specification> CreateAsync(Specification spec)
    {
        ApplyCanonicalSpecLimit(spec);
        await ValidateAsync(spec);
        _db.Specifications.Add(spec);
        await _db.SaveChangesAsync();
        return spec;
    }

    public static string BuildCanonicalSpecLimit(Specification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        return spec.LimitType switch
        {
            LimitType.Range => $"{spec.LowerLimit?.ToString(CultureInfo.InvariantCulture)}-{spec.UpperLimit?.ToString(CultureInfo.InvariantCulture)}",
            LimitType.NotMoreThan => $"NMT {spec.UpperLimit?.ToString(CultureInfo.InvariantCulture)}",
            LimitType.NotLessThan => $"NLT {spec.LowerLimit?.ToString(CultureInfo.InvariantCulture)}",
            LimitType.TargetWithTolerance => spec.ToleranceMode == ToleranceMode.Percent
                ? $"{spec.Target?.ToString(CultureInfo.InvariantCulture)} ± {spec.Tolerance?.ToString(CultureInfo.InvariantCulture)}%"
                : $"{spec.Target?.ToString(CultureInfo.InvariantCulture)} ± {spec.Tolerance?.ToString(CultureInfo.InvariantCulture)}",
            LimitType.Qualitative => spec.ExpectedResultText ?? string.Empty,
            LimitType.PresenceAbsence => spec.ExpectedState == ExpectedPresence.Presence ? "Present" : "Absent",
            LimitType.MultiStage => string.Empty,
            LimitType.CountTiered => spec.SpecLimit ?? string.Empty,
            LimitType.DissolutionQ => $"Q = {spec.LowerLimit?.ToString(CultureInfo.InvariantCulture)} %",
            LimitType.DisintegrationTime => $"NMT {spec.UpperLimit?.ToString(CultureInfo.InvariantCulture)} min",
            LimitType.WeightVariation => spec.DosageForm == DosageForm.Tablet
                ? "USP <2091>: tablets, limit by average weight"
                : "USP <2091>: net content 90-110 % of average",
            _ => spec.SpecLimit ?? string.Empty
        };
    }

    public static void ApplyCanonicalSpecLimit(Specification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.LimitType != LimitType.CountTiered)
        {
            spec.SpecLimit = BuildCanonicalSpecLimit(spec);
            spec.AlertLimit = string.Empty;
            spec.ActionLimit = string.Empty;
            spec.DilutionFactor = null;
        }
    }

    public void Validate(Specification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (string.IsNullOrWhiteSpace(spec.ParameterName))
            throw new InvalidOperationException("Parameter name is required.");

        if (spec.LimitType != LimitType.CountTiered && spec.DilutionFactor.HasValue)
            throw new InvalidOperationException("Dilution factor is only allowed for Count-Tiered specifications.");

        if (spec.LimitType != LimitType.WeightVariation && spec.DosageForm.HasValue)
            throw new InvalidOperationException("Dosage form is only allowed for WeightVariation specifications.");

        switch (spec.LimitType)
        {
            case LimitType.Range:
                if (!spec.LowerLimit.HasValue || !spec.UpperLimit.HasValue)
                    throw new InvalidOperationException("Lower limit and upper limit are required for Range specifications.");
                if (spec.LowerLimit.Value > spec.UpperLimit.Value)
                    throw new InvalidOperationException("Lower limit cannot be greater than upper limit.");
                break;

            case LimitType.NotMoreThan:
                if (!spec.UpperLimit.HasValue)
                    throw new InvalidOperationException("Upper limit is required for Not More Than specifications.");
                break;

            case LimitType.NotLessThan:
                if (!spec.LowerLimit.HasValue)
                    throw new InvalidOperationException("Lower limit is required for Not Less Than specifications.");
                break;

            case LimitType.TargetWithTolerance:
                if (!spec.Target.HasValue || !spec.Tolerance.HasValue)
                    throw new InvalidOperationException("Target and tolerance are required for Target With Tolerance specifications.");
                if (!spec.ToleranceMode.HasValue)
                    throw new InvalidOperationException("Tolerance mode is required for Target With Tolerance specifications.");
                if (spec.Tolerance.Value <= 0)
                    throw new InvalidOperationException("Tolerance must be greater than zero.");
                break;

            case LimitType.Qualitative:
                if (string.IsNullOrWhiteSpace(spec.ExpectedResultText))
                    throw new InvalidOperationException("Expected result text is required for Qualitative specifications.");
                break;

            case LimitType.PresenceAbsence:
                if (!spec.ExpectedState.HasValue)
                    throw new InvalidOperationException("Expected state is required for Presence/Absence specifications.");
                break;

            case LimitType.MultiStage:
                if (spec.Stages == null || spec.Stages.Count == 0)
                    throw new InvalidOperationException("At least one stage is required for Multi-Stage specifications.");
                if (spec.Stages.Any(s => string.IsNullOrWhiteSpace(s.StageLabel)))
                    throw new InvalidOperationException("Stage label is required for every stage.");
                if (spec.Stages.Any(s => string.IsNullOrWhiteSpace(s.AcceptanceCriteriaText)))
                    throw new InvalidOperationException("Acceptance criteria text is required for every stage.");
                break;

            case LimitType.DissolutionQ:
                if (!spec.LowerLimit.HasValue || spec.LowerLimit.Value <= 0 || spec.LowerLimit.Value > 100)
                    throw new InvalidOperationException("Lower limit (Q) must be between 0 and 100 (exclusive of 0, inclusive of 100) for DissolutionQ specifications.");
                if (!spec.LabelClaim.HasValue || spec.LabelClaim.Value <= 0)
                    throw new InvalidOperationException("Label claim must be greater than zero for DissolutionQ specifications.");
                if (string.IsNullOrWhiteSpace(spec.LabelClaimUnit) || spec.LabelClaimUnit.Trim() != "mg")
                    throw new InvalidOperationException("Label claim unit must be \"mg\" for DissolutionQ specifications.");
                break;

            case LimitType.DisintegrationTime:
                if (!spec.UpperLimit.HasValue || spec.UpperLimit.Value <= 0)
                    throw new InvalidOperationException("Upper limit (time in minutes) must be greater than zero for DisintegrationTime specifications.");
                if (string.IsNullOrWhiteSpace(spec.Unit) || spec.Unit.Trim() != "min")
                    throw new InvalidOperationException("Unit must be \"min\" for DisintegrationTime specifications.");
                if (spec.LabelClaim.HasValue)
                    throw new InvalidOperationException("Label claim is not allowed for DisintegrationTime specifications.");
                if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                    throw new InvalidOperationException("Label claim unit is not allowed for DisintegrationTime specifications.");
                break;

            case LimitType.WeightVariation:
                if (!spec.DosageForm.HasValue)
                    throw new InvalidOperationException("Dosage form is required for WeightVariation specifications.");
                if (string.IsNullOrWhiteSpace(spec.Unit) || spec.Unit.Trim() != "mg")
                    throw new InvalidOperationException("Unit must be \"mg\" for WeightVariation specifications.");
                if (spec.LowerLimit.HasValue || spec.UpperLimit.HasValue || spec.Target.HasValue || spec.Tolerance.HasValue)
                    throw new InvalidOperationException("Limits are not allowed for WeightVariation specifications (configured on Test Master).");
                if (spec.LabelClaim.HasValue)
                    throw new InvalidOperationException("Label claim is not allowed for WeightVariation specifications.");
                if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                    throw new InvalidOperationException("Label claim unit is not allowed for WeightVariation specifications.");
                break;

            case LimitType.CountTiered:
                break;

            default:
                throw new InvalidOperationException($"Unsupported limit type '{spec.LimitType}'.");
        }
    }

    public async Task ValidateAsync(Specification spec, CancellationToken cancellationToken = default)
    {
        Validate(spec);

        if (string.IsNullOrWhiteSpace(spec.TestCode))
            throw new InvalidOperationException("Test code is required.");

        var testAssigned = await _db.SampleTests.AnyAsync(
            st => st.ItemId == spec.ItemId && st.TestCode == spec.TestCode,
            cancellationToken);
        if (!testAssigned)
            throw new InvalidOperationException($"Test '{spec.TestCode}' is not assigned to item {spec.ItemId}.");

        var duplicate = await _db.Specifications.AnyAsync(
            s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ParameterName == spec.ParameterName && s.Id != spec.Id,
            cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A specification for parameter '{spec.ParameterName}' already exists for test '{spec.TestCode}' on this item.");

        var testDef = await _db.TestDefinitions
            .FirstOrDefaultAsync(t => t.Code == spec.TestCode, cancellationToken);

        if (spec.LimitType == LimitType.DissolutionQ)
        {
            if (testDef == null || testDef.WorkflowType != WorkflowType.Dissolution)
                throw new InvalidOperationException("DissolutionQ specifications are only allowed for Dissolution tests.");

            var duplicateDissolution = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.Id != spec.Id,
                cancellationToken);
            if (duplicateDissolution)
                throw new InvalidOperationException($"Only one specification is allowed for Dissolution test '{spec.TestCode}' on this item.");
        }
        else if (testDef?.WorkflowType == WorkflowType.Dissolution)
        {
            throw new InvalidOperationException("Only DissolutionQ specifications are allowed for Dissolution tests.");
        }

        if (spec.LimitType == LimitType.DisintegrationTime)
        {
            if (testDef == null || testDef.WorkflowType != WorkflowType.Disintegration)
                throw new InvalidOperationException("DisintegrationTime specifications are only allowed for Disintegration tests.");

            var duplicateDisintegration = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.Id != spec.Id,
                cancellationToken);
            if (duplicateDisintegration)
                throw new InvalidOperationException($"Only one specification is allowed for Disintegration test '{spec.TestCode}' on this item.");
        }
        else if (testDef?.WorkflowType == WorkflowType.Disintegration)
        {
            throw new InvalidOperationException("Only DisintegrationTime specifications are allowed for Disintegration tests.");
        }

        if (spec.LimitType == LimitType.WeightVariation)
        {
            if (testDef == null || testDef.WorkflowType != WorkflowType.WeightVariation)
                throw new InvalidOperationException("WeightVariation specifications are only allowed for WeightVariation tests.");

            var duplicateWeightVariation = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.Id != spec.Id,
                cancellationToken);
            if (duplicateWeightVariation)
                throw new InvalidOperationException($"Only one specification is allowed for WeightVariation test '{spec.TestCode}' on this item.");
        }
        else if (testDef?.WorkflowType == WorkflowType.WeightVariation)
        {
            throw new InvalidOperationException("Only WeightVariation specifications are allowed for WeightVariation tests.");
        }

        if (testDef?.EquationType == EquationType.CalibrationCurve)
        {
            if (!spec.TestAnalyteId.HasValue)
                throw new InvalidOperationException("Test analyte is required for Calibration Curve specifications.");

            var analyte = await _db.TestAnalytes
                .FirstOrDefaultAsync(a => a.Id == spec.TestAnalyteId.Value, cancellationToken);
            if (analyte == null || analyte.TestDefinitionId != testDef.Id)
                throw new InvalidOperationException($"Test analyte does not belong to test '{spec.TestCode}'.");

            var duplicateAnalyte = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.TestAnalyteId == spec.TestAnalyteId.Value && s.Id != spec.Id,
                cancellationToken);
            if (duplicateAnalyte)
                throw new InvalidOperationException($"A specification for this analyte already exists for test '{spec.TestCode}' on item {spec.ItemId}.");

            if (!spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is required for Calibration Curve specifications.");

            if (!spec.SampleMatrix.HasValue)
                throw new InvalidOperationException("Sample matrix is required for Calibration Curve specifications.");

            if (spec.LimitType != LimitType.Range &&
                spec.LimitType != LimitType.NotMoreThan &&
                spec.LimitType != LimitType.NotLessThan &&
                spec.LimitType != LimitType.TargetWithTolerance)
            {
                throw new InvalidOperationException($"Limit type '{spec.LimitType}' is not supported for Calibration Curve specifications. Must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance.");
            }

            if (spec.ConversionFactor <= 0)
                throw new InvalidOperationException("Conversion factor must be greater than zero.");

            if (spec.ResultBasis == ResultBasis.PercentLabelClaim)
            {
                if (!spec.LabelClaim.HasValue || spec.LabelClaim.Value <= 0)
                    throw new InvalidOperationException("Label claim must be greater than zero when result basis is PercentLabelClaim.");
            }
        }
        else if (testDef?.WorkflowType == WorkflowType.Dissolution || spec.LimitType == LimitType.DissolutionQ)
        {
            if (spec.TestAnalyteId.HasValue)
                throw new InvalidOperationException("Test analyte is only allowed for Calibration Curve specifications.");
            if (spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is only allowed for Calibration Curve specifications.");
            if (spec.SampleMatrix.HasValue)
                throw new InvalidOperationException("Sample matrix is only allowed for Calibration Curve specifications.");
            if (spec.ConversionFactor != 1.0m)
                throw new InvalidOperationException("Conversion factor must be 1.0 for Dissolution specifications.");
        }
        else if (testDef?.WorkflowType == WorkflowType.Disintegration || spec.LimitType == LimitType.DisintegrationTime)
        {
            if (spec.TestAnalyteId.HasValue)
                throw new InvalidOperationException("Test analyte is only allowed for Calibration Curve specifications.");
            if (spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is only allowed for Calibration Curve specifications.");
            if (spec.SampleMatrix.HasValue)
                throw new InvalidOperationException("Sample matrix is only allowed for Calibration Curve specifications.");
            if (spec.LabelClaim.HasValue)
                throw new InvalidOperationException("Label claim is not allowed for Disintegration specifications.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new InvalidOperationException("Label claim unit is not allowed for Disintegration specifications.");
            if (spec.ConversionFactor != 1.0m)
                throw new InvalidOperationException("Conversion factor must be 1.0 for Disintegration specifications.");
        }
        else if (testDef?.WorkflowType == WorkflowType.WeightVariation || spec.LimitType == LimitType.WeightVariation)
        {
            if (spec.TestAnalyteId.HasValue)
                throw new InvalidOperationException("Test analyte is only allowed for Calibration Curve specifications.");
            if (spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is only allowed for Calibration Curve specifications.");
            if (spec.SampleMatrix.HasValue)
                throw new InvalidOperationException("Sample matrix is only allowed for Calibration Curve specifications.");
            if (spec.LabelClaim.HasValue)
                throw new InvalidOperationException("Label claim is not allowed for WeightVariation specifications.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new InvalidOperationException("Label claim unit is not allowed for WeightVariation specifications.");
            if (spec.ConversionFactor != 1.0m)
                throw new InvalidOperationException("Conversion factor must be 1.0 for WeightVariation specifications.");
        }
        else
        {
            if (spec.TestAnalyteId.HasValue)
                throw new InvalidOperationException("Test analyte is only allowed for Calibration Curve specifications.");
            if (spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is only allowed for Calibration Curve specifications.");
            if (spec.SampleMatrix.HasValue)
                throw new InvalidOperationException("Sample matrix is only allowed for Calibration Curve specifications.");
            if (spec.LabelClaim.HasValue)
                throw new InvalidOperationException("Label claim is only allowed for Calibration Curve specifications.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new InvalidOperationException("Label claim unit is only allowed for Calibration Curve specifications.");
            if (spec.ConversionFactor != 1.0m)
                throw new InvalidOperationException("Conversion factor must be 1.0 for non-Calibration Curve specifications.");
        }
    }

    // Backend performs alert/action/spec comparison - frontend only displays results.
    public string CompareAgainstLimits(decimal value, Specification spec) =>
        SpecLimitParser.CompareAgainstLimits(value, spec.AlertLimit, spec.ActionLimit, spec.SpecLimit);
}
