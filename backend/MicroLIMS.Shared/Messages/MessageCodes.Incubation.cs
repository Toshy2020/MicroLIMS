namespace MicroLIMS.Shared.Messages;

// Incubation.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string IncubationAllStepsComplete = "Incubation.AllStepsComplete";
    public const string IncubationEmResultsViaBatchEndpoint = "Incubation.EmResultsViaBatchEndpoint";
    public const string IncubationFinalWindowRecordResults = "Incubation.FinalWindowRecordResults";
    public const string IncubationGrowthObservationRequiredForEveryLocation = "Incubation.GrowthObservationRequiredForEveryLocation";
    public const string IncubationMediaAlreadySelected = "Incubation.MediaAlreadySelected";
    public const string IncubationMediaLotNotFound = "Incubation.MediaLotNotFound";
    public const string IncubationMediaLotNotReleased = "Incubation.MediaLotNotReleased";
    public const string IncubationMediaRequiredBeforeResult = "Incubation.MediaRequiredBeforeResult";
    public const string IncubationMediaRequiredBeforeStage2 = "Incubation.MediaRequiredBeforeStage2";
    public const string IncubationMediaRequiredForBatchResults = "Incubation.MediaRequiredForBatchResults";
    public const string IncubationNoOpenWindow = "Incubation.NoOpenWindow";
    public const string IncubationNoSimpleGrowthObservation = "Incubation.NoSimpleGrowthObservation";
    public const string IncubationNoUnitMappingForLocation = "Incubation.NoUnitMappingForLocation";
    public const string IncubationNotFinalWindow = "Incubation.NotFinalWindow";
    public const string IncubationNotFound = "Incubation.NotFound";
    public const string IncubationNotPlateCountStep = "Incubation.NotPlateCountStep";
    public const string IncubationPlateReadingRequiredFor = "Incubation.PlateReadingRequiredFor";
    public const string IncubationPlateReadingRequiredForAverage = "Incubation.PlateReadingRequiredForAverage";
    public const string IncubationSampleNotFoundForTest = "Incubation.SampleNotFoundForTest";
    public const string IncubationStage2AlreadyStarted = "Incubation.Stage2AlreadyStarted";
    public const string IncubationStage2StartBeforeStage1 = "Incubation.Stage2StartBeforeStage1";
    public const string IncubationStepHasNoMedia = "Incubation.StepHasNoMedia";
    public const string IncubationStepHasNoStage2Config = "Incubation.StepHasNoStage2Config";
    public const string IncubationStepNoTwoStageTransfer = "Incubation.StepNoTwoStageTransfer";
    public const string IncubationStepNotInTemplate = "Incubation.StepNotInTemplate";
    public const string IncubationTransferStartStage2Instead = "Incubation.TransferStartStage2Instead";
    public const string IncubationUnrecognizedResultPayload = "Incubation.UnrecognizedResultPayload";
    public const string IncubationWindowMinHoursNotReached = "Incubation.WindowMinHoursNotReached";
    public const string IncubationWrongMedium = "Incubation.WrongMedium";

    private static Dictionary<string, string[]> IncubationParameters() => new()
    {
        [IncubationAllStepsComplete] = ["testCode"],
        [IncubationEmResultsViaBatchEndpoint] = [],
        [IncubationFinalWindowRecordResults] = [],
        [IncubationGrowthObservationRequiredForEveryLocation] = ["step"],
        [IncubationMediaAlreadySelected] = ["step"],
        [IncubationMediaLotNotFound] = ["mediaLotId"],
        [IncubationMediaLotNotReleased] = ["lot"],
        [IncubationMediaRequiredBeforeResult] = ["step"],
        [IncubationMediaRequiredBeforeStage2] = ["step"],
        [IncubationMediaRequiredForBatchResults] = [],
        [IncubationNoOpenWindow] = [],
        [IncubationNoSimpleGrowthObservation] = ["step"],
        [IncubationNoUnitMappingForLocation] = ["locationId", "machineTestType", "roomTestType"],
        [IncubationNotFinalWindow] = ["step"],
        [IncubationNotFound] = ["incubationId"],
        [IncubationNotPlateCountStep] = ["step"],
        [IncubationPlateReadingRequiredFor] = ["locations"],
        [IncubationPlateReadingRequiredForAverage] = [],
        [IncubationSampleNotFoundForTest] = [],
        [IncubationStage2AlreadyStarted] = ["step"],
        [IncubationStage2StartBeforeStage1] = ["stage1Start", "stage2Start"],
        [IncubationStepHasNoMedia] = ["step"],
        [IncubationStepHasNoStage2Config] = ["step"],
        [IncubationStepNoTwoStageTransfer] = ["step"],
        [IncubationStepNotInTemplate] = ["step", "testCode"],
        [IncubationTransferStartStage2Instead] = [],
        [IncubationUnrecognizedResultPayload] = [],
        [IncubationWindowMinHoursNotReached] = ["minHours", "readyAt"],
        [IncubationWrongMedium] = ["medium"],
    };
}
