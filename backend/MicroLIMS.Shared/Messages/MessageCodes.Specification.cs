namespace MicroLIMS.Shared.Messages;

// Specification.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string SpecificationAcceptanceCriteriaRequiredForStages = "Specification.AcceptanceCriteriaRequiredForStages";
    public const string SpecificationDilutionFactorOnlyCountTiered = "Specification.DilutionFactorOnlyCountTiered";
    public const string SpecificationDisintegrationLabelClaimNotAllowed = "Specification.DisintegrationLabelClaimNotAllowed";
    public const string SpecificationDisintegrationLabelClaimUnitNotAllowed = "Specification.DisintegrationLabelClaimUnitNotAllowed";
    public const string SpecificationDisintegrationTimePositive = "Specification.DisintegrationTimePositive";
    public const string SpecificationDisintegrationUnitMin = "Specification.DisintegrationUnitMin";
    public const string SpecificationDissolutionLabelClaimPositive = "Specification.DissolutionLabelClaimPositive";
    public const string SpecificationDissolutionLabelClaimUnitMg = "Specification.DissolutionLabelClaimUnitMg";
    public const string SpecificationDissolutionQRange = "Specification.DissolutionQRange";
    public const string SpecificationDosageFormOnlyWeightVariation = "Specification.DosageFormOnlyWeightVariation";
    public const string SpecificationDosageFormRequiredForWeightVariation = "Specification.DosageFormRequiredForWeightVariation";
    public const string SpecificationExpectedResultTextRequiredForQualitative = "Specification.ExpectedResultTextRequiredForQualitative";
    public const string SpecificationExpectedStateRequiredForPresenceAbsence = "Specification.ExpectedStateRequiredForPresenceAbsence";
    public const string SpecificationGradeMaxLength = "Specification.GradeMaxLength";
    public const string SpecificationLowerLimitCannotExceedUpper = "Specification.LowerLimitCannotExceedUpper";
    public const string SpecificationLowerLimitRequiredForNotLessThan = "Specification.LowerLimitRequiredForNotLessThan";
    public const string SpecificationMultiStageAtLeastOneStage = "Specification.MultiStageAtLeastOneStage";
    public const string SpecificationParameterNameRequired = "Specification.ParameterNameRequired";
    public const string SpecificationPpmOnlyResidualSolvent = "Specification.PpmOnlyResidualSolvent";
    public const string SpecificationRangeLimitsRequired = "Specification.RangeLimitsRequired";
    public const string SpecificationRelatedSubstanceImpurityNotInMethod = "Specification.RelatedSubstanceImpurityNotInMethod";
    public const string SpecificationRelatedSubstanceImpurityRequired = "Specification.RelatedSubstanceImpurityRequired";
    public const string SpecificationRelatedSubstanceLimitPositive = "Specification.RelatedSubstanceLimitPositive";
    public const string SpecificationRelatedSubstanceMustBeNotMoreThan = "Specification.RelatedSubstanceMustBeNotMoreThan";
    public const string SpecificationRelatedSubstanceMustBePercentAsIs = "Specification.RelatedSubstanceMustBePercentAsIs";
    public const string SpecificationRelatedSubstanceNoLabelClaim = "Specification.RelatedSubstanceNoLabelClaim";
    public const string SpecificationRelatedSubstanceOnlyNamedHasImpurity = "Specification.RelatedSubstanceOnlyNamedHasImpurity";
    public const string SpecificationRelatedSubstanceRowRequired = "Specification.RelatedSubstanceRowRequired";
    public const string SpecificationStageLabelRequiredForStages = "Specification.StageLabelRequiredForStages";
    public const string SpecificationTargetAndToleranceRequired = "Specification.TargetAndToleranceRequired";
    public const string SpecificationToleranceModeRequired = "Specification.ToleranceModeRequired";
    public const string SpecificationToleranceMustBePositive = "Specification.ToleranceMustBePositive";
    public const string SpecificationUnsupportedLimitType = "Specification.UnsupportedLimitType";
    public const string SpecificationUpperLimitRequiredForNotMoreThan = "Specification.UpperLimitRequiredForNotMoreThan";
    public const string SpecificationWeightVariationLabelClaimNotAllowed = "Specification.WeightVariationLabelClaimNotAllowed";
    public const string SpecificationWeightVariationLabelClaimUnitNotAllowed = "Specification.WeightVariationLabelClaimUnitNotAllowed";
    public const string SpecificationWeightVariationLimitsNotAllowed = "Specification.WeightVariationLimitsNotAllowed";
    public const string SpecificationWeightVariationUnitMg = "Specification.WeightVariationUnitMg";

    private static Dictionary<string, string[]> SpecificationParameters() => new()
    {
        [SpecificationAcceptanceCriteriaRequiredForStages] = [],
        [SpecificationDilutionFactorOnlyCountTiered] = [],
        [SpecificationDisintegrationLabelClaimNotAllowed] = [],
        [SpecificationDisintegrationLabelClaimUnitNotAllowed] = [],
        [SpecificationDisintegrationTimePositive] = [],
        [SpecificationDisintegrationUnitMin] = [],
        [SpecificationDissolutionLabelClaimPositive] = [],
        [SpecificationDissolutionLabelClaimUnitMg] = [],
        [SpecificationDissolutionQRange] = [],
        [SpecificationDosageFormOnlyWeightVariation] = [],
        [SpecificationDosageFormRequiredForWeightVariation] = [],
        [SpecificationExpectedResultTextRequiredForQualitative] = [],
        [SpecificationExpectedStateRequiredForPresenceAbsence] = [],
        [SpecificationGradeMaxLength] = [],
        [SpecificationLowerLimitCannotExceedUpper] = [],
        [SpecificationLowerLimitRequiredForNotLessThan] = [],
        [SpecificationMultiStageAtLeastOneStage] = [],
        [SpecificationParameterNameRequired] = [],
        [SpecificationPpmOnlyResidualSolvent] = [],
        [SpecificationRangeLimitsRequired] = [],
        [SpecificationRelatedSubstanceImpurityNotInMethod] = [],
        [SpecificationRelatedSubstanceImpurityRequired] = [],
        [SpecificationRelatedSubstanceLimitPositive] = [],
        [SpecificationRelatedSubstanceMustBeNotMoreThan] = [],
        [SpecificationRelatedSubstanceMustBePercentAsIs] = [],
        [SpecificationRelatedSubstanceNoLabelClaim] = [],
        [SpecificationRelatedSubstanceOnlyNamedHasImpurity] = [],
        [SpecificationRelatedSubstanceRowRequired] = [],
        [SpecificationStageLabelRequiredForStages] = [],
        [SpecificationTargetAndToleranceRequired] = [],
        [SpecificationToleranceModeRequired] = [],
        [SpecificationToleranceMustBePositive] = [],
        [SpecificationUnsupportedLimitType] = ["limitType"],
        [SpecificationUpperLimitRequiredForNotMoreThan] = [],
        [SpecificationWeightVariationLabelClaimNotAllowed] = [],
        [SpecificationWeightVariationLabelClaimUnitNotAllowed] = [],
        [SpecificationWeightVariationLimitsNotAllowed] = [],
        [SpecificationWeightVariationUnitMg] = [],
    };
}
