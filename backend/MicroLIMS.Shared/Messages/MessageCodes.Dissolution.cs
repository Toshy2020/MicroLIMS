namespace MicroLIMS.Shared.Messages;

// Dissolution.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string DissolutionCalculationDataDeserializationFailed = "Dissolution.CalculationDataDeserializationFailed";
    public const string DissolutionConditionCountMismatch = "Dissolution.ConditionCountMismatch";
    public const string DissolutionConditionFieldRequired = "Dissolution.ConditionFieldRequired";
    public const string DissolutionConditionsRequired = "Dissolution.ConditionsRequired";
    public const string DissolutionDilutionFactorPositive = "Dissolution.DilutionFactorPositive";
    public const string DissolutionInvalidExistingVesselCount = "Dissolution.InvalidExistingVesselCount";
    public const string DissolutionInvalidUnitCount = "Dissolution.InvalidUnitCount";
    public const string DissolutionLabelClaimPositive = "Dissolution.LabelClaimPositive";
    public const string DissolutionMediumVolumePositive = "Dissolution.MediumVolumePositive";
    public const string DissolutionMissingCalculationData = "Dissolution.MissingCalculationData";
    public const string DissolutionNextStageNotRequired = "Dissolution.NextStageNotRequired";
    public const string DissolutionNoActiveEntry = "Dissolution.NoActiveEntry";
    public const string DissolutionNoActiveParameterResult = "Dissolution.NoActiveParameterResult";
    public const string DissolutionNoConditionFieldsConfigured = "Dissolution.NoConditionFieldsConfigured";
    public const string DissolutionPasswordRequired = "Dissolution.PasswordRequired";
    public const string DissolutionQOutOfRange = "Dissolution.QOutOfRange";
    public const string DissolutionStage1VesselCount = "Dissolution.Stage1VesselCount";
    public const string DissolutionStage2VesselCount = "Dissolution.Stage2VesselCount";
    public const string DissolutionStage3VesselCount = "Dissolution.Stage3VesselCount";
    public const string DissolutionStageOffsetsNonNegative = "Dissolution.StageOffsetsNonNegative";
    public const string DissolutionStandardConcentrationPositive = "Dissolution.StandardConcentrationPositive";
    public const string DissolutionStandardDilutionPositive = "Dissolution.StandardDilutionPositive";
    public const string DissolutionStandardMeanAreaPositive = "Dissolution.StandardMeanAreaPositive";
    public const string DissolutionStandardMoistureRange = "Dissolution.StandardMoistureRange";
    public const string DissolutionStandardProblem = "Dissolution.StandardProblem";
    public const string DissolutionStandardPurityPositive = "Dissolution.StandardPurityPositive";
    public const string DissolutionSupersededOrder = "Dissolution.SupersededOrder";
    public const string DissolutionTestDefinitionNotFound = "Dissolution.TestDefinitionNotFound";
    public const string DissolutionUnexpectedConditionField = "Dissolution.UnexpectedConditionField";
    public const string DissolutionVesselAreaPositive = "Dissolution.VesselAreaPositive";
    public const string DissolutionVesselAreasRequired = "Dissolution.VesselAreasRequired";
    public const string DissolutionWrongWorkflow = "Dissolution.WrongWorkflow";

    private static Dictionary<string, string[]> DissolutionParameters() => new()
    {
        [DissolutionCalculationDataDeserializationFailed] = [],
        [DissolutionConditionCountMismatch] = ["expected", "received"],
        [DissolutionConditionFieldRequired] = ["field"],
        [DissolutionConditionsRequired] = [],
        [DissolutionDilutionFactorPositive] = [],
        [DissolutionInvalidExistingVesselCount] = ["count"],
        [DissolutionInvalidUnitCount] = ["count"],
        [DissolutionLabelClaimPositive] = [],
        [DissolutionMediumVolumePositive] = [],
        [DissolutionMissingCalculationData] = [],
        [DissolutionNextStageNotRequired] = [],
        [DissolutionNoActiveEntry] = [],
        [DissolutionNoActiveParameterResult] = [],
        [DissolutionNoConditionFieldsConfigured] = [],
        [DissolutionPasswordRequired] = [],
        [DissolutionQOutOfRange] = [],
        [DissolutionStage1VesselCount] = [],
        [DissolutionStage2VesselCount] = [],
        [DissolutionStage3VesselCount] = [],
        [DissolutionStageOffsetsNonNegative] = [],
        [DissolutionStandardConcentrationPositive] = [],
        [DissolutionStandardDilutionPositive] = [],
        [DissolutionStandardMeanAreaPositive] = [],
        [DissolutionStandardMoistureRange] = [],
        [DissolutionStandardProblem] = ["message"],
        [DissolutionStandardPurityPositive] = [],
        [DissolutionSupersededOrder] = [],
        [DissolutionTestDefinitionNotFound] = ["testCode"],
        [DissolutionUnexpectedConditionField] = ["field"],
        [DissolutionVesselAreaPositive] = [],
        [DissolutionVesselAreasRequired] = [],
        [DissolutionWrongWorkflow] = ["testOrderId"],
    };
}
