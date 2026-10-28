using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

public class SpecificationService
{
    private readonly IMicroLimsDbContext _db;

    public SpecificationService(IMicroLimsDbContext db)
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

        if (spec.ResultBasis == ResultBasis.Ppm && !spec.HplcMethodAnalyteId.HasValue)
            throw new InvalidOperationException("ppm is only used for residual-solvent specifications.");

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

    // Trims and collapses inner whitespace; blank = ungraded (null). BP and USP
    // are stored upper-case, any other grade (e.g. In-house) as typed.
    public static string? NormalizeGrade(string? grade)
    {
        if (string.IsNullOrWhiteSpace(grade)) return null;
        var g = string.Join(' ', grade.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (g.Length > 20) throw new InvalidOperationException("Grade must be 20 characters or fewer.");
        return g.ToUpperInvariant() is "BP" or "USP" ? g.ToUpperInvariant() : g;
    }

    // Default parameter name of a related-substance row when none is typed.
    public static string RelatedSubstanceParameterName(RelatedSubstanceRow kind, string? impurityName) => kind switch
    {
        RelatedSubstanceRow.Named => impurityName ?? "Named impurity",
        RelatedSubstanceRow.UnspecifiedEach => "Any unspecified impurity",
        RelatedSubstanceRow.Total => "Total impurities",
        _ => "Total of others",
    };

    private static string RelatedSubstanceDuplicateLabel(RelatedSubstanceRow kind, string? impurityName) => kind switch
    {
        RelatedSubstanceRow.Named => $"named impurity '{impurityName}'",
        RelatedSubstanceRow.UnspecifiedEach => "any unspecified impurity",
        RelatedSubstanceRow.Total => "total impurities",
        _ => "total of others",
    };

    private async Task ValidateRelatedSubstanceRowAsync(Specification spec, int? methodId, CancellationToken cancellationToken)
    {
        if (spec.RelatedSubstanceRow is not RelatedSubstanceRow kind || !Enum.IsDefined(kind))
            throw new InvalidOperationException("Choose the related-substance row: named impurity, any unspecified impurity, total, or total of others.");

        if (kind == RelatedSubstanceRow.Named)
        {
            if (!spec.HplcMethodImpurityId.HasValue)
                throw new InvalidOperationException("Choose the impurity for this row.");
            var impurityMethodId = await _db.HplcMethodImpurities.Where(i => i.Id == spec.HplcMethodImpurityId.Value)
                .Select(i => (int?)i.HplcMethodId).FirstOrDefaultAsync(cancellationToken);
            if (impurityMethodId != methodId)
                throw new InvalidOperationException("The impurity does not belong to this test's HPLC method.");
        }
        else if (spec.HplcMethodImpurityId.HasValue)
            throw new InvalidOperationException("Only a named-impurity row names an impurity.");

        if (spec.LimitType != LimitType.NotMoreThan)
            throw new InvalidOperationException("Related-substance limits are 'not more than' limits.");
        if (spec.ResultBasis != ResultBasis.PercentAsIs)
            throw new InvalidOperationException("Related-substance rows report % (as is).");
        if (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
            throw new InvalidOperationException("Related-substance rows have no label claim.");
    }

    public async Task ValidateAsync(Specification spec, CancellationToken cancellationToken = default)
    {
        if (spec.RelatedSubstanceRow.HasValue && spec.LimitType == LimitType.NotMoreThan && !(spec.UpperLimit > 0m))
            throw new InvalidOperationException("Enter the limit (above 0 %).");
        Validate(spec);
        spec.Grade = NormalizeGrade(spec.Grade);
        // Grades match case-insensitively ("In-house" = "in-house"); reuse the item's
        // existing spelling so exact-match lookups see one value per item.
        var grade = spec.Grade?.ToLower();
        if (grade is not null)
        {
            var existing = await _db.Specifications
                .Where(s => s.ItemId == spec.ItemId && s.Id != spec.Id && s.Grade != null && s.Grade.ToLower() == grade)
                .Select(s => s.Grade).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) spec.Grade = existing;
        }

        if (string.IsNullOrWhiteSpace(spec.TestCode))
            throw new InvalidOperationException("Test code is required.");

        var testAssigned = await _db.SampleTests.AnyAsync(
            st => st.ItemId == spec.ItemId && st.TestCode == spec.TestCode,
            cancellationToken);
        if (!testAssigned)
            throw new InvalidOperationException($"Test '{spec.TestCode}' is not assigned to item {spec.ItemId}.");

        // A physicochemical test only takes specs for the item categories of its area.
        var testArea = await _db.TestDefinitions.Where(t => t.Code == spec.TestCode).Select(t => t.PhyschemArea).FirstOrDefaultAsync(cancellationToken);
        if (testArea is not null)
        {
            var itemCategory = await _db.Items.Where(i => i.Id == spec.ItemId).Select(i => (SampleCategory?)i.Category).FirstOrDefaultAsync(cancellationToken);
            if (itemCategory is SampleCategory cat && !PhyschemAreas.Includes(testArea, PhyschemAreas.OfCategory(cat)))
                throw new InvalidOperationException(PhyschemAreas.OfCategory(cat) == WorkspaceArea.Fp
                    ? $"Test {spec.TestCode} is not used for finished products - it belongs to RM & PM."
                    : $"Test {spec.TestCode} is not used for raw and packaging materials - it belongs to FP.");
        }

        // Production stages exist only on Finished Product samples, so a
        // stage-specific row on any other item could never be used.
        if (spec.ProductionStageRole is ProductionStageRole role)
        {
            if (role == ProductionStageRole.Other)
                throw new InvalidOperationException("Choose Bulk, In-Process, Finished or Stability, or leave the stage as all stages.");
            var category = await _db.Items.Where(i => i.Id == spec.ItemId).Select(i => (SampleCategory?)i.Category).FirstOrDefaultAsync(cancellationToken);
            if (category != SampleCategory.FinishedProduct)
                throw new InvalidOperationException("Stage-specific specifications are only available for Finished Product items.");
        }

        // Method-analyte rows are keyed by analyte + basis; checked first so the
        // clearer message wins over the parameter-name duplicate below.
        if (spec.HplcMethodAnalyteId is int methodAnalyteId && spec.RelatedSubstanceRow is null)
        {
            var duplicateAnalyteBasis = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.HplcMethodAnalyteId == methodAnalyteId
                     && s.ResultBasis == spec.ResultBasis && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id
                     && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
                cancellationToken);
            if (duplicateAnalyteBasis)
                throw new InvalidOperationException("A specification for this analyte and basis already exists.");
        }

        if (spec.IcpMethodElementId is int icpElementId)
        {
            var duplicateElementBasis = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.IcpMethodElementId == icpElementId
                     && s.ResultBasis == spec.ResultBasis && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id
                     && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
                cancellationToken);
            if (duplicateElementBasis)
                throw new InvalidOperationException("A specification for this element and basis already exists.");
        }

        // Related-substance rows are keyed by kind (+ impurity for a Named row) per stage and grade;
        // that key replaces the parameter-name check, so rows may share a parameter name.
        if (spec.RelatedSubstanceRow is RelatedSubstanceRow kind)
        {
            var relatedDuplicate = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.RelatedSubstanceRow == kind
                     && s.HplcMethodImpurityId == spec.HplcMethodImpurityId
                     && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id
                     && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
                cancellationToken);
            if (relatedDuplicate)
            {
                string? impurityName = null;
                if (kind == RelatedSubstanceRow.Named && spec.HplcMethodImpurityId is int impurityId)
                    impurityName = await _db.HplcMethodImpurities.Where(i => i.Id == impurityId).Select(i => i.Name).FirstOrDefaultAsync(cancellationToken);
                throw new InvalidOperationException($"A {RelatedSubstanceDuplicateLabel(kind, impurityName)} row already exists for this test, stage and grade.");
            }
        }

        var duplicate = spec.RelatedSubstanceRow is null && await _db.Specifications.AnyAsync(
            s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ParameterName == spec.ParameterName
                 && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id
                 && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
            cancellationToken);
        if (duplicate)
            throw new InvalidOperationException(spec.Grade is not null
                ? "A specification for this parameter and grade already exists."
                : $"A specification for parameter '{spec.ParameterName}' already exists for test '{spec.TestCode}' at {StageText(spec.ProductionStageRole)} on this item.");

        var testDef = await _db.TestDefinitions
            .FirstOrDefaultAsync(t => t.Code == spec.TestCode, cancellationToken);

        if (spec.LimitType == LimitType.DissolutionQ)
        {
            if (testDef == null || testDef.WorkflowType != WorkflowType.Dissolution)
                throw new InvalidOperationException("DissolutionQ specifications are only allowed for Dissolution tests.");

            var duplicateDissolution = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id,
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
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id,
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
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id,
                cancellationToken);
            if (duplicateWeightVariation)
                throw new InvalidOperationException($"Only one specification is allowed for WeightVariation test '{spec.TestCode}' on this item.");
        }
        else if (testDef?.WorkflowType == WorkflowType.WeightVariation)
        {
            throw new InvalidOperationException("Only WeightVariation specifications are allowed for WeightVariation tests.");
        }

        // HPLC chain S3: specification rows for an HplcMethodAssay test are
        // keyed by method analyte + quantity, reusing ResultBasis as the
        // quantity (PercentLabelClaim = assay %, MgPerUnit = amount per unit;
        // spec 3.4). No new enum.
        if (testDef?.WorkflowType != WorkflowType.HplcMethodAssay
            && (spec.RelatedSubstanceRow.HasValue || spec.HplcMethodImpurityId.HasValue))
            throw new InvalidOperationException("Related-substance rows belong to related-substances methods only.");

        if (testDef?.WorkflowType == WorkflowType.HplcMethodAssay)
        {
            if (spec.IcpMethodElementId.HasValue)
                throw new InvalidOperationException("Method element is only allowed for ICP method assay specifications.");
            if (!spec.HplcMethodAnalyteId.HasValue)
                throw new InvalidOperationException("Method analyte is required for HPLC method assay specifications.");

            var analyte = await _db.HplcMethodAnalytes.Include(a => a.HplcMethod).FirstOrDefaultAsync(a => a.Id == spec.HplcMethodAnalyteId.Value, cancellationToken);
            if (analyte == null || analyte.HplcMethodId != testDef.HplcMethodId)
                throw new InvalidOperationException($"That analyte does not belong to the method of test '{spec.TestCode}'.");

            bool relatedSubstances = analyte.HplcMethod!.ResultMode == HplcResultMode.RelatedSubstances;
            if (!relatedSubstances && (spec.RelatedSubstanceRow.HasValue || spec.HplcMethodImpurityId.HasValue))
                throw new InvalidOperationException("Related-substance rows belong to related-substances methods only.");

            bool residualSolvents = analyte.HplcMethod!.ResultMode == HplcResultMode.ResidualSolvents;
            if (relatedSubstances)
                await ValidateRelatedSubstanceRowAsync(spec, testDef.HplcMethodId, cancellationToken);
            else if (residualSolvents)
            {
                if (spec.ResultBasis != ResultBasis.Ppm || spec.LimitType != LimitType.NotMoreThan
                    || spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                    throw new InvalidOperationException("Residual-solvent specifications use ppm with a not-more-than limit.");
            }
            else
            {
                if (spec.ResultBasis is not (ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit or ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis))
                    throw new InvalidOperationException("Result basis must be assay % of label claim, amount per unit, % as is, % dried basis or % anhydrous basis.");

                if (spec.ResultBasis == ResultBasis.MgPerUnit && (!spec.LabelClaim.HasValue || spec.LabelClaim <= 0 || string.IsNullOrWhiteSpace(spec.LabelClaimUnit)))
                    throw new InvalidOperationException("Amount per unit needs a label claim and its unit.");

                if (spec.ResultBasis != ResultBasis.MgPerUnit && (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit)))
                    throw new InvalidOperationException("Label claim belongs on the amount-per-unit row.");

                var neededRole = spec.ResultBasis switch
                {
                    ResultBasis.PercentDriedBasis => MoistureRole.LossOnDrying,
                    ResultBasis.PercentAnhydrousBasis => MoistureRole.Water,
                    _ => MoistureRole.None
                };
                if (neededRole != MoistureRole.None)
                {
                    var opposite = neededRole == MoistureRole.LossOnDrying ? ResultBasis.PercentAnhydrousBasis : ResultBasis.PercentDriedBasis;
                    if (await _db.Specifications.AnyAsync(s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.Id != spec.Id && s.ResultBasis == opposite, cancellationToken))
                        throw new InvalidOperationException("One HPLC test cannot report on both dried and anhydrous basis.");
                    var hasMoistureTest = await _db.SampleTests.AnyAsync(
                        st => st.ItemId == spec.ItemId && st.TestCode != spec.TestCode
                              && _db.TestDefinitions.Any(t => t.Code == st.TestCode && t.IsActive && t.MoistureRole == neededRole),
                        cancellationToken);
                    if (!hasMoistureTest)
                        throw new InvalidOperationException(neededRole == MoistureRole.LossOnDrying
                            ? "Assign a loss on drying test to this item before using a dried basis."
                            : "Assign a water (Karl Fischer) test to this item before using an anhydrous basis.");
                }

                bool isContent = spec.ResultBasis is ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis;
                bool isClaim = spec.ResultBasis is ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit;

                if (isContent)
                {
                    var conflict = await _db.Specifications.AnyAsync(
                        s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.HplcMethodAnalyteId == spec.HplcMethodAnalyteId
                             && s.Id != spec.Id && s.ResultBasis != spec.ResultBasis,
                        cancellationToken);
                    if (conflict)
                        throw new InvalidOperationException("An HPLC analyte reports either % of label claim or a content basis, not both.");
                }
                else if (isClaim)
                {
                    var conflict = await _db.Specifications.AnyAsync(
                        s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.HplcMethodAnalyteId == spec.HplcMethodAnalyteId
                             && s.Id != spec.Id && (s.ResultBasis == ResultBasis.PercentAsIs || s.ResultBasis == ResultBasis.PercentDriedBasis || s.ResultBasis == ResultBasis.PercentAnhydrousBasis),
                        cancellationToken);
                    if (conflict)
                        throw new InvalidOperationException("An HPLC analyte reports either % of label claim or a content basis, not both.");
                }

            }

            if (spec.LimitType != LimitType.Range &&
                spec.LimitType != LimitType.NotMoreThan &&
                spec.LimitType != LimitType.NotLessThan &&
                spec.LimitType != LimitType.TargetWithTolerance)
            {
                throw new InvalidOperationException($"Limit type '{spec.LimitType}' is not supported for HPLC method assay specifications. Must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance.");
            }
        }
        else if (testDef?.WorkflowType == WorkflowType.IcpMethodAssay)
        {
            if (!spec.IcpMethodElementId.HasValue)
                throw new InvalidOperationException("Method element is required for ICP method assay specifications.");

            var element = await _db.IcpMethodElements.Include(e => e.IcpMethod).FirstOrDefaultAsync(e => e.Id == spec.IcpMethodElementId.Value, cancellationToken);
            if (element == null || element.IcpMethodId != testDef.IcpMethodId)
                throw new InvalidOperationException($"That element does not belong to the method of test '{spec.TestCode}'.");

            if (spec.HplcMethodAnalyteId.HasValue)
                throw new InvalidOperationException("Method analyte is only allowed for HPLC method assay specifications.");

            if (element.IcpMethod!.Mode == IcpMethodMode.MineralAssay)
            {
                if (spec.ResultBasis is not (ResultBasis.MgPerUnit or ResultBasis.PercentLabelClaim))
                    throw new InvalidOperationException("Result basis must be mg per unit or % of label claim for mineral assay.");
                if (!spec.LabelClaim.HasValue || spec.LabelClaim <= 0 || string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                    throw new InvalidOperationException("Mineral assay specifications need a label claim and its unit.");
            }
            else
            {
                if (spec.ResultBasis != ResultBasis.MgPerKg)
                    throw new InvalidOperationException("Result basis must be µg/g (MgPerKg) for elemental impurities.");
                if (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                    throw new InvalidOperationException("Label claim is not used for elemental impurities.");
                if (spec.LimitType != LimitType.NotMoreThan)
                    throw new InvalidOperationException("Elemental impurity specifications use a not-more-than limit.");
            }

            if (spec.LimitType != LimitType.Range &&
                spec.LimitType != LimitType.NotMoreThan &&
                spec.LimitType != LimitType.NotLessThan &&
                spec.LimitType != LimitType.TargetWithTolerance)
            {
                throw new InvalidOperationException($"Limit type '{spec.LimitType}' is not supported for ICP method assay specifications.");
            }
        }
        else if (testDef?.WorkflowType == WorkflowType.Titration)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new InvalidOperationException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpMethodElementId.HasValue)
                throw new InvalidOperationException("Method element is only allowed for ICP method assay specifications.");

            if (spec.ResultBasis is not (ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis
                or ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit))
                throw new InvalidOperationException("Result basis is required for titration specifications: % as is, % dried basis, % anhydrous basis, % of label claim or mg per unit.");

            // A dried/anhydrous basis needs the item to carry the moisture test it corrects with
            // (assigned test is enough; its spec row may be added later). The spec's own test never counts.
            var neededRole = spec.ResultBasis switch
            {
                ResultBasis.PercentDriedBasis => MoistureRole.LossOnDrying,
                ResultBasis.PercentAnhydrousBasis => MoistureRole.Water,
                _ => MoistureRole.None
            };
            if (neededRole != MoistureRole.None)
            {
                var hasMoistureTest = await _db.SampleTests.AnyAsync(
                    st => st.ItemId == spec.ItemId && st.TestCode != spec.TestCode
                          && _db.TestDefinitions.Any(t => t.Code == st.TestCode && t.IsActive && t.MoistureRole == neededRole),
                    cancellationToken);
                if (!hasMoistureTest)
                    throw new InvalidOperationException(neededRole == MoistureRole.LossOnDrying
                        ? "Assign a loss on drying test to this item before using a dried basis."
                        : "Assign a water (Karl Fischer) test to this item before using an anhydrous basis.");
            }

            // One titration run corrects with a single moisture result, so its rows cannot mix the two bases.
            if (neededRole != MoistureRole.None)
            {
                var otherBasis = spec.ResultBasis == ResultBasis.PercentDriedBasis ? ResultBasis.PercentAnhydrousBasis : ResultBasis.PercentDriedBasis;
                if (await _db.Specifications.AnyAsync(
                        s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.Id != spec.Id && s.ResultBasis == otherBasis,
                        cancellationToken))
                    throw new InvalidOperationException("One titration cannot report on both dried and anhydrous basis.");
            }

            bool needsClaim = spec.ResultBasis is ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit;
            if (needsClaim && (!spec.LabelClaim.HasValue || spec.LabelClaim.Value <= 0))
                throw new InvalidOperationException("Label claim (mg per unit) must be greater than zero when the result basis is % of label claim or mg per unit.");
            if (!needsClaim && (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit)))
                throw new InvalidOperationException("Label claim is only used with the % of label claim and mg per unit result bases.");
            if (needsClaim)
            {
                // A label claim (mg per unit) exists only for dosage units.
                var itemCategory = await _db.Items.Where(i => i.Id == spec.ItemId).Select(i => (SampleCategory?)i.Category).FirstOrDefaultAsync(cancellationToken);
                if (itemCategory != SampleCategory.FinishedProduct)
                    throw new InvalidOperationException("% of label claim and mg per unit are only available for Finished Product items.");
            }

            if (spec.LimitType != LimitType.Range &&
                spec.LimitType != LimitType.NotMoreThan &&
                spec.LimitType != LimitType.NotLessThan &&
                spec.LimitType != LimitType.TargetWithTolerance)
            {
                throw new InvalidOperationException($"Limit type '{spec.LimitType}' is not supported for titration specifications. Must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance.");
            }
        }
        else if (testDef?.EquationType == EquationType.CalibrationCurve || testDef?.WorkflowType == WorkflowType.ElementalAssay)
        {
            throw new InvalidOperationException("Elemental assay tests are retired; use an ICP method assay test.");
        }
        else if (testDef?.WorkflowType == WorkflowType.Dissolution || spec.LimitType == LimitType.DissolutionQ)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new InvalidOperationException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpMethodElementId.HasValue)
                throw new InvalidOperationException("Method element is only allowed for ICP method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is not used for this test type.");
        }
        else if (testDef?.WorkflowType == WorkflowType.Disintegration || spec.LimitType == LimitType.DisintegrationTime)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new InvalidOperationException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpMethodElementId.HasValue)
                throw new InvalidOperationException("Method element is only allowed for ICP method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is not used for this test type.");
            if (spec.LabelClaim.HasValue)
                throw new InvalidOperationException("Label claim is not allowed for Disintegration specifications.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new InvalidOperationException("Label claim unit is not allowed for Disintegration specifications.");
        }
        else if (testDef?.WorkflowType == WorkflowType.WeightVariation || spec.LimitType == LimitType.WeightVariation)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new InvalidOperationException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpMethodElementId.HasValue)
                throw new InvalidOperationException("Method element is only allowed for ICP method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is not used for this test type.");
            if (spec.LabelClaim.HasValue)
                throw new InvalidOperationException("Label claim is not allowed for WeightVariation specifications.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new InvalidOperationException("Label claim unit is not allowed for WeightVariation specifications.");
        }
        else
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new InvalidOperationException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpMethodElementId.HasValue)
                throw new InvalidOperationException("Method element is only allowed for ICP method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new InvalidOperationException("Result basis is not used for this test type.");
            if (spec.LabelClaim.HasValue)
                throw new InvalidOperationException("Label claim is not used for this test type.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new InvalidOperationException("Label claim unit is not used for this test type.");
        }
    }

    private static string StageText(ProductionStageRole? role) => role is null ? "all stages" : $"the {role} stage";

    // Backend performs alert/action/spec comparison - frontend only displays results.
    public ResultStatus CompareAgainstLimits(decimal value, Specification spec) =>
        SpecLimitParser.CompareAgainstLimits(value, spec.AlertLimit, spec.ActionLimit, spec.SpecLimit);
}
