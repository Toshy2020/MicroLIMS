namespace MicroLIMS.Shared.Messages;

public static partial class MessageCodes
{
    public const string QualityNotFound = "Quality.NotFound";
    public const string QualityAccessDenied = "Quality.AccessDenied";
    public const string QualityInvalidType = "Quality.InvalidType";
    public const string QualityInvalidTarget = "Quality.InvalidTarget";
    public const string QualityInvalidText = "Quality.InvalidText";
    public const string QualityStale = "Quality.Stale";
    public const string QualityVersionRequired = "Quality.VersionRequired";
    public const string QualityRaiserOrOwner = "Quality.RaiserOrOwner";
    public const string QualityTransactionRequired = "Quality.TransactionRequired";
    public const string QualitySelfLink = "Quality.SelfLink";
    public const string QualityDuplicateLink = "Quality.DuplicateLink";
    public const string QualitySignedLink = "Quality.SignedLink";
    public const string QualityInvalidAttachment = "Quality.InvalidAttachment";
    public const string QualityDuplicateItem = "Quality.DuplicateItem";
    public const string QualityDuplicateClassification = "Quality.DuplicateClassification";
    public const string QualityInvalidWorkingDays = "Quality.InvalidWorkingDays";
    public const string QualityInvalidAnswer = "Quality.InvalidAnswer";
    public const string QualityExtensionAlreadyDecided = "Quality.ExtensionAlreadyDecided";
    public const string QualityExtensionSelfDecision = "Quality.ExtensionSelfDecision";
    public const string QualityExtensionDate = "Quality.ExtensionDate";
    public const string QualityExtensionSignatureFailed = "Quality.ExtensionSignatureFailed";
    public const string QualityRecordTerminal = "Quality.RecordTerminal";

    public const string DeviationRaiseRoute = "Deviation.RaiseRoute";
    public const string DeviationOccurrenceUtc = "Deviation.OccurrenceUtc";
    public const string DeviationUseReject = "Deviation.UseReject";
    public const string DeviationClassification = "Deviation.Classification";
    public const string DeviationCause = "Deviation.Cause";
    public const string DeviationOwnerLabConclusion = "Deviation.OwnerLabConclusion";
    public const string DeviationIncomplete = "Deviation.Incomplete";
    public const string DeviationCapaRequired = "Deviation.CapaRequired";
    public const string DeviationUnfinishedActions = "Deviation.UnfinishedActions";
    public const string DeviationInvalidOwner = "Deviation.InvalidOwner";
    public const string DeviationAnalystOwner = "Deviation.AnalystOwner";
    public const string DeviationWrongState = "Deviation.WrongState";

    public const string DeviationFutureOccurrence = "Deviation.FutureOccurrence";
    public const string DeviationPastDueDate = "Deviation.PastDueDate";
    public const string DeviationFinalCancellation = "Deviation.FinalCancellation";
    public const string DeviationDuplicateSource = "Deviation.DuplicateSource";

    public const string CapaPlanEmpty = "Capa.PlanEmpty";
    public const string CapaActionKindInvalid = "Capa.ActionKindInvalid";
    public const string CapaActionResponsibleInvalid = "Capa.ActionResponsibleInvalid";
    public const string CapaActionPastDueDate = "Capa.ActionPastDueDate";
    public const string CapaActionCannotBeRemoved = "Capa.ActionCannotBeRemoved";
    public const string CapaEffectivenessDateOrder = "Capa.EffectivenessDateOrder";
    public const string CapaPlanIncomplete = "Capa.PlanIncomplete";
    public const string CapaActionAlreadyDone = "Capa.ActionAlreadyDone";
    public const string CapaActionEvidenceLinkInvalid = "Capa.ActionEvidenceLinkInvalid";
    public const string CapaUnfinishedActions = "Capa.UnfinishedActions";
    public const string CapaEffectivenessEarly = "Capa.EffectivenessEarly";
    public const string CapaNewCapaRequired = "Capa.NewCapaRequired";
    public const string CapaInvalidOwner = "Capa.InvalidOwner";
    public const string CapaWrongState = "Capa.WrongState";
    public const string CapaFinalCancellation = "Capa.FinalCancellation";

    private static Dictionary<string, string[]> QualityParameters() => new()
    {
        [CapaPlanEmpty] = [],
        [CapaActionKindInvalid] = [],
        [CapaActionResponsibleInvalid] = [],
        [CapaActionPastDueDate] = [],
        [CapaActionCannotBeRemoved] = [],
        [CapaEffectivenessDateOrder] = [],
        [CapaPlanIncomplete] = [],
        [CapaActionAlreadyDone] = [],
        [CapaActionEvidenceLinkInvalid] = [],
        [CapaUnfinishedActions] = ["action"],
        [CapaEffectivenessEarly] = [],
        [CapaNewCapaRequired] = [],
        [CapaInvalidOwner] = [],
        [CapaWrongState] = [],
        [CapaFinalCancellation] = [],
        [DeviationRaiseRoute] = [],
        [DeviationOccurrenceUtc] = [],
        [DeviationUseReject] = [],
        [DeviationClassification] = [],
        [DeviationCause] = [],
        [DeviationOwnerLabConclusion] = [],
        [DeviationIncomplete] = [],
        [DeviationCapaRequired] = [],
        [DeviationFutureOccurrence] = [],
        [DeviationPastDueDate] = [],
        [DeviationFinalCancellation] = [],
        [DeviationDuplicateSource] = ["number"],
        [DeviationUnfinishedActions] = ["action"],
        [DeviationInvalidOwner] = [],
        [DeviationAnalystOwner] = [],
        [DeviationWrongState] = [],
        [QualityNotFound] = [],
        [QualityAccessDenied] = [],
        [QualityInvalidType] = [],
        [QualityInvalidTarget] = [],
        [QualityInvalidText] = ["max"],
        [QualityStale] = [],
        [QualityVersionRequired] = [],
        [QualityRaiserOrOwner] = [],
        [QualityTransactionRequired] = [],
        [QualitySelfLink] = [],
        [QualityDuplicateLink] = [],
        [QualitySignedLink] = [],
        [QualityInvalidAttachment] = [],
        [QualityDuplicateItem] = [],
        [QualityDuplicateClassification] = [],
        [QualityInvalidWorkingDays] = [],
        [QualityInvalidAnswer] = [],
        [QualityExtensionAlreadyDecided] = [],
        [QualityExtensionSelfDecision] = [],
        [QualityExtensionDate] = [],
        [QualityExtensionSignatureFailed] = [],
        [QualityRecordTerminal] = [],
    };
}
