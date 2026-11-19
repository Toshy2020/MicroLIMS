namespace MicroLIMS.Shared.Messages;

// User.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string UserAccessReviewAccountNotInReport = "User.AccessReviewAccountNotInReport";
    public const string UserAccessReviewAlreadySigned = "User.AccessReviewAlreadySigned";
    public const string UserAccessReviewCannotSignOwnAccess = "User.AccessReviewCannotSignOwnAccess";
    public const string UserAccessReviewChecksumMismatch = "User.AccessReviewChecksumMismatch";
    public const string UserAccessReviewConclusionRequired = "User.AccessReviewConclusionRequired";
    public const string UserAccessReviewConclusionTooLong = "User.AccessReviewConclusionTooLong";
    public const string UserAccessReviewFindingActionInvalid = "User.AccessReviewFindingActionInvalid";
    public const string UserAccessReviewFindingCannotMoveAccount = "User.AccessReviewFindingCannotMoveAccount";
    public const string UserAccessReviewFindingDetailRequired = "User.AccessReviewFindingDetailRequired";
    public const string UserAccessReviewFindingDetailTooLong = "User.AccessReviewFindingDetailTooLong";
    public const string UserAccessReviewFindingKindInvalid = "User.AccessReviewFindingKindInvalid";
    public const string UserAccessReviewFindingNotFound = "User.AccessReviewFindingNotFound";
    public const string UserAccessReviewFindingNoteRequired = "User.AccessReviewFindingNoteRequired";
    public const string UserAccessReviewFindingNoteTooLong = "User.AccessReviewFindingNoteTooLong";
    public const string UserAccessReviewNotFound = "User.AccessReviewNotFound";
    public const string UserAccessReviewPeriodLabelTooLong = "User.AccessReviewPeriodLabelTooLong";
    public const string UserAccessReviewQualityAssuranceOnly = "User.AccessReviewQualityAssuranceOnly";
    public const string UserAccessReviewReportUnreadable = "User.AccessReviewReportUnreadable";
    public const string UserCannotDisableOwnAccount = "User.CannotDisableOwnAccount";
    public const string UserCannotModifyOwnRole = "User.CannotModifyOwnRole";
    public const string UserCannotRelinkOwnCompanyAccount = "User.CannotRelinkOwnCompanyAccount";
    public const string UserCompanyUsernameRequired = "User.CompanyUsernameRequired";
    public const string UserDirectoryPermissionFilterNotAllowed = "User.DirectoryPermissionFilterNotAllowed";
    public const string UserDirectoryUsernameAlreadyLinked = "User.DirectoryUsernameAlreadyLinked";
    public const string UserEmergencyAdminOnly = "User.EmergencyAdminOnly";
    public const string UserEmergencyMaxCountReached = "User.EmergencyMaxCountReached";
    public const string UserEmergencyMustUseLocalSignIn = "User.EmergencyMustUseLocalSignIn";
    public const string UserEmergencyRequiresActiveMfa = "User.EmergencyRequiresActiveMfa";
    public const string UserFullNameRequired = "User.FullNameRequired";
    public const string UserNoCompanyUsernameToRelink = "User.NoCompanyUsernameToRelink";
    public const string UserNotFound = "User.NotFound";
    public const string UserPasswordPolicyFailed = "User.PasswordPolicyFailed";
    public const string UserSelectedRoleNotFound = "User.SelectedRoleNotFound";
    public const string UserStatusFilterInvalid = "User.StatusFilterInvalid";
    public const string UserUsernameAlreadyTaken = "User.UsernameAlreadyTaken";
    public const string UserUsernameRequired = "User.UsernameRequired";

    private static Dictionary<string, string[]> UserParameters() => new()
    {
        [UserAccessReviewAccountNotInReport] = [],
        [UserAccessReviewAlreadySigned] = [],
        [UserAccessReviewCannotSignOwnAccess] = [],
        [UserAccessReviewChecksumMismatch] = [],
        [UserAccessReviewConclusionRequired] = [],
        [UserAccessReviewConclusionTooLong] = [],
        [UserAccessReviewFindingActionInvalid] = [],
        [UserAccessReviewFindingCannotMoveAccount] = [],
        [UserAccessReviewFindingDetailRequired] = [],
        [UserAccessReviewFindingDetailTooLong] = [],
        [UserAccessReviewFindingKindInvalid] = ["kinds"],
        [UserAccessReviewFindingNotFound] = ["findingId"],
        [UserAccessReviewFindingNoteRequired] = [],
        [UserAccessReviewFindingNoteTooLong] = [],
        [UserAccessReviewNotFound] = ["reviewId"],
        [UserAccessReviewPeriodLabelTooLong] = [],
        [UserAccessReviewQualityAssuranceOnly] = [],
        [UserAccessReviewReportUnreadable] = [],
        [UserCannotDisableOwnAccount] = [],
        [UserCannotModifyOwnRole] = [],
        [UserCannotRelinkOwnCompanyAccount] = [],
        [UserCompanyUsernameRequired] = [],
        [UserDirectoryPermissionFilterNotAllowed] = ["requiredPermission"],
        [UserDirectoryUsernameAlreadyLinked] = ["directoryUsername"],
        [UserEmergencyAdminOnly] = [],
        [UserEmergencyMaxCountReached] = [],
        [UserEmergencyMustUseLocalSignIn] = [],
        [UserEmergencyRequiresActiveMfa] = [],
        [UserFullNameRequired] = [],
        [UserNoCompanyUsernameToRelink] = [],
        [UserNotFound] = ["userId"],
        [UserPasswordPolicyFailed] = ["message"],
        [UserSelectedRoleNotFound] = [],
        [UserStatusFilterInvalid] = [],
        [UserUsernameAlreadyTaken] = ["username"],
        [UserUsernameRequired] = [],
    };
}
