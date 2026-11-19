namespace MicroLIMS.Shared.Messages;

// Disintegration.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string DisintegrationCalculationDataDeserializationFailed = "Disintegration.CalculationDataDeserializationFailed";
    public const string DisintegrationInvalidExistingUnitCount = "Disintegration.InvalidExistingUnitCount";
    public const string DisintegrationInvalidUnitCount = "Disintegration.InvalidUnitCount";
    public const string DisintegrationMaxStage1FailuresRange = "Disintegration.MaxStage1FailuresRange";
    public const string DisintegrationMinPassTotalRange = "Disintegration.MinPassTotalRange";
    public const string DisintegrationMissingCalculationData = "Disintegration.MissingCalculationData";
    public const string DisintegrationNextStageNotRequired = "Disintegration.NextStageNotRequired";
    public const string DisintegrationNoActiveEntry = "Disintegration.NoActiveEntry";
    public const string DisintegrationNoActiveParameterResult = "Disintegration.NoActiveParameterResult";
    public const string DisintegrationStage1UnitCount = "Disintegration.Stage1UnitCount";
    public const string DisintegrationStage2UnitCount = "Disintegration.Stage2UnitCount";
    public const string DisintegrationStageUnitsMinOne = "Disintegration.StageUnitsMinOne";
    public const string DisintegrationTimeLimitPositive = "Disintegration.TimeLimitPositive";
    public const string DisintegrationTimePositive = "Disintegration.TimePositive";
    public const string DisintegrationUnitMinutesRequired = "Disintegration.UnitMinutesRequired";
    public const string DisintegrationWrongWorkflow = "Disintegration.WrongWorkflow";

    private static Dictionary<string, string[]> DisintegrationParameters() => new()
    {
        [DisintegrationCalculationDataDeserializationFailed] = [],
        [DisintegrationInvalidExistingUnitCount] = ["count"],
        [DisintegrationInvalidUnitCount] = ["stage1", "total", "count"],
        [DisintegrationMaxStage1FailuresRange] = ["max"],
        [DisintegrationMinPassTotalRange] = ["total"],
        [DisintegrationMissingCalculationData] = [],
        [DisintegrationNextStageNotRequired] = [],
        [DisintegrationNoActiveEntry] = [],
        [DisintegrationNoActiveParameterResult] = [],
        [DisintegrationStage1UnitCount] = ["count"],
        [DisintegrationStage2UnitCount] = ["count"],
        [DisintegrationStageUnitsMinOne] = [],
        [DisintegrationTimeLimitPositive] = [],
        [DisintegrationTimePositive] = [],
        [DisintegrationUnitMinutesRequired] = [],
        [DisintegrationWrongWorkflow] = ["testOrderId"],
    };
}
