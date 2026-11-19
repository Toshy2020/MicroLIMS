namespace MicroLIMS.Shared.Messages;

// ControlChart.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ControlChartActionAboveSpecification = "ControlChart.ActionAboveSpecification";
    public const string ControlChartAlertAboveAction = "ControlChart.AlertAboveAction";
    public const string ControlChartAlertNotAboveActionMultiplier = "ControlChart.AlertNotAboveActionMultiplier";
    public const string ControlChartAlertNotAboveActionPercentile = "ControlChart.AlertNotAboveActionPercentile";
    public const string ControlChartCapabilityLimitsOrder = "ControlChart.CapabilityLimitsOrder";
    public const string ControlChartChartImageMissing = "ControlChart.ChartImageMissing";
    public const string ControlChartChartImageNotBase64 = "ControlChart.ChartImageNotBase64";
    public const string ControlChartChartImageNotPng = "ControlChart.ChartImageNotPng";
    public const string ControlChartChartImagePixelLimit = "ControlChart.ChartImagePixelLimit";
    public const string ControlChartChartImageTooLarge = "ControlChart.ChartImageTooLarge";
    public const string ControlChartConfigurationLocationMismatch = "ControlChart.ConfigurationLocationMismatch";
    public const string ControlChartDuplicateConfiguration = "ControlChart.DuplicateConfiguration";
    public const string ControlChartInvalidConfigurationKind = "ControlChart.InvalidConfigurationKind";
    public const string ControlChartLimitsCategoryNotAllowed = "ControlChart.LimitsCategoryNotAllowed";
    public const string ControlChartLocationRequired = "ControlChart.LocationRequired";
    public const string ControlChartMinDataPointsNonNegative = "ControlChart.MinDataPointsNonNegative";
    public const string ControlChartMovingRangeLengthRange = "ControlChart.MovingRangeLengthRange";
    public const string ControlChartMultipliersPositive = "ControlChart.MultipliersPositive";
    public const string ControlChartNegativeLimits = "ControlChart.NegativeLimits";
    public const string ControlChartNoValues = "ControlChart.NoValues";
    public const string ControlChartNonNumericTest = "ControlChart.NonNumericTest";
    public const string ControlChartPercentileRange = "ControlChart.PercentileRange";
    public const string ControlChartReasonRequired = "ControlChart.ReasonRequired";
    public const string ControlChartReportNeedsChart = "ControlChart.ReportNeedsChart";
    public const string ControlChartReportTitleRequired = "ControlChart.ReportTitleRequired";
    public const string ControlChartReportTitleTooLong = "ControlChart.ReportTitleTooLong";
    public const string ControlChartReportTooManyCharts = "ControlChart.ReportTooManyCharts";
    public const string ControlChartRoomConfigurationInactive = "ControlChart.RoomConfigurationInactive";
    public const string ControlChartRoomConfigurationNotFound = "ControlChart.RoomConfigurationNotFound";
    public const string ControlChartRoomConfigurationTestMismatch = "ControlChart.RoomConfigurationTestMismatch";
    public const string ControlChartRuleNumberRange = "ControlChart.RuleNumberRange";
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
        [ControlChartAlertNotAboveActionMultiplier] = [],
        [ControlChartAlertNotAboveActionPercentile] = [],
        [ControlChartCapabilityLimitsOrder] = [],
        [ControlChartChartImageMissing] = ["index"],
        [ControlChartChartImageNotBase64] = ["index"],
        [ControlChartChartImageNotPng] = ["index"],
        [ControlChartChartImagePixelLimit] = ["index", "maxPixels"],
        [ControlChartChartImageTooLarge] = ["index", "maxMb"],
        [ControlChartConfigurationLocationMismatch] = ["configurationId", "location", "subjectName"],
        [ControlChartDuplicateConfiguration] = [],
        [ControlChartInvalidConfigurationKind] = ["category", "kind"],
        [ControlChartLimitsCategoryNotAllowed] = [],
        [ControlChartLocationRequired] = [],
        [ControlChartMinDataPointsNonNegative] = [],
        [ControlChartMovingRangeLengthRange] = [],
        [ControlChartMultipliersPositive] = [],
        [ControlChartNegativeLimits] = ["subjectName"],
        [ControlChartNoValues] = [],
        [ControlChartNonNumericTest] = ["testCode"],
        [ControlChartPercentileRange] = [],
        [ControlChartReasonRequired] = [],
        [ControlChartReportNeedsChart] = [],
        [ControlChartReportTitleRequired] = [],
        [ControlChartReportTitleTooLong] = [],
        [ControlChartReportTooManyCharts] = ["maxSections"],
        [ControlChartRoomConfigurationInactive] = ["configurationId"],
        [ControlChartRoomConfigurationNotFound] = ["configurationId"],
        [ControlChartRoomConfigurationTestMismatch] = ["configurationId", "configuredTestCode", "requestedTestCode"],
        [ControlChartRuleNumberRange] = [],
        [ControlChartSamplingConfigurationNotFound] = ["configurationId"],
        [ControlChartSamplingConfigurationTestMismatch] = ["configurationId", "configuredTestCode", "requestedTestCode"],
        [ControlChartSubjectNameRequired] = [],
        [ControlChartTestCodeRequired] = [],
        [ControlChartTestNotConfigured] = ["testCode"],
        [ControlChartUnsupportedConfiguration] = [],
    };
}
