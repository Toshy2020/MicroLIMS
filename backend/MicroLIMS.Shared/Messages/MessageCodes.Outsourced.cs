namespace MicroLIMS.Shared.Messages;

// Outsourced.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string OutsourcedAlreadyOutsourced = "Outsourced.AlreadyOutsourced";
    public const string OutsourcedConformityRequired = "Outsourced.ConformityRequired";
    public const string OutsourcedHasInHouseResult = "Outsourced.HasInHouseResult";
    public const string OutsourcedLabNameTooLong = "Outsourced.LabNameTooLong";
    public const string OutsourcedLabRequired = "Outsourced.LabRequired";
    public const string OutsourcedMoistureMustBePercentage = "Outsourced.MoistureMustBePercentage";
    public const string OutsourcedNumericValueRequired = "Outsourced.NumericValueRequired";
    public const string OutsourcedOnlyPendingOrInProgress = "Outsourced.OnlyPendingOrInProgress";
    public const string OutsourcedOnlyPhysicochemical = "Outsourced.OnlyPhysicochemical";
    public const string OutsourcedReasonRequired = "Outsourced.ReasonRequired";
    public const string OutsourcedReportNotExternalForTest = "Outsourced.ReportNotExternalForTest";
    public const string OutsourcedReportNotFound = "Outsourced.ReportNotFound";
    public const string OutsourcedReportNumberRequired = "Outsourced.ReportNumberRequired";
    public const string OutsourcedReportNumberTooLong = "Outsourced.ReportNumberTooLong";
    public const string OutsourcedReportVoided = "Outsourced.ReportVoided";
    public const string OutsourcedSpecNotReportable = "Outsourced.SpecNotReportable";
    public const string OutsourcedSpecNotReportableCannotOutsource = "Outsourced.SpecNotReportableCannotOutsource";

    private static Dictionary<string, string[]> OutsourcedParameters() => new()
    {
        [OutsourcedAlreadyOutsourced] = ["lab"],
        [OutsourcedConformityRequired] = ["parameter"],
        [OutsourcedHasInHouseResult] = [],
        [OutsourcedLabNameTooLong] = [],
        [OutsourcedLabRequired] = [],
        [OutsourcedMoistureMustBePercentage] = [],
        [OutsourcedNumericValueRequired] = ["parameter"],
        [OutsourcedOnlyPendingOrInProgress] = [],
        [OutsourcedOnlyPhysicochemical] = [],
        [OutsourcedReasonRequired] = [],
        [OutsourcedReportNotExternalForTest] = [],
        [OutsourcedReportNotFound] = [],
        [OutsourcedReportNumberRequired] = [],
        [OutsourcedReportNumberTooLong] = [],
        [OutsourcedReportVoided] = [],
        [OutsourcedSpecNotReportable] = ["limitType", "parameter"],
        [OutsourcedSpecNotReportableCannotOutsource] = ["limitType", "parameter"],
    };
}
