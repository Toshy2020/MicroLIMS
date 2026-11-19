namespace MicroLIMS.Shared.Messages;

// Calculation.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string CalculationActualWeightPositive = "Calculation.ActualWeightPositive";
    public const string CalculationExtractionVolumePositive = "Calculation.ExtractionVolumePositive";
    public const string CalculationFinalWeightNonNegative = "Calculation.FinalWeightNonNegative";
    public const string CalculationFinalWeightNotAboveInitial = "Calculation.FinalWeightNotAboveInitial";
    public const string CalculationInitialWeightPositive = "Calculation.InitialWeightPositive";
    public const string CalculationInvalidCleaningTechnique = "Calculation.InvalidCleaningTechnique";
    public const string CalculationReplicateResponseNonNegative = "Calculation.ReplicateResponseNonNegative";
    public const string CalculationSampleSolutionVolumePositive = "Calculation.SampleSolutionVolumePositive";
    public const string CalculationSpecificGravityPositive = "Calculation.SpecificGravityPositive";
    public const string CalculationStandardConcentrationPositive = "Calculation.StandardConcentrationPositive";
    public const string CalculationStandardMeanResponsePositive = "Calculation.StandardMeanResponsePositive";
    public const string CalculationSwabAreaRequired = "Calculation.SwabAreaRequired";
    public const string CalculationTargetConcentrationPositive = "Calculation.TargetConcentrationPositive";
    public const string CalculationTheoreticalAmountPositive = "Calculation.TheoreticalAmountPositive";

    private static Dictionary<string, string[]> CalculationParameters() => new()
    {
        [CalculationActualWeightPositive] = [],
        [CalculationExtractionVolumePositive] = [],
        [CalculationFinalWeightNonNegative] = [],
        [CalculationFinalWeightNotAboveInitial] = [],
        [CalculationInitialWeightPositive] = [],
        [CalculationInvalidCleaningTechnique] = ["technique"],
        [CalculationReplicateResponseNonNegative] = ["replicate"],
        [CalculationSampleSolutionVolumePositive] = [],
        [CalculationSpecificGravityPositive] = [],
        [CalculationStandardConcentrationPositive] = [],
        [CalculationStandardMeanResponsePositive] = [],
        [CalculationSwabAreaRequired] = [],
        [CalculationTargetConcentrationPositive] = [],
        [CalculationTheoreticalAmountPositive] = [],
    };
}
