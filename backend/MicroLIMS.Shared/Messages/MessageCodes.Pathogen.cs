namespace MicroLIMS.Shared.Messages;

// Pathogen.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string PathogenAnalystDecisionNotAvailable = "Pathogen.AnalystDecisionNotAvailable";
    public const string PathogenBiochemicalNeedsConformingResult = "Pathogen.BiochemicalNeedsConformingResult";
    public const string PathogenConfirmatoryPlatingNotCompleted = "Pathogen.ConfirmatoryPlatingNotCompleted";
    public const string PathogenIncubationAlreadyStarted = "Pathogen.IncubationAlreadyStarted";
    public const string PathogenIncubatorNotFound = "Pathogen.IncubatorNotFound";
    public const string PathogenMediaLotNotReleased = "Pathogen.MediaLotNotReleased";
    public const string PathogenMediaNotFound = "Pathogen.MediaNotFound";
    public const string PathogenMediaRequiredBeforeSubmission = "Pathogen.MediaRequiredBeforeSubmission";
    public const string PathogenNoActiveIncubation = "Pathogen.NoActiveIncubation";
    public const string PathogenNoBiochemicalDecisionToMake = "Pathogen.NoBiochemicalDecisionToMake";
    public const string PathogenNoPrecedingPlateStep = "Pathogen.NoPrecedingPlateStep";
    public const string PathogenNoTestDefinition = "Pathogen.NoTestDefinition";
    public const string PathogenNotBiochemicalStep = "Pathogen.NotBiochemicalStep";
    public const string PathogenNotBrothStep = "Pathogen.NotBrothStep";
    public const string PathogenNotConfirmatoryPlatingStep = "Pathogen.NotConfirmatoryPlatingStep";
    public const string PathogenNotSelectivePlatingStep = "Pathogen.NotSelectivePlatingStep";
    public const string PathogenPrecedingStepNotCompleted = "Pathogen.PrecedingStepNotCompleted";
    public const string PathogenReturnReasonRequired = "Pathogen.ReturnReasonRequired";
    public const string PathogenSampleNotFound = "Pathogen.SampleNotFound";
    public const string PathogenSessionNotFound = "Pathogen.SessionNotFound";
    public const string PathogenStepHasNoMedium = "Pathogen.StepHasNoMedium";
    public const string PathogenStepHasNoOrganism = "Pathogen.StepHasNoOrganism";
    public const string PathogenStepHasNoPermittedMedia = "Pathogen.StepHasNoPermittedMedia";
    public const string PathogenStepNotInTest = "Pathogen.StepNotInTest";
    public const string PathogenStepResultNotFound = "Pathogen.StepResultNotFound";
    public const string PathogenStepResultWrongType = "Pathogen.StepResultWrongType";
    public const string PathogenTestOrderNotFound = "Pathogen.TestOrderNotFound";
    public const string PathogenUseDedicatedEndpoints = "Pathogen.UseDedicatedEndpoints";

    private static Dictionary<string, string[]> PathogenParameters() => new()
    {
        [PathogenAnalystDecisionNotAvailable] = [],
        [PathogenBiochemicalNeedsConformingResult] = ["step"],
        [PathogenConfirmatoryPlatingNotCompleted] = [],
        [PathogenIncubationAlreadyStarted] = ["step"],
        [PathogenIncubatorNotFound] = ["incubatorId"],
        [PathogenMediaLotNotReleased] = ["lot"],
        [PathogenMediaNotFound] = ["mediaLotId"],
        [PathogenMediaRequiredBeforeSubmission] = ["step"],
        [PathogenNoActiveIncubation] = ["step"],
        [PathogenNoBiochemicalDecisionToMake] = ["resultId"],
        [PathogenNoPrecedingPlateStep] = ["step"],
        [PathogenNoTestDefinition] = ["testCode"],
        [PathogenNotBiochemicalStep] = ["step"],
        [PathogenNotBrothStep] = ["step"],
        [PathogenNotConfirmatoryPlatingStep] = ["step"],
        [PathogenNotSelectivePlatingStep] = ["step"],
        [PathogenPrecedingStepNotCompleted] = ["step"],
        [PathogenReturnReasonRequired] = [],
        [PathogenSampleNotFound] = ["sampleId"],
        [PathogenSessionNotFound] = ["sampleId"],
        [PathogenStepHasNoMedium] = ["step"],
        [PathogenStepHasNoOrganism] = ["step"],
        [PathogenStepHasNoPermittedMedia] = ["step"],
        [PathogenStepNotInTest] = ["step", "testCode"],
        [PathogenStepResultNotFound] = ["resultId"],
        [PathogenStepResultWrongType] = ["resultId", "stepType"],
        [PathogenTestOrderNotFound] = ["testOrderId"],
        [PathogenUseDedicatedEndpoints] = ["step"],
    };
}
