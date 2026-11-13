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
    // Written by SessionTokenCleanupWorker: a count of dead tokens deleted in one pass.
    public const string SessionTokensPurged = "AUTH_SESSION_TOKENS_PURGED";

    // ---- Credentials ----
    public const string PasswordChanged = "AUTH_PASSWORD_CHANGED";
    public const string PasswordChangeFailed = "AUTH_PASSWORD_CHANGE_FAILED";
    public const string PasswordReset = "AUTH_PASSWORD_RESET";

    // ---- Multi-factor authentication ----
    public const string MfaEnrolmentStarted = "AUTH_MFA_ENROLMENT_STARTED";
    public const string MfaEnrolled = "AUTH_MFA_ENROLLED";
    public const string MfaTokenAssigned = "AUTH_MFA_TOKEN_ASSIGNED";
    public const string MfaVerified = "AUTH_MFA_VERIFIED";
    public const string MfaFailed = "AUTH_MFA_FAILED";
    public const string MfaChallengeIssued = "AUTH_MFA_CHALLENGE_ISSUED";
    public const string MfaRecoveryCodeUsed = "AUTH_MFA_RECOVERY_CODE_USED";
    public const string MfaRecoveryCodesRegenerated = "AUTH_MFA_RECOVERY_CODES_REGENERATED";
    public const string MfaRemovedByUser = "AUTH_MFA_REMOVED_BY_USER";
    public const string MfaResetByAdmin = "AUTH_MFA_RESET_BY_ADMIN";
    public const string TrustedDeviceAdded = "AUTH_TRUSTED_DEVICE_ADDED";
    public const string TrustedDeviceRevoked = "AUTH_TRUSTED_DEVICE_REVOKED";

    // ---- Account state ----
    public const string AccountDisabled = "AUTH_ACCOUNT_DISABLED";
    public const string AccountEnabled = "AUTH_ACCOUNT_ENABLED";
    public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AccountUnlocked = "AUTH_ACCOUNT_UNLOCKED";

    // ---- Authorization state ----
    public const string RoleChanged = "AUTH_ROLE_CHANGED";

    // ---- Administration ----
    // An administrator ran a housekeeping background job by hand; carries the stated reason.
    public const string BackgroundJobManuallyTriggered = "ADMIN_BACKGROUND_JOB_TRIGGERED";
    // Integrations tab: an administrator sent a test alert / test email or ran the storage probe.
    public const string CriticalAlertTestSent = "ADMIN_CRITICAL_ALERT_TEST_SENT";
    public const string SmtpTestEmailSent = "ADMIN_SMTP_TEST_SENT";
    public const string StorageProbeRun = "ADMIN_STORAGE_PROBE_RUN";

    // Maintenance mode switched on or off; carries the reason and the new state.
    public const string MaintenanceModeChanged = "ADMIN_MAINTENANCE_MODE_CHANGED";

    // ---- Instrument agents ----
    // A presented agent key was malformed, unknown, revoked, or its equipment is gone.
    public const string AgentAuthFailed = "AGENT_AUTH_FAILED";
}
