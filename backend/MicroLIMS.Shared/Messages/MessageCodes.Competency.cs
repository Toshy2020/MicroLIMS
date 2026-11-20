namespace MicroLIMS.Shared.Messages;

public static partial class MessageCodes
{
    public const string CompetencyQualificationNotFound = "Competency.QualificationNotFound";
    public const string CompetencyQualificationInvalid = "Competency.QualificationInvalid";
    public const string CompetencyQualificationSelfSign = "Competency.QualificationSelfSign";
    public const string CompetencyQualificationIneligible = "Competency.QualificationIneligible";
    public const string CompetencyQualificationAlreadyWithdrawn = "Competency.QualificationAlreadyWithdrawn";
    public const string CompetencyIntervalInvalid = "Competency.IntervalInvalid";
    public const string CompetencyAttachmentInvalid = "Competency.AttachmentInvalid";
    public const string CompetencyAttachmentNameTooLong = "Competency.AttachmentNameTooLong";
    public const string CompetencyAttachmentLimit = "Competency.AttachmentLimit";
    public const string CompetencyWithdrawalReasonRequired = "Competency.WithdrawalReasonRequired";
    public const string CompetencyManageRequired = "Competency.ManageRequired";

    private static Dictionary<string, string[]> CompetencyParameters() => new()
    {
        [CompetencyAttachmentInvalid] = ["detail"],
        [CompetencyAttachmentLimit] = ["maxFiles", "maxFileMb"],
        [CompetencyAttachmentNameTooLong] = [],
        [CompetencyIntervalInvalid] = [],
        [CompetencyManageRequired] = [],
        [CompetencyQualificationAlreadyWithdrawn] = [],
        [CompetencyQualificationIneligible] = [],
        [CompetencyQualificationInvalid] = [],
        [CompetencyQualificationNotFound] = [],
        [CompetencyQualificationSelfSign] = [],
        [CompetencyWithdrawalReasonRequired] = []
    };
}
