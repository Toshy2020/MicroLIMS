namespace MicroLIMS.Shared.Messages;

// ControlChart.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ControlChartActionAboveSpecification = "ControlChart.ActionAboveSpecification";
    public const string ControlChartAlertAboveAction = "ControlChart.AlertAboveAction";
    public const string ControlChartCapabilityLimitsOrder = "ControlChart.CapabilityLimitsOrder";
    public const string ControlChartConfigurationLocationMismatch = "ControlChart.ConfigurationLocationMismatch";
    public const string ControlChartDuplicateConfiguration = "ControlChart.DuplicateConfiguration";
    public const string ControlChartInvalidConfigurationKind = "ControlChart.InvalidConfigurationKind";
    public const string ControlChartLimitsCategoryNotAllowed = "ControlChart.LimitsCategoryNotAllowed";
    public const string ControlChartLocationRequired = "ControlChart.LocationRequired";
    public const string ControlChartNegativeLimits = "ControlChart.NegativeLimits";
    public const string ControlChartNonNumericTest = "ControlChart.NonNumericTest";
    public const string ControlChartReasonRequired = "ControlChart.ReasonRequired";
    public const string ControlChartRoomConfigurationInactive = "ControlChart.RoomConfigurationInactive";
    public const string ControlChartRoomConfigurationNotFound = "ControlChart.RoomConfigurationNotFound";
    public const string ControlChartRoomConfigurationTestMismatch = "ControlChart.RoomConfigurationTestMismatch";
    public const string ControlChartSamplingConfigurationNotFound = "ControlChart.SamplingConfigurationNotFound";
    public const string ControlChartSamplingConfigurationTestMismatch = "ControlChart.SamplingConfigurationTestMismatch";
    public const string ControlChartSubjectNameRequired = "ControlChart.SubjectNameRequired";
    public const string ControlChartTestCodeRequired = "ControlChart.TestCodeRequired";
    public const string ControlChartTestNotConfigured = "ControlChart.TestNotConfigured";
    public const string ControlChartUnsupportedConfiguration = "ControlChart.UnsupportedConfiguration";

    private static Dictionary<string, string[]> ControlChartParameters() => new()
    {
        [ControlChartActionAboveSpecification] = ["actionLimit", "location", "specLimit"],
        [ControlChartAlertAboveAction] = ["subjectName"],
        [ControlChartCapabilityLimitsOrder] = [],
        [ControlChartConfigurationLocationMismatch] = ["configurationId", "location", "subjectName"],
        [ControlChartDuplicateConfiguration] = [],
        [ControlChartInvalidConfigurationKind] = ["category", "kind"],
        [ControlChartLimitsCategoryNotAllowed] = [],
        [ControlChartLocationRequired] = [],
        [ControlChartNegativeLimits] = ["subjectName"],
        [ControlChartNonNumericTest] = ["testCode"],
        [ControlChartReasonRequired] = [],
        [ControlChartRoomConfigurationInactive] = ["configurationId"],
        [ControlChartRoomConfigurationNotFound] = ["configurationId"],
        [ControlChartRoomConfigurationTestMismatch] = ["configurationId", "configuredTestCode", "requestedTestCode"],
        [ControlChartSamplingConfigurationNotFound] = ["configurationId"],
        [ControlChartSamplingConfigurationTestMismatch] = ["configurationId", "configuredTestCode", "requestedTestCode"],
        [ControlChartSubjectNameRequired] = [],
        [ControlChartTestCodeRequired] = [],
        [ControlChartTestNotConfigured] = ["testCode"],
        [ControlChartUnsupportedConfiguration] = [],
    };
}
