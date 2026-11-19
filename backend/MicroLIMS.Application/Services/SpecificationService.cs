using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
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
            throw new BusinessRuleException(
                MessageCodes.SpecificationParameterNameRequired,
                "Parameter name is required.");

        if (spec.ResultBasis == ResultBasis.Ppm && !spec.HplcMethodAnalyteId.HasValue)
            throw new BusinessRuleException(
                MessageCodes.SpecificationPpmOnlyResidualSolvent,
                "ppm is only used for residual-solvent specifications.");

        if (spec.LimitType != LimitType.CountTiered && spec.DilutionFactor.HasValue)
            throw new BusinessRuleException(
                MessageCodes.SpecificationDilutionFactorOnlyCountTiered,
                "Dilution factor is only allowed for Count-Tiered specifications.");

        if (spec.LimitType != LimitType.WeightVariation && spec.DosageForm.HasValue)
            throw new BusinessRuleException(
                MessageCodes.SpecificationDosageFormOnlyWeightVariation,
                "Dosage form is only allowed for WeightVariation specifications.");

        switch (spec.LimitType)
        {
            case LimitType.Range:
                if (!spec.LowerLimit.HasValue || !spec.UpperLimit.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationRangeLimitsRequired,
                        "Lower limit and upper limit are required for Range specifications.");
                if (spec.LowerLimit.Value > spec.UpperLimit.Value)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationLowerLimitCannotExceedUpper,
                        "Lower limit cannot be greater than upper limit.");
                break;

            case LimitType.NotMoreThan:
                if (!spec.UpperLimit.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationUpperLimitRequiredForNotMoreThan,
                        "Upper limit is required for Not More Than specifications.");
                break;

            case LimitType.NotLessThan:
                if (!spec.LowerLimit.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationLowerLimitRequiredForNotLessThan,
                        "Lower limit is required for Not Less Than specifications.");
                break;

            case LimitType.TargetWithTolerance:
                if (!spec.Target.HasValue || !spec.Tolerance.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationTargetAndToleranceRequired,
                        "Target and tolerance are required for Target With Tolerance specifications.");
                if (!spec.ToleranceMode.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationToleranceModeRequired,
                        "Tolerance mode is required for Target With Tolerance specifications.");
                if (spec.Tolerance.Value <= 0)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationToleranceMustBePositive,
                        "Tolerance must be greater than zero.");
                break;

            case LimitType.Qualitative:
                if (string.IsNullOrWhiteSpace(spec.ExpectedResultText))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationExpectedResultTextRequiredForQualitative,
                        "Expected result text is required for Qualitative specifications.");
                break;

            case LimitType.PresenceAbsence:
                if (!spec.ExpectedState.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationExpectedStateRequiredForPresenceAbsence,
                        "Expected state is required for Presence/Absence specifications.");
                break;

            case LimitType.MultiStage:
                if (spec.Stages == null || spec.Stages.Count == 0)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationMultiStageAtLeastOneStage,
                        "At least one stage is required for Multi-Stage specifications.");
                if (spec.Stages.Any(s => string.IsNullOrWhiteSpace(s.StageLabel)))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationStageLabelRequiredForStages,
                        "Stage label is required for every stage.");
                if (spec.Stages.Any(s => string.IsNullOrWhiteSpace(s.AcceptanceCriteriaText)))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationAcceptanceCriteriaRequiredForStages,
                        "Acceptance criteria text is required for every stage.");
                break;

            case LimitType.DissolutionQ:
                if (!spec.LowerLimit.HasValue || spec.LowerLimit.Value <= 0 || spec.LowerLimit.Value > 100)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationDissolutionQRange,
                        "Lower limit (Q) must be between 0 and 100 (exclusive of 0, inclusive of 100) for DissolutionQ specifications.");
                if (!spec.LabelClaim.HasValue || spec.LabelClaim.Value <= 0)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationDissolutionLabelClaimPositive,
                        "Label claim must be greater than zero for DissolutionQ specifications.");
                if (string.IsNullOrWhiteSpace(spec.LabelClaimUnit) || spec.LabelClaimUnit.Trim() != "mg")
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationDissolutionLabelClaimUnitMg,
                        "Label claim unit must be \"mg\" for DissolutionQ specifications.");
                break;

            case LimitType.DisintegrationTime:
                if (!spec.UpperLimit.HasValue || spec.UpperLimit.Value <= 0)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationDisintegrationTimePositive,
                        "Upper limit (time in minutes) must be greater than zero for DisintegrationTime specifications.");
                if (string.IsNullOrWhiteSpace(spec.Unit) || spec.Unit.Trim() != "min")
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationDisintegrationUnitMin,
                        "Unit must be \"min\" for DisintegrationTime specifications.");
                if (spec.LabelClaim.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationDisintegrationLabelClaimNotAllowed,
                        "Label claim is not allowed for DisintegrationTime specifications.");
                if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationDisintegrationLabelClaimUnitNotAllowed,
                        "Label claim unit is not allowed for DisintegrationTime specifications.");
                break;

            case LimitType.WeightVariation:
                if (!spec.DosageForm.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationDosageFormRequiredForWeightVariation,
                        "Dosage form is required for WeightVariation specifications.");
                if (string.IsNullOrWhiteSpace(spec.Unit) || spec.Unit.Trim() != "mg")
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationWeightVariationUnitMg,
                        "Unit must be \"mg\" for WeightVariation specifications.");
                if (spec.LowerLimit.HasValue || spec.UpperLimit.HasValue || spec.Target.HasValue || spec.Tolerance.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationWeightVariationLimitsNotAllowed,
                        "Limits are not allowed for WeightVariation specifications (configured on Test Master).");
                if (spec.LabelClaim.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationWeightVariationLabelClaimNotAllowed,
                        "Label claim is not allowed for WeightVariation specifications.");
                if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationWeightVariationLabelClaimUnitNotAllowed,
                        "Label claim unit is not allowed for WeightVariation specifications.");
                break;

            case LimitType.CountTiered:
                break;

            default:
                throw new BusinessRuleException(
                    MessageCodes.SpecificationUnsupportedLimitType,
                    $"Unsupported limit type '{spec.LimitType}'.",
                    new Dictionary<string, string> { ["limitType"] = spec.LimitType.ToString() });
        }
    }

    // Trims and collapses inner whitespace; blank = ungraded (null). BP and USP
    // are stored upper-case, any other grade (e.g. In-house) as typed.
    public static string? NormalizeGrade(string? grade)
    {
        if (string.IsNullOrWhiteSpace(grade)) return null;
        var g = string.Join(' ', grade.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (g.Length > 20)
            throw new BusinessRuleException(
                MessageCodes.SpecificationGradeMaxLength,
                "Grade must be 20 characters or fewer.");
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

    private async Task ValidateRelatedSubstanceRowAsync(Specification spec, int? methodId, CancellationToken cancellationToken)
    {
        if (spec.RelatedSubstanceRow is not RelatedSubstanceRow kind || !Enum.IsDefined(kind))
            throw new BusinessRuleException(
                MessageCodes.SpecificationRelatedSubstanceRowRequired,
                "Choose the related-substance row: named impurity, any unspecified impurity, total, or total of others.");

        if (kind == RelatedSubstanceRow.Named)
        {
            if (!spec.HplcMethodImpurityId.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationRelatedSubstanceImpurityRequired,
                    "Choose the impurity for this row.");
            var impurityMethodId = await _db.HplcMethodImpurities.Where(i => i.Id == spec.HplcMethodImpurityId.Value)
                .Select(i => (int?)i.HplcMethodId).FirstOrDefaultAsync(cancellationToken);
            if (impurityMethodId != methodId)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationRelatedSubstanceImpurityNotInMethod,
                    "The impurity does not belong to this test's HPLC method.");
        }
        else if (spec.HplcMethodImpurityId.HasValue)
            throw new BusinessRuleException(
                MessageCodes.SpecificationRelatedSubstanceOnlyNamedHasImpurity,
                "Only a named-impurity row names an impurity.");

        if (spec.LimitType != LimitType.NotMoreThan)
            throw new BusinessRuleException(
                MessageCodes.SpecificationRelatedSubstanceMustBeNotMoreThan,
                "Related-substance limits are 'not more than' limits.");
        if (spec.ResultBasis != ResultBasis.PercentAsIs)
            throw new BusinessRuleException(
                MessageCodes.SpecificationRelatedSubstanceMustBePercentAsIs,
                "Related-substance rows report % (as is).");
        if (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
            throw new BusinessRuleException(
                MessageCodes.SpecificationRelatedSubstanceNoLabelClaim,
                "Related-substance rows have no label claim.");
    }

    public async Task ValidateAsync(Specification spec, CancellationToken cancellationToken = default)
    {
        if (spec.RelatedSubstanceRow.HasValue && spec.LimitType == LimitType.NotMoreThan && !(spec.UpperLimit > 0m))
            throw new BusinessRuleException(
                MessageCodes.SpecificationRelatedSubstanceLimitPositive,
                "Enter the limit (above 0 %).");
        Validate(spec);
        spec.Grade = NormalizeGrade(spec.Grade);
        // Grades match case-insensitively ("In-house" = "in-house"); reuse the item's
        // existing spelling so exact-match lookups see one value per item.
        var grade = spec.Grade?.ToLower();
        if (grade is not null)
        {
            var existing = await _db.Specifications
                .Where(s => s.ItemId == spec.ItemId && s.Id != spec.Id && !s.IsRetired && s.Grade != null && s.Grade.ToLower() == grade)
                .Select(s => s.Grade).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) spec.Grade = existing;
        }

        if (string.IsNullOrWhiteSpace(spec.TestCode))
            throw new BusinessRuleException(
                MessageCodes.SpecificationTestCodeRequired,
                "Test code is required.");

        var testAssigned = await _db.SampleTests.AnyAsync(
            st => st.ItemId == spec.ItemId && st.TestCode == spec.TestCode,
            cancellationToken);
        if (!testAssigned)
            throw new BusinessRuleException(
                MessageCodes.SpecificationTestNotAssignedToItem,
                $"Test '{spec.TestCode}' is not assigned to item {spec.ItemId}.",
                new Dictionary<string, string>
                {
                    ["testCode"] = spec.TestCode,
                    ["itemId"] = spec.ItemId.ToString(CultureInfo.InvariantCulture),
                });

        await NotApplicableTestService.EnsureSpecBasisHasMoistureAsync(_db, spec.ItemId, spec.ResultBasis, spec.Grade, cancellationToken);

        // A physicochemical test only takes specs for the item categories of its area.
        var testArea = await _db.TestDefinitions.Where(t => t.Code == spec.TestCode).Select(t => t.PhyschemArea).FirstOrDefaultAsync(cancellationToken);
        if (testArea is not null)
        {
            var itemCategory = await _db.Items.Where(i => i.Id == spec.ItemId).Select(i => (SampleCategory?)i.Category).FirstOrDefaultAsync(cancellationToken);
            if (itemCategory is SampleCategory cat && !PhyschemAreas.Includes(testArea, PhyschemAreas.OfCategory(cat)))
            {
                if (PhyschemAreas.OfCategory(cat) == WorkspaceArea.Fp)
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationTestNotForFinishedProducts,
                        $"Test {spec.TestCode} is not used for finished products - it belongs to RM & PM.",
                        new Dictionary<string, string> { ["testCode"] = spec.TestCode });
                else
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationTestNotForRawPackagingMaterials,
                        $"Test {spec.TestCode} is not used for raw and packaging materials - it belongs to FP.",
                        new Dictionary<string, string> { ["testCode"] = spec.TestCode });
            }
        }

        // Production stages exist only on Finished Product samples, so a
        // stage-specific row on any other item could never be used.
        if (spec.ProductionStageRole is ProductionStageRole role)
        {
            if (role == ProductionStageRole.Other)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationProductionStageInvalid,
                    "Choose Bulk, In-Process, Finished or Stability, or leave the stage as all stages.");
            var category = await _db.Items.Where(i => i.Id == spec.ItemId).Select(i => (SampleCategory?)i.Category).FirstOrDefaultAsync(cancellationToken);
            if (category != SampleCategory.FinishedProduct)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationProductionStageOnlyFinishedProduct,
                    "Stage-specific specifications are only available for Finished Product items.");
        }

        // Method-analyte rows are keyed by analyte + basis; checked first so the
        // clearer message wins over the parameter-name duplicate below.
        if (spec.HplcMethodAnalyteId is int methodAnalyteId && spec.RelatedSubstanceRow is null)
        {
            var duplicateAnalyteBasis = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.HplcMethodAnalyteId == methodAnalyteId
                     && s.ResultBasis == spec.ResultBasis && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id && !s.IsRetired
                     && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
                cancellationToken);
            if (duplicateAnalyteBasis)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDuplicateAnalyteAndBasis,
                    "A specification for this analyte and basis already exists.");
        }

        if (spec.IcpTestElementId is int icpElementId)
        {
            var duplicateElementBasis = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.IcpTestElementId == icpElementId
                     && s.ResultBasis == spec.ResultBasis && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id && !s.IsRetired
                     && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
                cancellationToken);
            if (duplicateElementBasis)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDuplicateElementAndBasis,
                    "A specification for this element and basis already exists.");
        }

        // Related-substance rows are keyed by kind (+ impurity for a Named row) per stage and grade;
        // that key replaces the parameter-name check, so rows may share a parameter name.
        if (spec.RelatedSubstanceRow is RelatedSubstanceRow kind)
        {
            var relatedDuplicate = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.RelatedSubstanceRow == kind
                     && s.HplcMethodImpurityId == spec.HplcMethodImpurityId
                     && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id && !s.IsRetired
                     && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
                cancellationToken);
            if (relatedDuplicate)
            {
                string? impurityName = null;
                if (kind == RelatedSubstanceRow.Named && spec.HplcMethodImpurityId is int impurityId)
                    impurityName = await _db.HplcMethodImpurities.Where(i => i.Id == impurityId).Select(i => i.Name).FirstOrDefaultAsync(cancellationToken);
                switch (kind)
                {
                    case RelatedSubstanceRow.Named:
                        throw new BusinessRuleException(
                            MessageCodes.SpecificationDuplicateRelatedNamedImpurity,
                            $"A named impurity '{impurityName}' row already exists for this test, stage and grade.",
                            new Dictionary<string, string> { ["impurity"] = impurityName ?? string.Empty });
                    case RelatedSubstanceRow.UnspecifiedEach:
                        throw new BusinessRuleException(
                            MessageCodes.SpecificationDuplicateRelatedUnspecifiedImpurity,
                            "A any unspecified impurity row already exists for this test, stage and grade.");
                    case RelatedSubstanceRow.Total:
                        throw new BusinessRuleException(
                            MessageCodes.SpecificationDuplicateRelatedTotalImpurities,
                            "A total impurities row already exists for this test, stage and grade.");
                    default:
                        throw new BusinessRuleException(
                            MessageCodes.SpecificationDuplicateRelatedTotalOfOthers,
                            "A total of others row already exists for this test, stage and grade.");
                }
            }
        }

        var duplicate = spec.RelatedSubstanceRow is null && await _db.Specifications.AnyAsync(
            s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ParameterName == spec.ParameterName
                 && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id && !s.IsRetired
                 && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
            cancellationToken);
        if (duplicate)
        {
            if (spec.Grade is not null)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDuplicateParameterAndGrade,
                    "A specification for this parameter and grade already exists.");
            else if (spec.ProductionStageRole is null)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDuplicateParameterAllStages,
                    $"A specification for parameter '{spec.ParameterName}' already exists for test '{spec.TestCode}' at all stages on this item.",
                    new Dictionary<string, string>
                    {
                        ["parameter"] = spec.ParameterName,
                        ["testCode"] = spec.TestCode,
                    });
            else
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDuplicateParameterAtStage,
                    $"A specification for parameter '{spec.ParameterName}' already exists for test '{spec.TestCode}' at the {spec.ProductionStageRole} stage on this item.",
                    new Dictionary<string, string>
                    {
                        ["parameter"] = spec.ParameterName,
                        ["testCode"] = spec.TestCode,
                        ["stage"] = spec.ProductionStageRole.Value.ToString(),
                    });
        }

        var testDef = await _db.TestDefinitions
            .FirstOrDefaultAsync(t => t.Code == spec.TestCode, cancellationToken);

        if (spec.LimitType == LimitType.DissolutionQ)
        {
            if (testDef == null || testDef.WorkflowType != WorkflowType.Dissolution)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDissolutionOnlyForDissolutionTests,
                    "DissolutionQ specifications are only allowed for Dissolution tests.");

            var duplicateDissolution = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id && !s.IsRetired,
                cancellationToken);
            if (duplicateDissolution)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDissolutionSingleSpec,
                    $"Only one specification is allowed for Dissolution test '{spec.TestCode}' on this item.",
                    new Dictionary<string, string> { ["testCode"] = spec.TestCode });
        }
        else if (testDef?.WorkflowType == WorkflowType.Dissolution)
        {
            throw new BusinessRuleException(
                MessageCodes.SpecificationDissolutionTestRequiresDissolutionQ,
                "Only DissolutionQ specifications are allowed for Dissolution tests.");
        }

        if (spec.LimitType == LimitType.DisintegrationTime)
        {
            if (testDef == null || testDef.WorkflowType != WorkflowType.Disintegration)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDisintegrationOnlyForDisintegrationTests,
                    "DisintegrationTime specifications are only allowed for Disintegration tests.");

            var duplicateDisintegration = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id && !s.IsRetired,
                cancellationToken);
            if (duplicateDisintegration)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationDisintegrationSingleSpec,
                    $"Only one specification is allowed for Disintegration test '{spec.TestCode}' on this item.",
                    new Dictionary<string, string> { ["testCode"] = spec.TestCode });
        }
        else if (testDef?.WorkflowType == WorkflowType.Disintegration)
        {
            throw new BusinessRuleException(
                MessageCodes.SpecificationDisintegrationTestRequiresDisintegrationTime,
                "Only DisintegrationTime specifications are allowed for Disintegration tests.");
        }

        if (spec.LimitType == LimitType.WeightVariation)
        {
            if (testDef == null || testDef.WorkflowType != WorkflowType.WeightVariation)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationWeightVariationOnlyForWeightVariationTests,
                    "WeightVariation specifications are only allowed for WeightVariation tests.");

            var duplicateWeightVariation = await _db.Specifications.AnyAsync(
                s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.ProductionStageRole == spec.ProductionStageRole && s.Id != spec.Id && !s.IsRetired,
                cancellationToken);
            if (duplicateWeightVariation)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationWeightVariationSingleSpec,
                    $"Only one specification is allowed for WeightVariation test '{spec.TestCode}' on this item.",
                    new Dictionary<string, string> { ["testCode"] = spec.TestCode });
        }
        else if (testDef?.WorkflowType == WorkflowType.WeightVariation)
        {
            throw new BusinessRuleException(
                MessageCodes.SpecificationWeightVariationTestRequiresWeightVariation,
                "Only WeightVariation specifications are allowed for WeightVariation tests.");
        }

        // HPLC chain S3: specification rows for an HplcMethodAssay test are
        // keyed by method analyte + quantity, reusing ResultBasis as the
        // quantity (PercentLabelClaim = assay %, MgPerUnit = amount per unit;
        // spec 3.4). No new enum.
        if (testDef?.WorkflowType != WorkflowType.HplcMethodAssay
            && (spec.RelatedSubstanceRow.HasValue || spec.HplcMethodImpurityId.HasValue))
            throw new BusinessRuleException(
                MessageCodes.SpecificationRelatedSubstanceOnlyRelatedMethods,
                "Related-substance rows belong to related-substances methods only.");

        if (testDef?.WorkflowType == WorkflowType.HplcMethodAssay)
        {
            if (spec.IcpTestElementId.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationMethodElementOnlyIcp,
                    "Method element is only allowed for ICP method assay specifications.");
            if (!spec.HplcMethodAnalyteId.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationMethodAnalyteRequiredForHplc,
                    "Method analyte is required for HPLC method assay specifications.");

            var analyte = await _db.HplcMethodAnalytes.Include(a => a.HplcMethod).FirstOrDefaultAsync(a => a.Id == spec.HplcMethodAnalyteId.Value, cancellationToken);
            if (analyte == null || analyte.HplcMethodId != testDef.HplcMethodId)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationAnalyteNotInTestMethod,
                    $"That analyte does not belong to the method of test '{spec.TestCode}'.",
                    new Dictionary<string, string> { ["testCode"] = spec.TestCode });

            bool relatedSubstances = analyte.HplcMethod!.ResultMode == HplcResultMode.RelatedSubstances;
            if (!relatedSubstances && (spec.RelatedSubstanceRow.HasValue || spec.HplcMethodImpurityId.HasValue))
                throw new BusinessRuleException(
                    MessageCodes.SpecificationRelatedSubstanceOnlyRelatedMethods,
                    "Related-substance rows belong to related-substances methods only.");

            bool residualSolvents = analyte.HplcMethod!.ResultMode == HplcResultMode.ResidualSolvents;
            if (relatedSubstances)
                await ValidateRelatedSubstanceRowAsync(spec, testDef.HplcMethodId, cancellationToken);
            else if (residualSolvents)
            {
                if (spec.ResultBasis != ResultBasis.Ppm || spec.LimitType != LimitType.NotMoreThan
                    || spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationResidualSolventMustBePpmNotMoreThan,
                        "Residual-solvent specifications use ppm with a not-more-than limit.");
            }
            else
            {
                if (spec.ResultBasis is not (ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit or ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationHplcResultBasisInvalid,
                        "Result basis must be assay % of label claim, amount per unit, % as is, % dried basis or % anhydrous basis.");

                if (spec.ResultBasis == ResultBasis.MgPerUnit && (!spec.LabelClaim.HasValue || spec.LabelClaim <= 0 || string.IsNullOrWhiteSpace(spec.LabelClaimUnit)))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationHplcAmountPerUnitNeedsLabelClaim,
                        "Amount per unit needs a label claim and its unit.");

                if (spec.ResultBasis != ResultBasis.MgPerUnit && (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit)))
                    throw new BusinessRuleException(
                        MessageCodes.SpecificationHplcLabelClaimOnlyOnAmountPerUnit,
                        "Label claim belongs on the amount-per-unit row.");

                var neededRole = spec.ResultBasis switch
                {
                    ResultBasis.PercentDriedBasis => MoistureRole.LossOnDrying,
                    ResultBasis.PercentAnhydrousBasis => MoistureRole.Water,
                    _ => MoistureRole.None
                };
                if (neededRole != MoistureRole.None)
                {
                    var opposite = neededRole == MoistureRole.LossOnDrying ? ResultBasis.PercentAnhydrousBasis : ResultBasis.PercentDriedBasis;
                    if (await _db.Specifications.AnyAsync(s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.Id != spec.Id && !s.IsRetired && s.ProductionStageRole == spec.ProductionStageRole
                             && (s.Grade == null ? null : s.Grade.ToLower()) == grade
                             && s.ResultBasis == opposite, cancellationToken))
                        throw new BusinessRuleException(
                            MessageCodes.SpecificationHplcBothMoistureBasesConflict,
                            "One HPLC test cannot report on both dried and anhydrous basis.");
                    var hasMoistureTest = await _db.SampleTests.AnyAsync(
                        st => st.ItemId == spec.ItemId && st.TestCode != spec.TestCode
                              && _db.TestDefinitions.Any(t => t.Code == st.TestCode && t.IsActive && t.MoistureRole == neededRole),
                        cancellationToken);
                    if (!hasMoistureTest)
                    {
                        if (neededRole == MoistureRole.LossOnDrying)
                            throw new BusinessRuleException(
                                MessageCodes.SpecificationMoistureLossOnDryingRequired,
                                "Assign a loss on drying test to this item before using a dried basis.");
                        else
                            throw new BusinessRuleException(
                                MessageCodes.SpecificationMoistureWaterRequired,
                                "Assign a water (Karl Fischer) test to this item before using an anhydrous basis.");
                    }
                }

                bool isContent = spec.ResultBasis is ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis;
                bool isClaim = spec.ResultBasis is ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit;

                if (isContent)
                {
                    var conflict = await _db.Specifications.AnyAsync(
                        s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.HplcMethodAnalyteId == spec.HplcMethodAnalyteId
                             && s.Id != spec.Id && !s.IsRetired && s.ResultBasis != spec.ResultBasis
                             && s.ProductionStageRole == spec.ProductionStageRole && (s.Grade == null ? null : s.Grade.ToLower()) == grade,
                        cancellationToken);
                    if (conflict)
                        throw new BusinessRuleException(
                            MessageCodes.SpecificationHplcAnalyteClaimOrContentOnly,
                            "An HPLC analyte reports either % of label claim or a content basis, not both.");
                }
                else if (isClaim)
                {
                    var conflict = await _db.Specifications.AnyAsync(
                        s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.HplcMethodAnalyteId == spec.HplcMethodAnalyteId
                             && s.Id != spec.Id && !s.IsRetired && s.ProductionStageRole == spec.ProductionStageRole
                             && (s.Grade == null ? null : s.Grade.ToLower()) == grade
                             && (s.ResultBasis == ResultBasis.PercentAsIs || s.ResultBasis == ResultBasis.PercentDriedBasis || s.ResultBasis == ResultBasis.PercentAnhydrousBasis),
                        cancellationToken);
                    if (conflict)
                        throw new BusinessRuleException(
                            MessageCodes.SpecificationHplcAnalyteClaimOrContentOnly,
                            "An HPLC analyte reports either % of label claim or a content basis, not both.");
                }

            }

            if (spec.LimitType != LimitType.Range &&
                spec.LimitType != LimitType.NotMoreThan &&
                spec.LimitType != LimitType.NotLessThan &&
                spec.LimitType != LimitType.TargetWithTolerance)
            {
                throw new BusinessRuleException(
                    MessageCodes.SpecificationHplcLimitTypeUnsupported,
                    $"Limit type '{spec.LimitType}' is not supported for HPLC method assay specifications. Must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance.",
                    new Dictionary<string, string> { ["limitType"] = spec.LimitType.ToString() });
            }
        }
        else if (testDef?.WorkflowType == WorkflowType.IcpMethodAssay)
        {
            if (!spec.IcpTestElementId.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationElementRequiredForIcp,
                    "Element is required for ICP method assay specifications.");

            var element = await _db.TestDefinitionIcpElements.FirstOrDefaultAsync(e => e.Id == spec.IcpTestElementId.Value, cancellationToken);
            if (element == null || element.TestDefinitionId != testDef.Id)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationElementNotInTest,
                    $"That element does not belong to test '{spec.TestCode}'.",
                    new Dictionary<string, string> { ["testCode"] = spec.TestCode });

            if (spec.HplcMethodAnalyteId.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationMethodAnalyteOnlyHplc,
                    "Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationIcpResultBasisNotUsed,
                    "Result basis is not used for ICP specifications.");
            if (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new BusinessRuleException(
                    MessageCodes.SpecificationIcpLabelClaimNotUsed,
                    "Label claim is not used for ICP specifications.");

            // Assay: the limit is on the assay % (of the test's target). Impurity: the limit is the element ppm; 0 = not detected.
            if (testDef.IcpMode == IcpTestMode.Impurity && spec.LimitType != LimitType.NotMoreThan)
                throw new BusinessRuleException(
                    MessageCodes.SpecificationElementalImpurityMustBeNotMoreThan,
                    "Elemental impurity specifications use a not-more-than limit.");

            if (spec.LimitType != LimitType.Range &&
                spec.LimitType != LimitType.NotMoreThan &&
                spec.LimitType != LimitType.NotLessThan &&
                spec.LimitType != LimitType.TargetWithTolerance)
            {
                throw new BusinessRuleException(
                    MessageCodes.SpecificationIcpLimitTypeUnsupported,
                    $"Limit type '{spec.LimitType}' is not supported for ICP method assay specifications.",
                    new Dictionary<string, string> { ["limitType"] = spec.LimitType.ToString() });
            }
        }
        else if (testDef?.WorkflowType == WorkflowType.Titration)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new BusinessRuleException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpTestElementId.HasValue)
                throw new BusinessRuleException("Method element is only allowed for ICP method assay specifications.");

            if (spec.ResultBasis is not (ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis
                or ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit))
                throw new BusinessRuleException("Result basis is required for titration specifications: % as is, % dried basis, % anhydrous basis, % of label claim or mg per unit.");

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
                    throw new BusinessRuleException(neededRole == MoistureRole.LossOnDrying
                        ? "Assign a loss on drying test to this item before using a dried basis."
                        : "Assign a water (Karl Fischer) test to this item before using an anhydrous basis.");
            }

            // One titration run corrects with a single moisture result, so its rows cannot mix the two bases.
            if (neededRole != MoistureRole.None)
            {
                var otherBasis = spec.ResultBasis == ResultBasis.PercentDriedBasis ? ResultBasis.PercentAnhydrousBasis : ResultBasis.PercentDriedBasis;
                if (await _db.Specifications.AnyAsync(
                        s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.Id != spec.Id && !s.IsRetired && s.ResultBasis == otherBasis,
                        cancellationToken))
                    throw new BusinessRuleException("One titration cannot report on both dried and anhydrous basis.");
            }

            bool needsClaim = spec.ResultBasis is ResultBasis.PercentLabelClaim or ResultBasis.MgPerUnit;
            if (needsClaim && (!spec.LabelClaim.HasValue || spec.LabelClaim.Value <= 0))
                throw new BusinessRuleException("Label claim (mg per unit) must be greater than zero when the result basis is % of label claim or mg per unit.");
            if (!needsClaim && (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit)))
                throw new BusinessRuleException("Label claim is only used with the % of label claim and mg per unit result bases.");
            if (needsClaim)
            {
                // A label claim (mg per unit) exists only for dosage units.
                var itemCategory = await _db.Items.Where(i => i.Id == spec.ItemId).Select(i => (SampleCategory?)i.Category).FirstOrDefaultAsync(cancellationToken);
                if (itemCategory != SampleCategory.FinishedProduct)
                    throw new BusinessRuleException("% of label claim and mg per unit are only available for Finished Product items.");
            }

            if (spec.LimitType != LimitType.Range &&
                spec.LimitType != LimitType.NotMoreThan &&
                spec.LimitType != LimitType.NotLessThan &&
                spec.LimitType != LimitType.TargetWithTolerance)
            {
                throw new BusinessRuleException($"Limit type '{spec.LimitType}' is not supported for titration specifications. Must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance.");
            }
        }
        else if (testDef?.WorkflowType == WorkflowType.UvSpectrophotometry)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new BusinessRuleException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpTestElementId.HasValue)
                throw new BusinessRuleException("Method element is only allowed for ICP method assay specifications.");
            if (spec.LabelClaim.HasValue || !string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new BusinessRuleException("Label claim is not used for UV specifications.");

            if (testDef.UvCalculationMode == UvCalculationMode.AbsorbanceLimit)
            {
                // The limit is a reference absorbance typed at recording time, so the row carries text only.
                if (spec.LimitType != LimitType.Qualitative)
                    throw new BusinessRuleException("An absorbance limit test takes a Qualitative specification.");
                if (spec.ResultBasis.HasValue)
                    throw new BusinessRuleException("Result basis is not used for an absorbance limit test.");
            }
            else
            {
                if (spec.LimitType is not (LimitType.Range or LimitType.NotMoreThan or LimitType.NotLessThan))
                    throw new BusinessRuleException($"Limit type '{spec.LimitType}' is not supported for this UV test. Must be Range, NotMoreThan or NotLessThan.");
                if (spec.ResultBasis is not (null or ResultBasis.PercentAsIs or ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis))
                    throw new BusinessRuleException("Result basis must be % as is, % dried basis, % anhydrous basis or left empty for a UV test.");

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
                        throw new BusinessRuleException(neededRole == MoistureRole.LossOnDrying
                            ? "Assign a loss on drying test to this item before using a dried basis."
                            : "Assign a water (Karl Fischer) test to this item before using an anhydrous basis.");

                    var otherBasis = spec.ResultBasis == ResultBasis.PercentDriedBasis ? ResultBasis.PercentAnhydrousBasis : ResultBasis.PercentDriedBasis;
                    if (await _db.Specifications.AnyAsync(
                            s => s.ItemId == spec.ItemId && s.TestCode == spec.TestCode && s.Id != spec.Id && !s.IsRetired && s.ResultBasis == otherBasis,
                            cancellationToken))
                        throw new BusinessRuleException("One UV test cannot report on both dried and anhydrous basis.");
                }
            }
        }
        else if (testDef?.EquationType == EquationType.CalibrationCurve || testDef?.WorkflowType == WorkflowType.ElementalAssay)
        {
            throw new BusinessRuleException("Elemental assay tests are retired; use an ICP method assay test.");
        }
        else if (testDef?.WorkflowType == WorkflowType.Dissolution || spec.LimitType == LimitType.DissolutionQ)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new BusinessRuleException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpTestElementId.HasValue)
                throw new BusinessRuleException("Method element is only allowed for ICP method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new BusinessRuleException("Result basis is not used for this test type.");
        }
        else if (testDef?.WorkflowType == WorkflowType.Disintegration || spec.LimitType == LimitType.DisintegrationTime)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new BusinessRuleException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpTestElementId.HasValue)
                throw new BusinessRuleException("Method element is only allowed for ICP method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new BusinessRuleException("Result basis is not used for this test type.");
            if (spec.LabelClaim.HasValue)
                throw new BusinessRuleException("Label claim is not allowed for Disintegration specifications.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new BusinessRuleException("Label claim unit is not allowed for Disintegration specifications.");
        }
        else if (testDef?.WorkflowType == WorkflowType.WeightVariation || spec.LimitType == LimitType.WeightVariation)
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new BusinessRuleException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpTestElementId.HasValue)
                throw new BusinessRuleException("Method element is only allowed for ICP method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new BusinessRuleException("Result basis is not used for this test type.");
            if (spec.LabelClaim.HasValue)
                throw new BusinessRuleException("Label claim is not allowed for WeightVariation specifications.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new BusinessRuleException("Label claim unit is not allowed for WeightVariation specifications.");
        }
        else
        {
            if (spec.HplcMethodAnalyteId.HasValue)
                throw new BusinessRuleException("Method analyte is only allowed for HPLC method assay specifications.");
            if (spec.IcpTestElementId.HasValue)
                throw new BusinessRuleException("Method element is only allowed for ICP method assay specifications.");
            if (spec.ResultBasis.HasValue)
                throw new BusinessRuleException("Result basis is not used for this test type.");
            if (spec.LabelClaim.HasValue)
                throw new BusinessRuleException("Label claim is not used for this test type.");
            if (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit))
                throw new BusinessRuleException("Label claim unit is not used for this test type.");
        }
    }

    // Backend performs alert/action/spec comparison - frontend only displays results.
    public ResultStatus CompareAgainstLimits(decimal value, Specification spec) =>
        SpecLimitParser.CompareAgainstLimits(value, spec.AlertLimit, spec.ActionLimit, spec.SpecLimit);
}
