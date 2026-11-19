namespace MicroLIMS.Shared.Messages;

// Instrument.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string InstrumentAgentNameRequired = "Instrument.AgentNameRequired";
    public const string InstrumentAgentNotFound = "Instrument.AgentNotFound";
    public const string InstrumentAgentRevokeReasonRequired = "Instrument.AgentRevokeReasonRequired";
    public const string InstrumentAgentRevoked = "Instrument.AgentRevoked";
    public const string InstrumentEquipmentAlreadyHasActiveAgent = "Instrument.EquipmentAlreadyHasActiveAgent";
    public const string InstrumentEquipmentCodeTooLongForAgent = "Instrument.EquipmentCodeTooLongForAgent";
    public const string InstrumentEquipmentNotHplc = "Instrument.EquipmentNotHplc";
    public const string InstrumentIngestAgentClockInvalid = "Instrument.IngestAgentClockInvalid";
    public const string InstrumentIngestFormUnreadable = "Instrument.IngestFormUnreadable";
    public const string InstrumentIngestInjectionNumberRequired = "Instrument.IngestInjectionNumberRequired";
    public const string InstrumentIngestMultipartRequired = "Instrument.IngestMultipartRequired";
    public const string InstrumentIngestReportFileRequired = "Instrument.IngestReportFileRequired";
    public const string InstrumentInvalidTestEmailAddress = "Instrument.InvalidTestEmailAddress";
    public const string InstrumentNoCriticalAlertRecipients = "Instrument.NoCriticalAlertRecipients";
    public const string InstrumentStorageProbeReadMismatch = "Instrument.StorageProbeReadMismatch";
    public const string InstrumentUnknownTimeZone = "Instrument.UnknownTimeZone";

    private static Dictionary<string, string[]> InstrumentParameters() => new()
    {
        [InstrumentAgentNameRequired] = [],
        [InstrumentAgentNotFound] = ["agentId"],
        [InstrumentAgentRevokeReasonRequired] = [],
        [InstrumentAgentRevoked] = ["agentCode"],
        [InstrumentEquipmentAlreadyHasActiveAgent] = ["agentCode", "equipmentCode"],
        [InstrumentEquipmentCodeTooLongForAgent] = ["equipmentCode", "length", "maxLength"],
        [InstrumentEquipmentNotHplc] = ["equipmentCode"],
        [InstrumentIngestAgentClockInvalid] = [],
        [InstrumentIngestFormUnreadable] = ["error"],
        [InstrumentIngestInjectionNumberRequired] = [],
        [InstrumentIngestMultipartRequired] = [],
        [InstrumentIngestReportFileRequired] = [],
        [InstrumentInvalidTestEmailAddress] = [],
        [InstrumentNoCriticalAlertRecipients] = ["settingKey"],
        [InstrumentStorageProbeReadMismatch] = [],
        [InstrumentUnknownTimeZone] = ["timeZoneId"],
    };
}
