using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Spc;

// How the proposed alert/action limits are derived from the data.
public enum LimitMethod
{
    // Mean ± k·σ with σ estimated from the average moving range (MR̄/d2) -
    // the Minitab I-chart convention.
    MovingRange,
    // Mean ± k × sample standard deviation (n-1).
    StandardDeviation,
    // Alert/action are percentiles of the results (PERCENTILE.INC). No
    // lower limits are produced.
    Percentile
}

public enum LimitRounding { None, OneDecimal, TwoDecimals, UpToWholeNumber }

// What the process capability indices are measured against.
public enum CapabilityBasis { SpecLimit, ActionLimit, AlertLimit, Custom }

public record ControlChartSettings(
    LimitMethod Method = LimitMethod.MovingRange,
    double AlertK = 2,
    double ActionK = 3,
    double AlertPercentile = 95,
    double ActionPercentile = 99,
    int MovingRangeLength = 2,
    LimitRounding Rounding = LimitRounding.None,
    int MinimumPoints = 30,
    IReadOnlyCollection<int>? Rules = null)
{
    public static readonly IReadOnlyCollection<int> DefaultRules = new[] { 1, 2 };
    public IReadOnlyCollection<int> EnabledRules => Rules ?? DefaultRules;
}

public record CapabilitySpec(double? Lsl, double? Usl);

public record ProposedLimits(double? AlertUpper, double? ActionUpper, double? AlertLower, double? ActionLower);

public record CapabilityIndices(
    double? Lsl, double? Usl,
    double? Cp, double? Cpk, double? Cpu, double? Cpl,
    double? Pp, double? Ppk,
    double? ObservedPercentOutside, double? ExpectedPercentOutside);

public record HistogramBin(double From, double To, int Count, double? ExpectedCount);

public record BoxSummary(
    double Min, double Q1, double Median, double Q3, double Max,
    double LowerWhisker, double UpperWhisker, List<double> Outliers);

// Everything the calculator derives from one series of values.
public record ControlChartComputation(
    int Count,
    double? Mean,
    double? Median,
    double? Min,
    double? Max,
    double? StandardDeviation,
    double? SigmaWithin,
    double? SigmaUsed,
    double? MovingRangeMean,
    double? MovingRangeUcl,
    double? MovingRangeLcl,
    bool BelowMinimumPoints,
    ProposedLimits Limits,
    List<double?> MovingRanges,
    List<int[]> RuleViolations,
    CapabilityIndices Capability,
    List<HistogramBin> Histogram,
    BoxSummary? Box);

// ---- API read models -------------------------------------------------

public record ControlChartQuery(
    string TestCode,
    string SubjectName,
    SampleCategory? Category,
    DateTime? FromDate,
    DateTime? ToDate,
    DateTime? CalculationFrom,
    DateTime? CalculationTo,
    bool ApprovedOnly,
    ControlChartSettings Settings,
    CapabilityBasis CapabilityBasis = CapabilityBasis.SpecLimit,
    double? CustomLsl = null,
    double? CustomUsl = null);

public record ControlChartPoint(
    int RecordId,
    string ReferenceNumber,
    DateTime Date,
    double Value,
    string ReportedValue,
    bool IsImputed,
    double? MovingRange,
    ResultLevel ResultLevel,
    bool InCalculation,
    int[] Rules,
    double? AlertLimit,
    double? ActionLimit,
    double? SpecLimit);

public record MonthlyBox(string Month, int Count, double Mean, BoxSummary Box);

// A configuration row whose alert/action limits the analysis can update.
public record LimitTarget(
    string Kind,
    int ConfigurationId,
    uint Version,
    string Location,
    string? Detail,
    string TestCode,
    string? TestType,
    string AlertLimit,
    string ActionLimit,
    string SpecLimit,
    string Unit);

public record CurrentLimits(double? Alert, double? Action, double? SpecLower, double? SpecUpper);

public record ControlChartResult(
    string TestCode,
    string TestDisplayName,
    string SubjectName,
    string? Unit,
    ControlChartSettings Settings,
    CapabilityBasis CapabilityBasis,
    int ImputedPointCount,
    ControlChartComputation Statistics,
    CurrentLimits CurrentLimits,
    List<ControlChartPoint> Points,
    List<MonthlyBox> Monthly,
    List<LimitTarget> LimitTargets);

public record LimitReviewQuery(
    string TestCode,
    SampleCategory Category,
    DateTime? FromDate,
    DateTime? ToDate,
    bool ApprovedOnly,
    ControlChartSettings Settings);

public record LimitReviewRow(
    string SubjectName,
    int Count,
    bool BelowMinimumPoints,
    double? Mean,
    double? StandardDeviation,
    double? SigmaUsed,
    double? ProposedAlert,
    double? ProposedAction,
    double? CurrentAlert,
    double? CurrentAction,
    double? Cpk,
    int FlaggedPoints,
    List<LimitTarget> LimitTargets);

public record LimitReviewResult(string TestCode, string TestDisplayName, SampleCategory Category, string? Unit, ControlChartSettings Settings, List<LimitReviewRow> Rows);

// ---- Apply limits ----------------------------------------------------

public record ApplyLimitItem(string Kind, int ConfigurationId, uint Version, string SubjectName, decimal AlertLimit, decimal ActionLimit);

public record ApplyTrendLimitsRequest(
    string TestCode,
    SampleCategory Category,
    DateTime? FromDate,
    DateTime? ToDate,
    bool ApprovedOnly,
    ControlChartSettings Settings,
    List<ApplyLimitItem> Items,
    string Reason,
    string Password);

public record AppliedLimit(
    string Kind, int ConfigurationId, string Location,
    string PreviousAlert, string PreviousAction, string NewAlert, string NewAction,
    double? CalculatedAlert, double? CalculatedAction, bool Overridden, int SignatureId);
