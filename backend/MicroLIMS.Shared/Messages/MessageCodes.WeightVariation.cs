namespace MicroLIMS.Shared.Messages;

// WeightVariation.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string WeightVariationCalculationDataDeserializationFailed = "WeightVariation.CalculationDataDeserializationFailed";
    public const string WeightVariationCapsuleFieldsOnly = "WeightVariation.CapsuleFieldsOnly";
    public const string WeightVariationCapsuleFieldsRequired = "WeightVariation.CapsuleFieldsRequired";
    public const string WeightVariationCapsulePercentageOrder = "WeightVariation.CapsulePercentageOrder";
    public const string WeightVariationCapsuleS1Order = "WeightVariation.CapsuleS1Order";
    public const string WeightVariationCapsuleS2MaxOutsideTooHigh = "WeightVariation.CapsuleS2MaxOutsideTooHigh";
    public const string WeightVariationDosageFormNotConfigured = "WeightVariation.DosageFormNotConfigured";
    public const string WeightVariationInvalidUnitCount = "WeightVariation.InvalidUnitCount";
    public const string WeightVariationMaxOutsideNonNegative = "WeightVariation.MaxOutsideNonNegative";
    public const string WeightVariationMissingCalculationData = "WeightVariation.MissingCalculationData";
    public const string WeightVariationNextStageNotRequired = "WeightVariation.NextStageNotRequired";
    public const string WeightVariationNoActiveEntry = "WeightVariation.NoActiveEntry";
    public const string WeightVariationNoActiveParameterResult = "WeightVariation.NoActiveParameterResult";
    public const string WeightVariationPercentagesPositive = "WeightVariation.PercentagesPositive";
    public const string WeightVariationShellLessThanGross = "WeightVariation.ShellLessThanGross";
    public const string WeightVariationShellWeightPositive = "WeightVariation.ShellWeightPositive";
    public const string WeightVariationStage1UnitCount = "WeightVariation.Stage1UnitCount";
    public const string WeightVariationStage2UnitCount = "WeightVariation.Stage2UnitCount";
    public const string WeightVariationTabletBandOrder = "WeightVariation.TabletBandOrder";
    public const string WeightVariationTabletBandWeightLimitsPositive = "WeightVariation.TabletBandWeightLimitsPositive";
    public const string WeightVariationTabletFieldsOnly = "WeightVariation.TabletFieldsOnly";
    public const string WeightVariationTabletWeightPositive = "WeightVariation.TabletWeightPositive";
    public const string WeightVariationTabletsNoStage2 = "WeightVariation.TabletsNoStage2";
    public const string WeightVariationUnitCountMinOne = "WeightVariation.UnitCountMinOne";
    public const string WeightVariationUnitsRequired = "WeightVariation.UnitsRequired";
    public const string WeightVariationWrongWorkflow = "WeightVariation.WrongWorkflow";

    private static Dictionary<string, string[]> WeightVariationParameters() => new()
    {
        [WeightVariationCalculationDataDeserializationFailed] = [],
        [WeightVariationCapsuleFieldsOnly] = ["unit"],
        [WeightVariationCapsuleFieldsRequired] = ["unit"],
        [WeightVariationCapsulePercentageOrder] = [],
        [WeightVariationCapsuleS1Order] = [],
        [WeightVariationCapsuleS2MaxOutsideTooHigh] = ["total"],
        [WeightVariationDosageFormNotConfigured] = [],
        [WeightVariationInvalidUnitCount] = ["stage1", "stage2", "count"],
        [WeightVariationMaxOutsideNonNegative] = [],
        [WeightVariationMissingCalculationData] = [],
        [WeightVariationNextStageNotRequired] = [],
        [WeightVariationNoActiveEntry] = [],
        [WeightVariationNoActiveParameterResult] = [],
        [WeightVariationPercentagesPositive] = [],
        [WeightVariationShellLessThanGross] = ["unit"],
        [WeightVariationShellWeightPositive] = ["unit"],
        [WeightVariationStage1UnitCount] = ["count"],
        [WeightVariationStage2UnitCount] = ["count"],
        [WeightVariationTabletBandOrder] = [],
        [WeightVariationTabletBandWeightLimitsPositive] = [],
        [WeightVariationTabletFieldsOnly] = ["unit"],
        [WeightVariationTabletWeightPositive] = ["unit"],
        [WeightVariationTabletsNoStage2] = [],
        [WeightVariationUnitCountMinOne] = [],
        [WeightVariationUnitsRequired] = [],
        [WeightVariationWrongWorkflow] = ["testOrderId"],
    };
}
