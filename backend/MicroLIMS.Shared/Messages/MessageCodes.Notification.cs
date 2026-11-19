namespace MicroLIMS.Shared.Messages;

// Notification.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string NotificationEscalationRuleDuplicate = "Notification.EscalationRuleDuplicate";
    public const string NotificationEscalationRuleNotFound = "Notification.EscalationRuleNotFound";
    public const string NotificationEscalationRuleRetiredCannotReplace = "Notification.EscalationRuleRetiredCannotReplace";
    public const string NotificationSectionNotFound = "Notification.SectionNotFound";
    public const string NotificationWorkingHoursRequired = "Notification.WorkingHoursRequired";

    private static Dictionary<string, string[]> NotificationParameters() => new()
    {
        [NotificationEscalationRuleDuplicate] = [],
        [NotificationEscalationRuleNotFound] = ["ruleId"],
        [NotificationEscalationRuleRetiredCannotReplace] = [],
        [NotificationSectionNotFound] = ["sectionId"],
        [NotificationWorkingHoursRequired] = [],
    };
}
