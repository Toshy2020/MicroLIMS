namespace MicroLIMS.Shared.Messages;

// EquipmentConfig.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string EquipmentConfigAssetAlreadyLinked = "EquipmentConfig.AssetAlreadyLinked";
    public const string EquipmentConfigAutoclaveNotFound = "EquipmentConfig.AutoclaveNotFound";
    public const string EquipmentConfigAutoclaveProgramNotFound = "EquipmentConfig.AutoclaveProgramNotFound";
    public const string EquipmentConfigCdsSoftwareHplcRequired = "EquipmentConfig.CdsSoftwareHplcRequired";
    public const string EquipmentConfigCdsSoftwareIcpOesRequired = "EquipmentConfig.CdsSoftwareIcpOesRequired";
    public const string EquipmentConfigCdsSoftwareOnlyHplcIcpOes = "EquipmentConfig.CdsSoftwareOnlyHplcIcpOes";
    public const string EquipmentConfigCodeAlreadyExists = "EquipmentConfig.CodeAlreadyExists";
    public const string EquipmentConfigCodeRequired = "EquipmentConfig.CodeRequired";
    public const string EquipmentConfigEquipmentInventoryAssetNotFound = "EquipmentConfig.EquipmentInventoryAssetNotFound";
    public const string EquipmentConfigEquipmentNotFound = "EquipmentConfig.EquipmentNotFound";
    public const string EquipmentConfigInventoryEquipmentNotFound = "EquipmentConfig.InventoryEquipmentNotFound";
    public const string EquipmentConfigMicrobiologySectionMissing = "EquipmentConfig.MicrobiologySectionMissing";
    public const string EquipmentConfigNameRequired = "EquipmentConfig.NameRequired";
    public const string EquipmentConfigNotAnIncubator = "EquipmentConfig.NotAnIncubator";
    public const string EquipmentConfigProgramCodeExists = "EquipmentConfig.ProgramCodeExists";
    public const string EquipmentConfigSetPointReasonRequired = "EquipmentConfig.SetPointReasonRequired";
    public const string EquipmentConfigVendorTooLong = "EquipmentConfig.VendorTooLong";

    private static Dictionary<string, string[]> EquipmentConfigParameters() => new()
    {
        [EquipmentConfigAssetAlreadyLinked] = [],
        [EquipmentConfigAutoclaveNotFound] = ["equipmentId"],
        [EquipmentConfigAutoclaveProgramNotFound] = ["id"],
        [EquipmentConfigCdsSoftwareHplcRequired] = [],
        [EquipmentConfigCdsSoftwareIcpOesRequired] = [],
        [EquipmentConfigCdsSoftwareOnlyHplcIcpOes] = [],
        [EquipmentConfigCodeAlreadyExists] = ["code"],
        [EquipmentConfigCodeRequired] = [],
        [EquipmentConfigEquipmentInventoryAssetNotFound] = ["assetId"],
        [EquipmentConfigEquipmentNotFound] = ["id"],
        [EquipmentConfigInventoryEquipmentNotFound] = ["inventoryEquipmentId"],
        [EquipmentConfigMicrobiologySectionMissing] = [],
        [EquipmentConfigNameRequired] = [],
        [EquipmentConfigNotAnIncubator] = ["code"],
        [EquipmentConfigProgramCodeExists] = ["programCode", "code"],
        [EquipmentConfigSetPointReasonRequired] = [],
        [EquipmentConfigVendorTooLong] = [],
    };
}
