namespace MicroLIMS.Shared.Messages;

// EquipmentConfig.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string EquipmentConfigAutoclaveNotFound = "EquipmentConfig.AutoclaveNotFound";
    public const string EquipmentConfigAutoclaveProgramNotFound = "EquipmentConfig.AutoclaveProgramNotFound";
    public const string EquipmentConfigEquipmentNotFound = "EquipmentConfig.EquipmentNotFound";
    public const string EquipmentConfigInventoryEquipmentNotFound = "EquipmentConfig.InventoryEquipmentNotFound";
    public const string EquipmentConfigMicrobiologySectionMissing = "EquipmentConfig.MicrobiologySectionMissing";
    public const string EquipmentConfigNotAnIncubator = "EquipmentConfig.NotAnIncubator";
    public const string EquipmentConfigProgramCodeExists = "EquipmentConfig.ProgramCodeExists";
    public const string EquipmentConfigSetPointReasonRequired = "EquipmentConfig.SetPointReasonRequired";

    private static Dictionary<string, string[]> EquipmentConfigParameters() => new()
    {
        [EquipmentConfigAutoclaveNotFound] = ["equipmentId"],
        [EquipmentConfigAutoclaveProgramNotFound] = ["id"],
        [EquipmentConfigEquipmentNotFound] = ["equipmentId"],
        [EquipmentConfigInventoryEquipmentNotFound] = ["inventoryEquipmentId"],
        [EquipmentConfigMicrobiologySectionMissing] = [],
        [EquipmentConfigNotAnIncubator] = ["code"],
        [EquipmentConfigProgramCodeExists] = ["programCode", "code"],
        [EquipmentConfigSetPointReasonRequired] = [],
    };
}
