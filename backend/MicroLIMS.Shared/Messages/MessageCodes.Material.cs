namespace MicroLIMS.Shared.Messages;

// Material.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string MaterialCategoryCannotChangeOnUpdate = "Material.CategoryCannotChangeOnUpdate";
    public const string MaterialCategoryInactive = "Material.CategoryInactive";
    public const string MaterialCategoryNotInLab = "Material.CategoryNotInLab";
    public const string MaterialCurrentCoaRequired = "Material.CurrentCoaRequired";
    public const string MaterialCustomTypeNotAcceptedOnCreate = "Material.CustomTypeNotAcceptedOnCreate";
    public const string MaterialCustomTypeNotAcceptedOnUpdate = "Material.CustomTypeNotAcceptedOnUpdate";
    public const string MaterialExpiryDateRequiredForCategory = "Material.ExpiryDateRequiredForCategory";
    public const string MaterialInsufficientStock = "Material.InsufficientStock";
    public const string MaterialLegacyTypeRetired = "Material.LegacyTypeRetired";
    public const string MaterialLotAwaitingRelease = "Material.LotAwaitingRelease";
    public const string MaterialLotRejected = "Material.LotRejected";
    public const string MaterialMasterEntryInactive = "Material.MasterEntryInactive";
    public const string MaterialMasterEntryMissingCategory = "Material.MasterEntryMissingCategory";
    public const string MaterialMasterEntryRequired = "Material.MasterEntryRequired";
    public const string MaterialMasterEntryWrongCategory = "Material.MasterEntryWrongCategory";
    public const string MaterialMediaProductInactive = "Material.MediaProductInactive";
    public const string MaterialMediaProductLockedByLots = "Material.MediaProductLockedByLots";
    public const string MaterialMediaProductRequired = "Material.MediaProductRequired";
    public const string MaterialMoistureOnlyForReferenceStandards = "Material.MoistureOnlyForReferenceStandards";
    public const string MaterialMoistureOutOfRange = "Material.MoistureOutOfRange";
    public const string MaterialOrganismMismatch = "Material.OrganismMismatch";
    public const string MaterialPurityOnlyForStandards = "Material.PurityOnlyForStandards";
    public const string MaterialPurityOutOfRange = "Material.PurityOutOfRange";
    public const string MaterialPurityRequiredPrimaryStandard = "Material.PurityRequiredPrimaryStandard";
    public const string MaterialPurityRequiredReferenceStandard = "Material.PurityRequiredReferenceStandard";
    public const string MaterialReferenceOrganismInactive = "Material.ReferenceOrganismInactive";
    public const string MaterialReferenceOrganismLockedByCryovials = "Material.ReferenceOrganismLockedByCryovials";
    public const string MaterialReferenceOrganismNotFound = "Material.ReferenceOrganismNotFound";
    public const string MaterialReferenceOrganismOnlyForLyophilized = "Material.ReferenceOrganismOnlyForLyophilized";
    public const string MaterialReferenceOrganismRequired = "Material.ReferenceOrganismRequired";
    public const string MaterialStorageConditionTooLong = "Material.StorageConditionTooLong";
    public const string MaterialTypeNotOfferedInLab = "Material.TypeNotOfferedInLab";
    public const string MaterialUnexpectedType = "Material.UnexpectedType";
    public const string MaterialWorkingStandardCreatedByQualification = "Material.WorkingStandardCreatedByQualification";
    public const string MaterialWorkingStandardUpdateBlocked = "Material.WorkingStandardUpdateBlocked";

    private static Dictionary<string, string[]> MaterialParameters() => new()
    {
        [MaterialCategoryCannotChangeOnUpdate] = [],
        [MaterialCategoryInactive] = ["name"],
        [MaterialCategoryNotInLab] = ["name"],
        [MaterialCurrentCoaRequired] = ["materialName", "batchNumber"],
        [MaterialCustomTypeNotAcceptedOnCreate] = [],
        [MaterialCustomTypeNotAcceptedOnUpdate] = [],
        [MaterialExpiryDateRequiredForCategory] = ["category"],
        [MaterialInsufficientStock] = ["materialName", "batchNumber", "remaining", "unit", "requested"],
        [MaterialLegacyTypeRetired] = [],
        [MaterialLotAwaitingRelease] = ["batchNumber", "materialName"],
        [MaterialLotRejected] = ["batchNumber", "materialName"],
        [MaterialMasterEntryInactive] = ["code"],
        [MaterialMasterEntryMissingCategory] = ["code"],
        [MaterialMasterEntryRequired] = [],
        [MaterialMasterEntryWrongCategory] = ["code", "actual", "expected"],
        [MaterialMediaProductInactive] = ["name"],
        [MaterialMediaProductLockedByLots] = ["batchNumber"],
        [MaterialMediaProductRequired] = [],
        [MaterialMoistureOnlyForReferenceStandards] = [],
        [MaterialMoistureOutOfRange] = [],
        [MaterialOrganismMismatch] = [],
        [MaterialPurityOnlyForStandards] = [],
        [MaterialPurityOutOfRange] = [],
        [MaterialPurityRequiredPrimaryStandard] = [],
        [MaterialPurityRequiredReferenceStandard] = [],
        [MaterialReferenceOrganismInactive] = ["code", "name"],
        [MaterialReferenceOrganismLockedByCryovials] = ["batchNumber"],
        [MaterialReferenceOrganismNotFound] = ["referenceOrganismId"],
        [MaterialReferenceOrganismOnlyForLyophilized] = [],
        [MaterialReferenceOrganismRequired] = [],
        [MaterialStorageConditionTooLong] = [],
        [MaterialTypeNotOfferedInLab] = ["typeLabel"],
        [MaterialUnexpectedType] = ["materialName", "expectedType"],
        [MaterialWorkingStandardCreatedByQualification] = [],
        [MaterialWorkingStandardUpdateBlocked] = [],
    };
}
