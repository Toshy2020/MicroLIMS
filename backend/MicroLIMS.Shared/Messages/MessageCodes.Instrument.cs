namespace MicroLIMS.Shared.Messages;

// Instrument.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string InstrumentAcquisitionTimeFormat = "Instrument.AcquisitionTimeFormat";
    public const string InstrumentAcquisitionYearRange = "Instrument.AcquisitionYearRange";
    public const string InstrumentAgentNameRequired = "Instrument.AgentNameRequired";
    public const string InstrumentAgentNotFound = "Instrument.AgentNotFound";
    public const string InstrumentAgentRevokeReasonRequired = "Instrument.AgentRevokeReasonRequired";
    public const string InstrumentAgentRevoked = "Instrument.AgentRevoked";
    public const string InstrumentAgentTimeZoneUnknown = "Instrument.AgentTimeZoneUnknown";
    public const string InstrumentEquipmentAlreadyHasActiveAgent = "Instrument.EquipmentAlreadyHasActiveAgent";
    public const string InstrumentEquipmentCodeTooLongForAgent = "Instrument.EquipmentCodeTooLongForAgent";
    public const string InstrumentEquipmentNotHplc = "Instrument.EquipmentNotHplc";
    public const string InstrumentIngestAgentClockInvalid = "Instrument.IngestAgentClockInvalid";
    public const string InstrumentIngestFormUnreadable = "Instrument.IngestFormUnreadable";
    public const string InstrumentIngestInjectionNumberRequired = "Instrument.IngestInjectionNumberRequired";
    public const string InstrumentIngestMultipartRequired = "Instrument.IngestMultipartRequired";
    public const string InstrumentIngestReportFileRequired = "Instrument.IngestReportFileRequired";
    public const string InstrumentInvalidTestEmailAddress = "Instrument.InvalidTestEmailAddress";
    public const string InstrumentManifestTooLarge = "Instrument.ManifestTooLarge";
    public const string InstrumentNoCriticalAlertRecipients = "Instrument.NoCriticalAlertRecipients";
    public const string InstrumentOperatorNameTooLong = "Instrument.OperatorNameTooLong";
    public const string InstrumentReportFileEmpty = "Instrument.ReportFileEmpty";
    public const string InstrumentReportFileNameInvalid = "Instrument.ReportFileNameInvalid";
    public const string InstrumentReportFileTooLarge = "Instrument.ReportFileTooLarge";
    public const string InstrumentReportHashFormat = "Instrument.ReportHashFormat";
    public const string InstrumentReportHashMismatch = "Instrument.ReportHashMismatch";
    public const string InstrumentReportNotFoundForHash = "Instrument.ReportNotFoundForHash";
    public const string InstrumentSequenceNameInvalid = "Instrument.SequenceNameInvalid";
    public const string InstrumentStorageProbeReadMismatch = "Instrument.StorageProbeReadMismatch";
    public const string InstrumentUnknownTimeZone = "Instrument.UnknownTimeZone";

    private static Dictionary<string, string[]> InstrumentParameters() => new()
    {
        [InstrumentAcquisitionTimeFormat] = ["format"],
        [InstrumentAcquisitionYearRange] = ["minYear", "maxYear"],
        [InstrumentAgentNameRequired] = [],
        [InstrumentAgentNotFound] = ["agentId"],
        [InstrumentAgentRevokeReasonRequired] = [],
        [InstrumentAgentRevoked] = ["agentCode"],
        [InstrumentAgentTimeZoneUnknown] = ["timeZoneId"],
        [InstrumentEquipmentAlreadyHasActiveAgent] = ["agentCode", "equipmentCode"],
        [InstrumentEquipmentCodeTooLongForAgent] = ["equipmentCode", "length", "maxLength"],
        [InstrumentEquipmentNotHplc] = ["equipmentCode"],
        [InstrumentIngestAgentClockInvalid] = [],
        [InstrumentIngestFormUnreadable] = ["error"],
        [InstrumentIngestInjectionNumberRequired] = [],
        [InstrumentIngestMultipartRequired] = [],
        [InstrumentIngestReportFileRequired] = [],
        [InstrumentInvalidTestEmailAddress] = [],
        [InstrumentManifestTooLarge] = ["maxMb"],
        [InstrumentNoCriticalAlertRecipients] = ["settingKey"],
        [InstrumentOperatorNameTooLong] = [],
        [InstrumentReportFileEmpty] = [],
        [InstrumentReportFileNameInvalid] = [],
        [InstrumentReportFileTooLarge] = ["maxMb"],
        [InstrumentReportHashFormat] = [],
        [InstrumentReportHashMismatch] = [],
        [InstrumentReportNotFoundForHash] = [],
        [InstrumentSequenceNameInvalid] = [],
        [InstrumentStorageProbeReadMismatch] = [],
        [InstrumentUnknownTimeZone] = ["timeZoneId"],
    };
}
