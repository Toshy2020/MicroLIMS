namespace MicroLIMS.Shared.Constants;

// Event codes for the Security Audit Trail (SecurityAuditEvent).
//
// These name *events*, not database operations: the trail records that a
// session was revoked, never that a RefreshToken row was updated. Codes
// are stable strings because they are read by humans reviewing security
// evidence and are stored as text, so adding one needs no migration.
public static class SecurityEventCodes
{
    // ---- Sign-in ----
    public const string LoginSuccess = "AUTH_LOGIN_SUCCESS";
    public const string LoginFailedBadCredentials = "AUTH_LOGIN_FAILED_BAD_CREDENTIALS";
    public const string LoginFailedUnknownUser = "AUTH_LOGIN_FAILED_UNKNOWN_USER";
    public const string LoginFailedAccountLocked = "AUTH_LOGIN_FAILED_ACCOUNT_LOCKED";
    public const string LoginFailedAccountDisabled = "AUTH_LOGIN_FAILED_ACCOUNT_DISABLED";

    // ---- Session lifecycle ----
    public const string RefreshTokenIssued = "AUTH_REFRESH_TOKEN_ISSUED";
    public const string RefreshTokenRotated = "AUTH_REFRESH_TOKEN_ROTATED";
    public const string RefreshRejectedInvalidToken = "AUTH_REFRESH_REJECTED_INVALID_TOKEN";
    public const string RefreshRejectedAccountBlocked = "AUTH_REFRESH_REJECTED_ACCOUNT_BLOCKED";
    public const string Logout = "AUTH_LOGOUT";
    public const string SessionRevoked = "AUTH_SESSION_REVOKED";

    // ---- Credentials ----
    public const string PasswordChanged = "AUTH_PASSWORD_CHANGED";
    public const string PasswordChangeFailed = "AUTH_PASSWORD_CHANGE_FAILED";
    public const string PasswordReset = "AUTH_PASSWORD_RESET";

    // ---- Account state ----
    public const string AccountDisabled = "AUTH_ACCOUNT_DISABLED";
    public const string AccountEnabled = "AUTH_ACCOUNT_ENABLED";
    public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AccountUnlocked = "AUTH_ACCOUNT_UNLOCKED";

    // ---- Authorization state ----
    public const string RoleChanged = "AUTH_ROLE_CHANGED";
}
