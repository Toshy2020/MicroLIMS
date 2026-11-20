namespace MicroLIMS.Shared.Messages;

// Settings.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string SettingsSsoConnectionTestRequired = "Settings.SsoConnectionTestRequired";
    public const string SettingsSsoEmergencyAdminRequired = "Settings.SsoEmergencyAdminRequired";
    public const string SettingsSsoActingAdminNotLinked = "Settings.SsoActingAdminNotLinked";
    public const string SettingsAlreadyHasValue = "Settings.AlreadyHasValue";
    public const string SettingsBackgroundJobNoImplementation = "Settings.BackgroundJobNoImplementation";
    public const string SettingsBackgroundJobNotFound = "Settings.BackgroundJobNotFound";
    public const string SettingsEmailListInvalidAddress = "Settings.EmailListInvalidAddress";
    public const string SettingsEmailListTooManyAddresses = "Settings.EmailListTooManyAddresses";
    public const string SettingsLabTimeZoneNotFound = "Settings.LabTimeZoneNotFound";
    public const string SettingsMfaRequiresTwoAdmins = "Settings.MfaRequiresTwoAdmins";
    public const string SettingsMustBeAtLeast = "Settings.MustBeAtLeast";
    public const string SettingsMustBeAtMost = "Settings.MustBeAtMost";
    public const string SettingsMustBeBoolean = "Settings.MustBeBoolean";
    public const string SettingsMustBeOneOf = "Settings.MustBeOneOf";
    public const string SettingsMustBeWholeNumber = "Settings.MustBeWholeNumber";
    public const string SettingsNotFound = "Settings.NotFound";
    public const string SettingsTooLong = "Settings.TooLong";
    public const string SettingsUnsupportedSsoMode = "Settings.UnsupportedSsoMode";
    public const string SettingsValueRequired = "Settings.ValueRequired";

    private static Dictionary<string, string[]> SettingsParameters() => new()
    {
        [SettingsSsoConnectionTestRequired] = [],
        [SettingsSsoEmergencyAdminRequired] = [],
        [SettingsSsoActingAdminNotLinked] = [],
        [SettingsAlreadyHasValue] = ["key", "value"],
        [SettingsBackgroundJobNoImplementation] = ["jobName"],
        [SettingsBackgroundJobNotFound] = [],
        [SettingsEmailListInvalidAddress] = ["key", "addresses"],
        [SettingsEmailListTooManyAddresses] = ["key", "maxAddresses"],
        [SettingsLabTimeZoneNotFound] = ["timeZoneId"],
        [SettingsMfaRequiresTwoAdmins] = [],
        [SettingsMustBeAtLeast] = ["key", "min", "unitSuffix"],
        [SettingsMustBeAtMost] = ["key", "max", "unitSuffix"],
        [SettingsMustBeBoolean] = ["key"],
        [SettingsMustBeOneOf] = ["allowedValues", "key"],
        [SettingsMustBeWholeNumber] = ["key"],
        [SettingsNotFound] = ["key"],
        [SettingsTooLong] = ["key"],
        [SettingsUnsupportedSsoMode] = ["mode"],
        [SettingsValueRequired] = ["key"],
    };
}
