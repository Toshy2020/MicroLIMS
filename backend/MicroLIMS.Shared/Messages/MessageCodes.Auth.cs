namespace MicroLIMS.Shared.Messages;

// Auth.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string AuthMfaAlreadyActiveForHardwareToken = "Auth.MfaAlreadyActiveForHardwareToken";
    public const string AuthMfaCannotAssignTokenToDisabledUser = "Auth.MfaCannotAssignTokenToDisabledUser";
    public const string AuthMfaCannotAssignTokenToOwnAccount = "Auth.MfaCannotAssignTokenToOwnAccount";
    public const string AuthMfaCannotResetDisabledUser = "Auth.MfaCannotResetDisabledUser";
    public const string AuthMfaCannotResetOwnAccount = "Auth.MfaCannotResetOwnAccount";
    public const string AuthMfaResetReasonRequired = "Auth.MfaResetReasonRequired";
    public const string AuthMfaTokenAssignReasonRequired = "Auth.MfaTokenAssignReasonRequired";
    public const string AuthMfaTokenSeedInvalid = "Auth.MfaTokenSeedInvalid";
    public const string AuthMfaTokenSerialRequired = "Auth.MfaTokenSerialRequired";
    public const string AuthMfaTotpStepInvalid = "Auth.MfaTotpStepInvalid";
    public const string AuthRoleDoesNotIncludeAction = "Auth.RoleDoesNotIncludeAction";
    public const string AuthTrustedDeviceNotFound = "Auth.TrustedDeviceNotFound";

    private static Dictionary<string, string[]> AuthParameters() => new()
    {
        [AuthMfaAlreadyActiveForHardwareToken] = [],
        [AuthMfaCannotAssignTokenToDisabledUser] = [],
        [AuthMfaCannotAssignTokenToOwnAccount] = [],
        [AuthMfaCannotResetDisabledUser] = [],
        [AuthMfaCannotResetOwnAccount] = [],
        [AuthMfaResetReasonRequired] = [],
        [AuthMfaTokenAssignReasonRequired] = [],
        [AuthMfaTokenSeedInvalid] = [],
        [AuthMfaTokenSerialRequired] = [],
        [AuthMfaTotpStepInvalid] = [],
        [AuthRoleDoesNotIncludeAction] = [],
        [AuthTrustedDeviceNotFound] = [],
    };
}
