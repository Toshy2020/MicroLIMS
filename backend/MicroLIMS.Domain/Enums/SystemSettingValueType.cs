namespace MicroLIMS.Domain.Enums;

// How a registry setting's text value is read. Persisted as int.
public enum SystemSettingValueType
{
    Integer = 0,
    Boolean = 1,
    String = 2,
    // A list of email addresses, stored as a comma-separated string.
    EmailList = 3
}
