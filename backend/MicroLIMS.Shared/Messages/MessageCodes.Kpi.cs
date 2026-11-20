namespace MicroLIMS.Shared.Messages;

// Kpi.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string KpiAnalystNotFoundInactive = "Kpi.AnalystNotFoundInactive";
    public const string KpiAvailabilityEntryNotFound = "Kpi.AvailabilityEntryNotFound";
    public const string KpiAvailabilityOverlap = "Kpi.AvailabilityOverlap";
    public const string KpiChangeReasonRequired = "Kpi.ChangeReasonRequired";
    public const string KpiDateRangeRequired = "Kpi.DateRangeRequired";
    public const string KpiDuplicateKey = "Kpi.DuplicateKey";
    public const string KpiEffectiveDateInPast = "Kpi.EffectiveDateInPast";
    public const string KpiEndDateBeforeStart = "Kpi.EndDateBeforeStart";
    public const string KpiEntryCannotMoveAnalyst = "Kpi.EntryCannotMoveAnalyst";
    public const string KpiFractionAvailableRange = "Kpi.FractionAvailableRange";
    public const string KpiHoursPerDayRange = "Kpi.HoursPerDayRange";
    public const string KpiLabSectionNotFound = "Kpi.LabSectionNotFound";
    public const string KpiLoadHorizonRange = "Kpi.LoadHorizonRange";
    public const string KpiReasonTooLong = "Kpi.ReasonTooLong";
    public const string KpiStartAfterEnd = "Kpi.StartAfterEnd";
    public const string KpiTestNotInTestMaster = "Kpi.TestNotInTestMaster";
    public const string KpiUnknownKpi = "Kpi.UnknownKpi";
    public const string KpiUnknownSampleCategory = "Kpi.UnknownSampleCategory";
    public const string KpiValueDecimalPlaces = "Kpi.ValueDecimalPlaces";
    public const string KpiValueMustBePositive = "Kpi.ValueMustBePositive";
    public const string KpiValueTooLarge = "Kpi.ValueTooLarge";
    public const string KpiVersionAlreadyEffective = "Kpi.VersionAlreadyEffective";
    public const string KpiWorkUnitsOrTargetRequired = "Kpi.WorkUnitsOrTargetRequired";
    public const string KpiWorkflowProfileVersionNotFound = "Kpi.WorkflowProfileVersionNotFound";
    public const string KpiWorkflowTypeRetired = "Kpi.WorkflowTypeRetired";
    public const string KpiWorkingWeekdayRequired = "Kpi.WorkingWeekdayRequired";

    private static Dictionary<string, string[]> KpiParameters() => new()
    {
        [KpiAnalystNotFoundInactive] = [],
        [KpiAvailabilityEntryNotFound] = [],
        [KpiAvailabilityOverlap] = [],
        [KpiChangeReasonRequired] = [],
        [KpiDateRangeRequired] = [],
        [KpiDuplicateKey] = ["key"],
        [KpiEffectiveDateInPast] = ["effectiveFrom", "today"],
        [KpiEndDateBeforeStart] = [],
        [KpiEntryCannotMoveAnalyst] = [],
        [KpiFractionAvailableRange] = [],
        [KpiHoursPerDayRange] = [],
        [KpiLabSectionNotFound] = [],
        [KpiLoadHorizonRange] = [],
        [KpiReasonTooLong] = [],
        [KpiStartAfterEnd] = [],
        [KpiTestNotInTestMaster] = ["code"],
        [KpiUnknownKpi] = ["key"],
        [KpiUnknownSampleCategory] = ["category"],
        [KpiValueDecimalPlaces] = ["label"],
        [KpiValueMustBePositive] = ["label"],
        [KpiValueTooLarge] = ["label", "max"],
        [KpiVersionAlreadyEffective] = [],
        [KpiWorkUnitsOrTargetRequired] = [],
        [KpiWorkflowProfileVersionNotFound] = ["id"],
        [KpiWorkflowTypeRetired] = ["type"],
        [KpiWorkingWeekdayRequired] = [],
    };
}
