using System.Globalization;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.SystemSettings;

// Keys of the settings registry. A key is a stable identifier: renaming one
// orphans its stored value and history.
public static class SystemSettingKeys
{
    public const string PasswordMinimumLength = "security.password.minimumLength";
    public const string PasswordHistoryLimit = "security.password.historyLimit";
    public const string LockoutMaxFailedAttempts = "security.lockout.maxFailedAttempts";
    public const string LockoutDurationMinutes = "security.lockout.durationMinutes";
    public const string RefreshTokenLifetimeDays = "security.refreshToken.lifetimeDays";
    public const string PasswordResetLifetimeMinutes = "security.passwordReset.lifetimeMinutes";
    public const string SignatureMaxFailedAttempts = "security.signature.maxFailedAttempts";
    public const string SignatureWindowMinutes = "security.signature.windowMinutes";

    public const string ErrorLogLowSeverityDays = "retention.errorLog.lowSeverityDays";
    public const string ErrorLogHighSeverityDays = "retention.errorLog.highSeverityDays";
    public const string ErrorLogIntervalHours = "retention.errorLog.intervalHours";
    public const string SessionTokenRetentionDays = "retention.sessionTokens.retentionDays";
    public const string SessionTokenIntervalHours = "retention.sessionTokens.intervalHours";

    public const string SlowQueryThresholdMs = "monitoring.database.slowQueryThresholdMs";
    public const string BlockedQueryThresholdMs = "monitoring.database.blockedQueryThresholdMs";

    public const string ReassignmentEscalationDays = "DocumentControl.ReassignmentEscalationDays";

    public const string AccessReviewDormantDays = "AccessReview.DormantDays";
}

// One setting as defined in code: type, default, bounds, GxP relevance.
// Value precedence at runtime: database row, then the legacy configuration key
// (so an existing deployment keeps its appsettings value until it is changed
// here), then Default. Secrets are never definitions.
public sealed record SystemSettingDefinition(
    string Key,
    string Category,
    SystemSettingValueType Type,
    string Default,
    string Description,
    string? Unit = null,
    long? Min = null,
    long? Max = null,
    IReadOnlyList<string>? AllowedValues = null,
    bool IsGxpRelevant = false,
    string? LegacyConfigKey = null)
{
    // Checks the raw text against type, range and allowed values and returns the
    // canonical text to store. Throws InvalidOperationException (answered as 400).
    public string Normalize(string? raw)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException($"A value is required for '{Key}'.");

        switch (Type)
        {
            case SystemSettingValueType.Integer:
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    throw new InvalidOperationException($"'{Key}' must be a whole number.");
                if (Min is { } min && number < min)
                    throw new InvalidOperationException($"'{Key}' must be at least {min}{UnitSuffix()}.");
                if (Max is { } max && number > max)
                    throw new InvalidOperationException($"'{Key}' must be at most {max}{UnitSuffix()}.");
                return number.ToString(CultureInfo.InvariantCulture);

            case SystemSettingValueType.Boolean:
                if (bool.TryParse(text, out var flag))
                    return flag ? "true" : "false";
                throw new InvalidOperationException($"'{Key}' must be true or false.");

            default:
                if (AllowedValues is { Count: > 0 } && !AllowedValues.Contains(text, StringComparer.Ordinal))
                    throw new InvalidOperationException($"'{Key}' must be one of: {string.Join(", ", AllowedValues)}.");
                if (text.Length > 500)
                    throw new InvalidOperationException($"'{Key}' must be 500 characters or fewer.");
                return text;
        }
    }

    // Like Normalize but never throws: a stored or configured value that no
    // longer validates is ignored in favour of the next source.
    public bool TryNormalize(string? raw, out string normalized)
    {
        try
        {
            normalized = Normalize(raw);
            return true;
        }
        catch (InvalidOperationException)
        {
            normalized = string.Empty;
            return false;
        }
    }

    private string UnitSuffix() => Unit is null ? string.Empty : $" {Unit}";
}

public static class SystemSettingDefinitions
{
    public const string SecurityCategory = "Security";
    public const string RetentionCategory = "Retention";
    public const string MonitoringCategory = "Monitoring";
    public const string DocumentControlCategory = "Document Control";
    public const string AccessReviewCategory = "Access Review";

    // The defaults below are the values that were hard-coded or configured
    // before the registry existed; AuthenticationService and
    // ElectronicSignatureService keep exposing them as constants for the same reason.
    public static readonly IReadOnlyList<SystemSettingDefinition> All = new SystemSettingDefinition[]
    {
        new(SystemSettingKeys.PasswordMinimumLength, SecurityCategory, SystemSettingValueType.Integer, "8",
            "Minimum password length. Applies to passwords set or changed from now on; existing passwords are not re-checked.",
            "characters", 8, 64, IsGxpRelevant: true),
        new(SystemSettingKeys.PasswordHistoryLimit, SecurityCategory, SystemSettingValueType.Integer, "5",
            "How many of a user's last passwords cannot be reused.",
            "passwords", 1, 24, IsGxpRelevant: true),
        new(SystemSettingKeys.LockoutMaxFailedAttempts, SecurityCategory, SystemSettingValueType.Integer, "5",
            "Failed sign-ins before the account is locked.",
            "attempts", 3, 20, IsGxpRelevant: true),
        new(SystemSettingKeys.LockoutDurationMinutes, SecurityCategory, SystemSettingValueType.Integer, "15",
            "How long a locked account stays locked.",
            "minutes", 1, 1440, IsGxpRelevant: true),
        new(SystemSettingKeys.RefreshTokenLifetimeDays, SecurityCategory, SystemSettingValueType.Integer, "7",
            "Refresh token lifetime, i.e. how long a session can stay signed in without a password. Applies to sessions started or refreshed from now on.",
            "days", 1, 30, IsGxpRelevant: true),
        new(SystemSettingKeys.PasswordResetLifetimeMinutes, SecurityCategory, SystemSettingValueType.Integer, "60",
            "How long a password reset link stays valid.",
            "minutes", 5, 1440, IsGxpRelevant: true),
        new(SystemSettingKeys.SignatureMaxFailedAttempts, SecurityCategory, SystemSettingValueType.Integer, "5",
            "Failed electronic signatures within the window before further attempts are refused (the login account is not locked).",
            "attempts", 3, 20, IsGxpRelevant: true),
        new(SystemSettingKeys.SignatureWindowMinutes, SecurityCategory, SystemSettingValueType.Integer, "15",
            "Window in which failed electronic signatures are counted; also how long a throttled signer must wait.",
            "minutes", 1, 1440, IsGxpRelevant: true),

        new(SystemSettingKeys.ErrorLogLowSeverityDays, RetentionCategory, SystemSettingValueType.Integer, "180",
            "How long Info and Warning incidents are kept in the technical error log before they are pruned.",
            "days", 30, 3650, LegacyConfigKey: "ErrorMonitoring:Retention:LowSeverityRetentionDays"),
        new(SystemSettingKeys.ErrorLogHighSeverityDays, RetentionCategory, SystemSettingValueType.Integer, "365",
            "How long Error and Critical incidents are kept in the technical error log before they are pruned.",
            "days", 30, 3650, LegacyConfigKey: "ErrorMonitoring:Retention:HighSeverityRetentionDays"),
        new(SystemSettingKeys.ErrorLogIntervalHours, RetentionCategory, SystemSettingValueType.Integer, "24",
            "How often the error log is pruned. Takes effect after the next pruning run.",
            "hours", 1, 168, LegacyConfigKey: "ErrorMonitoring:Retention:IntervalHours"),
        new(SystemSettingKeys.SessionTokenRetentionDays, RetentionCategory, SystemSettingValueType.Integer, "30",
            "How long expired or revoked session and password-reset tokens are kept before they are deleted.",
            "days", 1, 365, LegacyConfigKey: "SessionTokenCleanup:RetentionDays"),
        new(SystemSettingKeys.SessionTokenIntervalHours, RetentionCategory, SystemSettingValueType.Integer, "24",
            "How often dead session tokens are cleaned up. Takes effect after the next cleanup run.",
            "hours", 1, 168, LegacyConfigKey: "SessionTokenCleanup:IntervalHours"),

        new(SystemSettingKeys.SlowQueryThresholdMs, MonitoringCategory, SystemSettingValueType.Integer, "1000",
            "A database query running longer than this is reported as slow.",
            "ms", 100, 60000, LegacyConfigKey: "ErrorMonitoring:DatabaseHealth:SlowQueryThresholdMs"),
        new(SystemSettingKeys.BlockedQueryThresholdMs, MonitoringCategory, SystemSettingValueType.Integer, "5000",
            "A database query waiting on a lock longer than this is reported as blocked.",
            "ms", 500, 600000, LegacyConfigKey: "ErrorMonitoring:DatabaseHealth:BlockedQueryThresholdMs"),

        new(SystemSettingKeys.ReassignmentEscalationDays, DocumentControlCategory, SystemSettingValueType.Integer, "7",
            "Days a deactivated user's document responsibilities may wait for reassignment before they are flagged overdue and the department's Section Heads are notified. Nothing is reassigned automatically.",
            "days", 1, 90),

        new(SystemSettingKeys.AccessReviewDormantDays, AccessReviewCategory, SystemSettingValueType.Integer, "90",
            "An active account whose last successful sign-in is older than this (or that has never signed in and is older than this) is flagged dormant in the access-review report. A review already generated keeps the value it was generated with.",
            "days", 1, 3650),
    };

    private static readonly Dictionary<string, SystemSettingDefinition> ByKey =
        All.ToDictionary(d => d.Key, StringComparer.Ordinal);

    public static SystemSettingDefinition? Find(string key) => ByKey.GetValueOrDefault(key);

    public static SystemSettingDefinition Get(string key) =>
        Find(key) ?? throw new ArgumentException($"Unknown system setting '{key}'.", nameof(key));

    public static int DefaultInt(string key) =>
        int.Parse(Get(key).Default, CultureInfo.InvariantCulture);
}
