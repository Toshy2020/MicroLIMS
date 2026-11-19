namespace MicroLIMS.Shared.Messages;

// User.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
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
