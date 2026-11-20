namespace MicroLIMS.Shared.Messages;

// Oos.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string OosDocumentNotFound = "Oos.DocumentNotFound";
    public const string OosDocumentNotFoundForGroup = "Oos.DocumentNotFoundForGroup";
    public const string OosDocumentValidationFailed = "Oos.DocumentValidationFailed";
    public const string OosGroupNotFound = "Oos.GroupNotFound";
    public const string OosSupersessionReasonRequired = "Oos.SupersessionReasonRequired";
    public const string OosVoidedDocumentCannotBeSuperseded = "Oos.VoidedDocumentCannotBeSuperseded";
    public const string OosInvestigationOpen = "Oos.InvestigationOpen";
    public const string OosFailingResultWithoutInvestigation = "Oos.FailingResultWithoutInvestigation";
    public const string OosConfirmedCannotBeApproved = "Oos.ConfirmedCannotBeApproved";
    public const string OosRetestSamplesNotApproved = "Oos.RetestSamplesNotApproved";
    public const string OosRetestNeedsApprovedPlan = "Oos.RetestNeedsApprovedPlan";

    private static Dictionary<string, string[]> OosParameters() => new()
    {
        [OosDocumentNotFound] = ["documentId"],
        [OosDocumentNotFoundForGroup] = ["documentId", "oosGroupCode"],
        [OosDocumentValidationFailed] = ["message"],
        [OosGroupNotFound] = ["oosGroupCode"],
        [OosSupersessionReasonRequired] = [],
        [OosVoidedDocumentCannotBeSuperseded] = [],
        [OosInvestigationOpen] = ["number"],
        [OosFailingResultWithoutInvestigation] = [],
        [OosConfirmedCannotBeApproved] = [],
        [OosRetestSamplesNotApproved] = ["number"],
        [OosRetestNeedsApprovedPlan] = [],
    };
}
