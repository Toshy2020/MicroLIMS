namespace MicroLIMS.Shared.Messages;

// Instrument.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string InstrumentAcquisitionTimeFormat = "Instrument.AcquisitionTimeFormat";
    public const string InstrumentAcquisitionYearRange = "Instrument.AcquisitionYearRange";
    public const string InstrumentAgentTimeZoneUnknown = "Instrument.AgentTimeZoneUnknown";
    public const string InstrumentManifestTooLarge = "Instrument.ManifestTooLarge";
    public const string InstrumentOperatorNameTooLong = "Instrument.OperatorNameTooLong";
    public const string InstrumentReportFileEmpty = "Instrument.ReportFileEmpty";
    public const string InstrumentReportFileNameInvalid = "Instrument.ReportFileNameInvalid";
    public const string InstrumentReportFileTooLarge = "Instrument.ReportFileTooLarge";
    public const string InstrumentReportHashFormat = "Instrument.ReportHashFormat";
    public const string InstrumentReportHashMismatch = "Instrument.ReportHashMismatch";
    public const string InstrumentReportNotFoundForHash = "Instrument.ReportNotFoundForHash";
    public const string InstrumentSequenceNameInvalid = "Instrument.SequenceNameInvalid";

    private static Dictionary<string, string[]> InstrumentParameters() => new()
    {
        [InstrumentAcquisitionTimeFormat] = ["format"],
        [InstrumentAcquisitionYearRange] = ["minYear", "maxYear"],
        [InstrumentAgentTimeZoneUnknown] = ["timeZoneId"],
        [InstrumentManifestTooLarge] = ["maxMb"],
        [InstrumentOperatorNameTooLong] = [],
        [InstrumentReportFileEmpty] = [],
        [InstrumentReportFileNameInvalid] = [],
        [InstrumentReportFileTooLarge] = ["maxMb"],
        [InstrumentReportHashFormat] = [],
        [InstrumentReportHashMismatch] = [],
        [InstrumentReportNotFoundForHash] = [],
        [InstrumentSequenceNameInvalid] = [],
    };
}
