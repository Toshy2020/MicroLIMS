namespace MicroLIMS.Shared.Messages;

// Uv.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string UvAbsorbancePositive = "Uv.AbsorbancePositive";
    public const string UvAbsorbanceRangeAboveZero = "Uv.AbsorbanceRangeAboveZero";
    public const string UvAbsorbanceRangeFromZero = "Uv.AbsorbanceRangeFromZero";
    public const string UvCalculationModeRequired = "Uv.CalculationModeRequired";
    public const string UvEquipmentNotSpectrophotometer = "Uv.EquipmentNotSpectrophotometer";
    public const string UvEquipmentRequired = "Uv.EquipmentRequired";
    public const string UvInvalidEquationType = "Uv.InvalidEquationType";
    public const string UvInvalidReplicateCount = "Uv.InvalidReplicateCount";
    public const string UvReferenceAbsorbanceNotUsed = "Uv.ReferenceAbsorbanceNotUsed";
    public const string UvReferenceAbsorbanceRequired = "Uv.ReferenceAbsorbanceRequired";
    public const string UvReplicateAbsorbanceRequired = "Uv.ReplicateAbsorbanceRequired";
    public const string UvReplicateCountMismatch = "Uv.ReplicateCountMismatch";
    public const string UvReplicateWeightNotUsed = "Uv.ReplicateWeightNotUsed";
    public const string UvReplicateWeightPositive = "Uv.ReplicateWeightPositive";
    public const string UvSpecBasisNotUsedInLimitTest = "Uv.SpecBasisNotUsedInLimitTest";
    public const string UvSpecInvalidResultBasis = "Uv.SpecInvalidResultBasis";
    public const string UvSpecMustBeNumericLimit = "Uv.SpecMustBeNumericLimit";
    public const string UvSpecMustBeQualitative = "Uv.SpecMustBeQualitative";
    public const string UvSpecNotAllowed = "Uv.SpecNotAllowed";
    public const string UvSpecificAbsorbancePositive = "Uv.SpecificAbsorbancePositive";
    public const string UvSpecificAbsorbanceRequired = "Uv.SpecificAbsorbanceRequired";
    public const string UvStandardAbsorbanceRange = "Uv.StandardAbsorbanceRange";
    public const string UvStandardLotExpired = "Uv.StandardLotExpired";
    public const string UvStandardLotNoMoisture = "Uv.StandardLotNoMoisture";
    public const string UvStandardLotNoStock = "Uv.StandardLotNoStock";
    public const string UvStandardLotRequired = "Uv.StandardLotRequired";
    public const string UvStandardLotWrongEntry = "Uv.StandardLotWrongEntry";
    public const string UvStandardLotWrongLab = "Uv.StandardLotWrongLab";
    public const string UvStandardMaterialNotConfigured = "Uv.StandardMaterialNotConfigured";
    public const string UvStandardModeSingleParameter = "Uv.StandardModeSingleParameter";
    public const string UvStandardNotUsed = "Uv.StandardNotUsed";
    public const string UvStandardVolumeRequired = "Uv.StandardVolumeRequired";
    public const string UvStandardWeightPositive = "Uv.StandardWeightPositive";
    public const string UvTotalVolumePositive = "Uv.TotalVolumePositive";
    public const string UvTotalVolumeRequired = "Uv.TotalVolumeRequired";
    public const string UvWavelengthOutOfRange = "Uv.WavelengthOutOfRange";

    private static Dictionary<string, string[]> UvParameters() => new()
    {
        [UvAbsorbancePositive] = [],
        [UvAbsorbanceRangeAboveZero] = [],
        [UvAbsorbanceRangeFromZero] = [],
        [UvCalculationModeRequired] = [],
        [UvEquipmentNotSpectrophotometer] = [],
        [UvEquipmentRequired] = [],
        [UvInvalidEquationType] = [],
        [UvInvalidReplicateCount] = [],
        [UvReferenceAbsorbanceNotUsed] = [],
        [UvReferenceAbsorbanceRequired] = [],
        [UvReplicateAbsorbanceRequired] = [],
        [UvReplicateCountMismatch] = ["count"],
        [UvReplicateWeightNotUsed] = ["replicate"],
        [UvReplicateWeightPositive] = ["replicate"],
        [UvSpecBasisNotUsedInLimitTest] = ["parameter"],
        [UvSpecInvalidResultBasis] = ["parameter"],
        [UvSpecMustBeNumericLimit] = ["parameter"],
        [UvSpecMustBeQualitative] = ["parameter"],
        [UvSpecNotAllowed] = ["parameter"],
        [UvSpecificAbsorbancePositive] = [],
        [UvSpecificAbsorbanceRequired] = [],
        [UvStandardAbsorbanceRange] = [],
        [UvStandardLotExpired] = ["lot", "date"],
        [UvStandardLotNoMoisture] = ["lot"],
        [UvStandardLotNoStock] = ["lot"],
        [UvStandardLotRequired] = [],
        [UvStandardLotWrongEntry] = ["lot", "entryName"],
        [UvStandardLotWrongLab] = ["lot"],
        [UvStandardMaterialNotConfigured] = [],
        [UvStandardModeSingleParameter] = [],
        [UvStandardNotUsed] = [],
        [UvStandardVolumeRequired] = [],
        [UvStandardWeightPositive] = [],
        [UvTotalVolumePositive] = [],
        [UvTotalVolumeRequired] = [],
        [UvWavelengthOutOfRange] = ["min", "max"],
    };
}
