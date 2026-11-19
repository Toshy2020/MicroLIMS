namespace MicroLIMS.Shared.Messages;

// Label.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string LabelCannotPrintBlocked = "Label.CannotPrintBlocked";
    public const string LabelCannotPrintNoCode = "Label.CannotPrintNoCode";
    public const string LabelDefaultCopiesOutOfRange = "Label.DefaultCopiesOutOfRange";
    public const string LabelFormatForTypeNotFound = "Label.FormatForTypeNotFound";
    public const string LabelFormatNotFound = "Label.FormatNotFound";
    public const string LabelHeightOutOfRange = "Label.HeightOutOfRange";
    public const string LabelItemCopiesOutOfRange = "Label.ItemCopiesOutOfRange";
    public const string LabelItemDuplicate = "Label.ItemDuplicate";
    public const string LabelItemEntityIdInvalid = "Label.ItemEntityIdInvalid";
    public const string LabelItemEntityTypeInvalid = "Label.ItemEntityTypeInvalid";
    public const string LabelLayoutInvalid = "Label.LayoutInvalid";
    public const string LabelPrintRequestItemsCountOutOfRange = "Label.PrintRequestItemsCountOutOfRange";
    public const string LabelPrintRequestItemsInvalid = "Label.PrintRequestItemsInvalid";
    public const string LabelReasonTooLong = "Label.ReasonTooLong";
    public const string LabelRecordNotFound = "Label.RecordNotFound";
    public const string LabelReprintReasonTooShort = "Label.ReprintReasonTooShort";
    public const string LabelScanValueRequired = "Label.ScanValueRequired";
    public const string LabelScanValueTooLong = "Label.ScanValueTooLong";
    public const string LabelStatusQueryTooManyItems = "Label.StatusQueryTooManyItems";
    public const string LabelWidthOutOfRange = "Label.WidthOutOfRange";

    private static Dictionary<string, string[]> LabelParameters() => new()
    {
        [LabelCannotPrintBlocked] = ["blockedReason", "displayName", "entityId", "entityType"],
        [LabelCannotPrintNoCode] = ["displayName", "entityId", "entityType"],
        [LabelDefaultCopiesOutOfRange] = ["max", "min"],
        [LabelFormatForTypeNotFound] = ["entityType"],
        [LabelFormatNotFound] = ["id"],
        [LabelHeightOutOfRange] = ["maxMm", "minMm"],
        [LabelItemCopiesOutOfRange] = ["index", "max", "min"],
        [LabelItemDuplicate] = ["entityId", "entityType", "index"],
        [LabelItemEntityIdInvalid] = ["index"],
        [LabelItemEntityTypeInvalid] = ["index"],
        [LabelLayoutInvalid] = [],
        [LabelPrintRequestItemsCountOutOfRange] = ["maxItems", "minItems"],
        [LabelPrintRequestItemsInvalid] = ["message"],
        [LabelReasonTooLong] = ["maxLength"],
        [LabelRecordNotFound] = ["entityId", "entityType"],
        [LabelReprintReasonTooShort] = ["minLength"],
        [LabelScanValueRequired] = [],
        [LabelScanValueTooLong] = ["maxLength"],
        [LabelStatusQueryTooManyItems] = ["maxItems"],
        [LabelWidthOutOfRange] = ["maxMm", "minMm"],
    };
}
