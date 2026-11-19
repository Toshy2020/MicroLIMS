namespace MicroLIMS.Shared.Messages;

// Column.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ColumnCodeExists = "Column.CodeExists";
    public const string ColumnCodeRequired = "Column.CodeRequired";
    public const string ColumnCompatibleEquipmentNotChromatography = "Column.CompatibleEquipmentNotChromatography";
    public const string ColumnCompatibleEquipmentNotFound = "Column.CompatibleEquipmentNotFound";
    public const string ColumnCompatibleEquipmentWrongSection = "Column.CompatibleEquipmentWrongSection";
    public const string ColumnNameRequired = "Column.NameRequired";
    public const string ColumnNotFound = "Column.NotFound";
    public const string ColumnSerialNumberTooLong = "Column.SerialNumberTooLong";
    public const string ColumnUspDesignationTooLong = "Column.UspDesignationTooLong";

    private static Dictionary<string, string[]> ColumnParameters() => new()
    {
        [ColumnCodeExists] = ["code"],
        [ColumnCodeRequired] = [],
        [ColumnCompatibleEquipmentNotChromatography] = ["equipment"],
        [ColumnCompatibleEquipmentNotFound] = [],
        [ColumnCompatibleEquipmentWrongSection] = ["equipment"],
        [ColumnNameRequired] = [],
        [ColumnNotFound] = ["columnId"],
        [ColumnSerialNumberTooLong] = [],
        [ColumnUspDesignationTooLong] = [],
    };
}
