namespace MicroLIMS.Shared.Messages;

// EquipmentPlan.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string EquipmentPlanCalendarEventNotFound = "EquipmentPlan.CalendarEventNotFound";
    public const string EquipmentPlanCalendarEventWrongInstrument = "EquipmentPlan.CalendarEventWrongInstrument";
    public const string EquipmentPlanCalibrationDateFuture = "EquipmentPlan.CalibrationDateFuture";
    public const string EquipmentPlanCalibrationDateNotAfterPrevious = "EquipmentPlan.CalibrationDateNotAfterPrevious";
    public const string EquipmentPlanCertificateDueAfterCalibration = "EquipmentPlan.CertificateDueAfterCalibration";
    public const string EquipmentPlanCertificateNotCurrent = "EquipmentPlan.CertificateNotCurrent";
    public const string EquipmentPlanCertificateNotFound = "EquipmentPlan.CertificateNotFound";
    public const string EquipmentPlanCertificateWrongInstrument = "EquipmentPlan.CertificateWrongInstrument";
    public const string EquipmentPlanContentTypeMismatch = "EquipmentPlan.ContentTypeMismatch";
    public const string EquipmentPlanDocumentIntegrityFailed = "EquipmentPlan.DocumentIntegrityFailed";
    public const string EquipmentPlanDocumentNotCertificate = "EquipmentPlan.DocumentNotCertificate";
    public const string EquipmentPlanDocumentReadFailed = "EquipmentPlan.DocumentReadFailed";
    public const string EquipmentPlanDuplicateEvent = "EquipmentPlan.DuplicateEvent";
    public const string EquipmentPlanEntryAlreadyRecorded = "EquipmentPlan.EntryAlreadyRecorded";
    public const string EquipmentPlanEquipmentNotFound = "EquipmentPlan.EquipmentNotFound";
    public const string EquipmentPlanEventAlreadyCancelled = "EquipmentPlan.EventAlreadyCancelled";
    public const string EquipmentPlanEventCancellationReasonRequired = "EquipmentPlan.EventCancellationReasonRequired";
    public const string EquipmentPlanEventCancelledCannotAttachDocuments = "EquipmentPlan.EventCancelledCannotAttachDocuments";
    public const string EquipmentPlanEventCancelledCannotEdit = "EquipmentPlan.EventCancelledCannotEdit";
    public const string EquipmentPlanEventDateOrMonthRequired = "EquipmentPlan.EventDateOrMonthRequired";
    public const string EquipmentPlanEventDocumentNotFound = "EquipmentPlan.EventDocumentNotFound";
    public const string EquipmentPlanEventMonthFormat = "EquipmentPlan.EventMonthFormat";
    public const string EquipmentPlanEventTitleRequired = "EquipmentPlan.EventTitleRequired";
    public const string EquipmentPlanEventTitleTooLong = "EquipmentPlan.EventTitleTooLong";
    public const string EquipmentPlanFileContentFormatMismatch = "EquipmentPlan.FileContentFormatMismatch";
    public const string EquipmentPlanFileContentTooShort = "EquipmentPlan.FileContentTooShort";
    public const string EquipmentPlanFileEmpty = "EquipmentPlan.FileEmpty";
    public const string EquipmentPlanFileTooLarge = "EquipmentPlan.FileTooLarge";
    public const string EquipmentPlanFileTypeUnsupported = "EquipmentPlan.FileTypeUnsupported";
    public const string EquipmentPlanLogEntryAlreadyVoided = "EquipmentPlan.LogEntryAlreadyVoided";
    public const string EquipmentPlanLogEntryNotFound = "EquipmentPlan.LogEntryNotFound";
    public const string EquipmentPlanMaintenanceDateFuture = "EquipmentPlan.MaintenanceDateFuture";
    public const string EquipmentPlanPmEntryAlreadyRecorded = "EquipmentPlan.PmEntryAlreadyRecorded";
    public const string EquipmentPlanRangeEndBeforeStart = "EquipmentPlan.RangeEndBeforeStart";
    public const string EquipmentPlanVoidReasonRequired = "EquipmentPlan.VoidReasonRequired";
    public const string EquipmentPlanWebpInvalid = "EquipmentPlan.WebpInvalid";
    public const string EquipmentPlanWorkDoneRequired = "EquipmentPlan.WorkDoneRequired";

    private static Dictionary<string, string[]> EquipmentPlanParameters() => new()
    {
        [EquipmentPlanCalendarEventNotFound] = ["id"],
        [EquipmentPlanCalendarEventWrongInstrument] = [],
        [EquipmentPlanCalibrationDateFuture] = [],
        [EquipmentPlanCalibrationDateNotAfterPrevious] = ["date"],
        [EquipmentPlanCertificateDueAfterCalibration] = [],
        [EquipmentPlanCertificateNotCurrent] = [],
        [EquipmentPlanCertificateNotFound] = ["certificateId"],
        [EquipmentPlanCertificateWrongInstrument] = [],
        [EquipmentPlanContentTypeMismatch] = ["ext", "mime"],
        [EquipmentPlanDocumentIntegrityFailed] = [],
        [EquipmentPlanDocumentNotCertificate] = [],
        [EquipmentPlanDocumentReadFailed] = [],
        [EquipmentPlanDuplicateEvent] = [],
        [EquipmentPlanEntryAlreadyRecorded] = ["code", "date"],
        [EquipmentPlanEquipmentNotFound] = ["equipmentId"],
        [EquipmentPlanEventAlreadyCancelled] = [],
        [EquipmentPlanEventCancellationReasonRequired] = [],
        [EquipmentPlanEventCancelledCannotAttachDocuments] = [],
        [EquipmentPlanEventCancelledCannotEdit] = [],
        [EquipmentPlanEventDateOrMonthRequired] = [],
        [EquipmentPlanEventDocumentNotFound] = ["documentId"],
        [EquipmentPlanEventMonthFormat] = [],
        [EquipmentPlanEventTitleRequired] = [],
        [EquipmentPlanEventTitleTooLong] = [],
        [EquipmentPlanFileContentFormatMismatch] = ["ext"],
        [EquipmentPlanFileContentTooShort] = [],
        [EquipmentPlanFileEmpty] = [],
        [EquipmentPlanFileTooLarge] = ["maxMb"],
        [EquipmentPlanFileTypeUnsupported] = ["ext"],
        [EquipmentPlanLogEntryAlreadyVoided] = [],
        [EquipmentPlanLogEntryNotFound] = ["entryId"],
        [EquipmentPlanMaintenanceDateFuture] = [],
        [EquipmentPlanPmEntryAlreadyRecorded] = ["code", "date"],
        [EquipmentPlanRangeEndBeforeStart] = [],
        [EquipmentPlanVoidReasonRequired] = [],
        [EquipmentPlanWebpInvalid] = [],
        [EquipmentPlanWorkDoneRequired] = [],
    };
}
