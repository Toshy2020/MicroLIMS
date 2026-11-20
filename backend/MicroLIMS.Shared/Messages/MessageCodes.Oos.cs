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

    public const string OosTemplateInvalid = "Oos.TemplateInvalid";
    public const string OosSectionNotFound = "Oos.SectionNotFound";
    public const string OosSectionAccessDenied = "Oos.SectionAccessDenied";
    public const string OosVersionRequired = "Oos.VersionRequired";
    public const string OosVersionConflict = "Oos.VersionConflict";
    public const string OosCategoryAccessDenied = "Oos.CategoryAccessDenied";
    public const string OosCategoryInvalid = "Oos.CategoryInvalid";
    public const string OosCategoryFixed = "Oos.CategoryFixed";
    public const string OosCategoryNotFound = "Oos.CategoryNotFound";
    public const string OosInvestigationNotFound = "Oos.InvestigationNotFound";
    public const string OosInvestigationAccessDenied = "Oos.InvestigationAccessDenied";
    public const string OosStatementPerformerRequired = "Oos.StatementPerformerRequired";
    public const string OosStatementInvalid = "Oos.StatementInvalid";
    public const string OosChecklistAnswerInvalid = "Oos.ChecklistAnswerInvalid";
    public const string OosPhaseOneClosed = "Oos.PhaseOneClosed";
    public const string OosPhaseOneTemplateEmpty = "Oos.PhaseOneTemplateEmpty";
    public const string OosPhaseOneStatementRequired = "Oos.PhaseOneStatementRequired";
    public const string OosPhaseOneChecklistIncomplete = "Oos.PhaseOneChecklistIncomplete";
    public const string OosSignerPerformedTest = "Oos.SignerPerformedTest";
    public const string OosPhaseOneAssessmentInvalid = "Oos.PhaseOneAssessmentInvalid";
    public const string OosPhaseOneAssignableCauseRequired = "Oos.PhaseOneAssignableCauseRequired";
    public const string OosPhaseOneCauseInactive = "Oos.PhaseOneCauseInactive";
    public const string OosPhaseOnePasswordFailed = "Oos.PhaseOnePasswordFailed";

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
        [OosTemplateInvalid] = [],
        [OosSectionNotFound] = [],
        [OosSectionAccessDenied] = [],
        [OosVersionRequired] = [],
        [OosVersionConflict] = [],
        [OosCategoryAccessDenied] = [],
        [OosCategoryInvalid] = [],
        [OosCategoryFixed] = [],
        [OosCategoryNotFound] = [],
        [OosInvestigationNotFound] = [],
        [OosInvestigationAccessDenied] = [],
        [OosStatementPerformerRequired] = [],
        [OosStatementInvalid] = [],
        [OosChecklistAnswerInvalid] = [],
        [OosPhaseOneClosed] = [],
        [OosPhaseOneTemplateEmpty] = [],
        [OosPhaseOneStatementRequired] = [],
        [OosPhaseOneChecklistIncomplete] = [],
        [OosSignerPerformedTest] = [],
        [OosPhaseOneAssessmentInvalid] = [],
        [OosPhaseOneAssignableCauseRequired] = [],
        [OosPhaseOneCauseInactive] = [],
        [OosPhaseOnePasswordFailed] = [],
    };
}
