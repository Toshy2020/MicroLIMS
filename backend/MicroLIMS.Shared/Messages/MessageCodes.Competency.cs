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
    public const string CompetencyOverridePermissionRequired = "Competency.OverridePermissionRequired";
    public const string CompetencyOverrideSelf = "Competency.OverrideSelf";
    public const string CompetencyOverrideReasonRequired = "Competency.OverrideReasonRequired";
    public const string CompetencyOverrideOrderClosed = "Competency.OverrideOrderClosed";
    public const string CompetencyOverrideAnalystIneligible = "Competency.OverrideAnalystIneligible";
    public const string CompetencyOverrideAlreadyActive = "Competency.OverrideAlreadyActive";
    public const string CompetencyOverrideNotNeeded = "Competency.OverrideNotNeeded";
    public const string CompetencyOverridePasswordFailed = "Competency.OverridePasswordFailed";
    public const string CompetencyOverrideConcurrencyConflict = "Competency.OverrideConcurrencyConflict";
    public const string CompetencyOverrideAlreadySignedOff = "Competency.OverrideAlreadySignedOff";
    public const string CompetencyManageRequired = "Competency.ManageRequired";
    public const string CompetencyNotQualified = "Competency.NotQualified";
    public const string CompetencyAssignmentRefused = "Competency.AssignmentRefused";
    public const string CompetencyRecordingRefused = "Competency.RecordingRefused";
    public const string CompetencyQualificationExpired = "Competency.QualificationExpired";
    public const string CompetencyQualificationWithdrawn = "Competency.QualificationWithdrawn";
    public const string CompetencySopNotAcknowledged = "Competency.SopNotAcknowledged";
    public const string CompetencyAssigneeNotQualified = "Competency.AssigneeNotQualified";
    public const string CompetencyAssigneeQualificationExpired = "Competency.AssigneeQualificationExpired";
    public const string CompetencyAssigneeQualificationWithdrawn = "Competency.AssigneeQualificationWithdrawn";
    public const string CompetencyAssigneeSopNotAcknowledged = "Competency.AssigneeSopNotAcknowledged";
    public const string CompetencySopNotCheckedNoSopNumber = "Competency.SopNotCheckedNoSopNumber";
    public const string CompetencySopNotCheckedNotFound = "Competency.SopNotCheckedNotFound";
    public const string CompetencySopNotCheckedNoEffectiveRevision = "Competency.SopNotCheckedNoEffectiveRevision";
    public const string CompetencySopNotCheckedAmbiguous = "Competency.SopNotCheckedAmbiguous";
    public const string CompetencyGateReasonRequired = "Competency.GateReasonRequired";
    public const string CompetencyGateVersionStale = "Competency.GateVersionStale";
    public const string CompetencySectionScopeDenied = "Competency.SectionScopeDenied";
    public const string CompetencyViewRequired = "Competency.ViewRequired";

    private static Dictionary<string, string[]> CompetencyParameters() => new()
    {
        [CompetencyAttachmentInvalid] = ["detail"],
        [CompetencyAttachmentLimit] = ["maxFiles", "maxFileMb"],
        [CompetencyAttachmentNameTooLong] = [],
        [CompetencyGateReasonRequired] = [],
        [CompetencyGateVersionStale] = [],
        [CompetencyIntervalInvalid] = [],
        [CompetencyOverridePermissionRequired] = [],
        [CompetencyOverrideSelf] = [],
        [CompetencyOverrideReasonRequired] = [],
        [CompetencyOverrideOrderClosed] = [],
        [CompetencyOverrideAnalystIneligible] = [],
        [CompetencyOverrideAlreadyActive] = [],
        [CompetencyOverrideNotNeeded] = [],
        [CompetencyOverridePasswordFailed] = [],
        [CompetencyOverrideConcurrencyConflict] = [],
        [CompetencyOverrideAlreadySignedOff] = [],
        [CompetencyManageRequired] = [],
        [CompetencyQualificationAlreadyWithdrawn] = [],
        [CompetencyQualificationIneligible] = [],
        [CompetencyQualificationInvalid] = [],
        [CompetencyQualificationNotFound] = [],
        [CompetencyQualificationSelfSign] = [],
        [CompetencyWithdrawalReasonRequired] = [],
        [CompetencyNotQualified] = ["test"],
        [CompetencyAssignmentRefused] = [],
        [CompetencyRecordingRefused] = [],
        [CompetencyQualificationExpired] = ["test", "date"],
        [CompetencyQualificationWithdrawn] = ["test", "date"],
        [CompetencySopNotAcknowledged] = ["documentCode", "revision"],
        [CompetencyAssigneeNotQualified] = ["analyst", "test"],
        [CompetencyAssigneeQualificationExpired] = ["analyst", "test", "date"],
        [CompetencyAssigneeQualificationWithdrawn] = ["analyst", "test", "date"],
        [CompetencyAssigneeSopNotAcknowledged] = ["analyst", "documentCode", "revision"],
        [CompetencySopNotCheckedNoSopNumber] = [],
        [CompetencySopNotCheckedNotFound] = ["sopNumber"],
        [CompetencySopNotCheckedNoEffectiveRevision] = ["documentCode"],
        [CompetencySopNotCheckedAmbiguous] = ["sopNumber"],
        [CompetencySectionScopeDenied] = [],
        [CompetencyViewRequired] = [],
    };
}
