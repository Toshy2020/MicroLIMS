namespace MicroLIMS.Shared.Messages;

// EquipmentConfig.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string EquipmentConfigAssetAlreadyLinked = "EquipmentConfig.AssetAlreadyLinked";
    public const string EquipmentConfigCdsSoftwareHplcRequired = "EquipmentConfig.CdsSoftwareHplcRequired";
    public const string EquipmentConfigCdsSoftwareIcpOesRequired = "EquipmentConfig.CdsSoftwareIcpOesRequired";
    public const string EquipmentConfigCdsSoftwareOnlyHplcIcpOes = "EquipmentConfig.CdsSoftwareOnlyHplcIcpOes";
    public const string EquipmentConfigCodeAlreadyExists = "EquipmentConfig.CodeAlreadyExists";
    public const string EquipmentConfigCodeRequired = "EquipmentConfig.CodeRequired";
    public const string EquipmentConfigEquipmentInventoryAssetNotFound = "EquipmentConfig.EquipmentInventoryAssetNotFound";
    public const string EquipmentConfigEquipmentNotFound = "EquipmentConfig.EquipmentNotFound";
    public const string EquipmentConfigNameRequired = "EquipmentConfig.NameRequired";
    public const string EquipmentConfigVendorTooLong = "EquipmentConfig.VendorTooLong";

    private static Dictionary<string, string[]> EquipmentConfigParameters() => new()
    {
        [EquipmentConfigAssetAlreadyLinked] = [],
        [EquipmentConfigCdsSoftwareHplcRequired] = [],
        [EquipmentConfigCdsSoftwareIcpOesRequired] = [],
        [EquipmentConfigCdsSoftwareOnlyHplcIcpOes] = [],
        [EquipmentConfigCodeAlreadyExists] = ["code"],
        [EquipmentConfigCodeRequired] = [],
        [EquipmentConfigEquipmentInventoryAssetNotFound] = ["assetId"],
        [EquipmentConfigEquipmentNotFound] = ["id"],
        [EquipmentConfigNameRequired] = [],
        [EquipmentConfigVendorTooLong] = [],
    };
}
