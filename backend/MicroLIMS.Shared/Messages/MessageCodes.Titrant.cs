namespace MicroLIMS.Shared.Messages;

// Titrant.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string TitrantNoPrimaryStandardConfigured = "Titrant.NoPrimaryStandardConfigured";
    public const string TitrantNoReferenceSolutionConfigured = "Titrant.NoReferenceSolutionConfigured";
    public const string TitrantNotConfiguredForStandardization = "Titrant.NotConfiguredForStandardization";
    public const string TitrantOnlyTitrantPreparations = "Titrant.OnlyTitrantPreparations";
    public const string TitrantPreparationExpiredOrDiscarded = "Titrant.PreparationExpiredOrDiscarded";
    public const string TitrantReplicateBlankNotUsed = "Titrant.ReplicateBlankNotUsed";
    public const string TitrantReplicateBlankRequired = "Titrant.ReplicateBlankRequired";
    public const string TitrantReplicateCalculationFailed = "Titrant.ReplicateCalculationFailed";
    public const string TitrantReplicateCountMismatch = "Titrant.ReplicateCountMismatch";
    public const string TitrantReplicateReferenceExpiredOrDiscarded = "Titrant.ReplicateReferenceExpiredOrDiscarded";
    public const string TitrantReplicateReferenceNoNominalStrength = "Titrant.ReplicateReferenceNoNominalStrength";
    public const string TitrantReplicateReferenceNotApplicable = "Titrant.ReplicateReferenceNotApplicable";
    public const string TitrantReplicateReferenceNotFound = "Titrant.ReplicateReferenceNotFound";
    public const string TitrantReplicateReferenceRequired = "Titrant.ReplicateReferenceRequired";
    public const string TitrantReplicateReferenceSnapshotUnreadable = "Titrant.ReplicateReferenceSnapshotUnreadable";
    public const string TitrantReplicateReferenceStandardizationInvalid = "Titrant.ReplicateReferenceStandardizationInvalid";
    public const string TitrantReplicateReferenceStrengthUnitMismatch = "Titrant.ReplicateReferenceStrengthUnitMismatch";
    public const string TitrantReplicateReferenceWrongLab = "Titrant.ReplicateReferenceWrongLab";
    public const string TitrantReplicateReferenceWrongSolution = "Titrant.ReplicateReferenceWrongSolution";
    public const string TitrantReplicateStandardFieldsNotApplicable = "Titrant.ReplicateStandardFieldsNotApplicable";
    public const string TitrantReplicateStandardLotAndWeightRequired = "Titrant.ReplicateStandardLotAndWeightRequired";
    public const string TitrantReplicateStandardLotNoPurity = "Titrant.ReplicateStandardLotNoPurity";
    public const string TitrantReplicateStandardLotNotPrimary = "Titrant.ReplicateStandardLotNotPrimary";
    public const string TitrantReplicateStandardLotUnusable = "Titrant.ReplicateStandardLotUnusable";
    public const string TitrantReplicateStandardLotWrongLab = "Titrant.ReplicateStandardLotWrongLab";
    public const string TitrantReplicateStandardMaterialNotFound = "Titrant.ReplicateStandardMaterialNotFound";
    public const string TitrantReplicateVolumeInvalid = "Titrant.ReplicateVolumeInvalid";
    public const string TitrantStandardLotUnitUnsupported = "Titrant.StandardLotUnitUnsupported";
    public const string TitrantStandardLotUnusable = "Titrant.StandardLotUnusable";
    public const string TitrantTemperatureOutOfRange = "Titrant.TemperatureOutOfRange";

    private static Dictionary<string, string[]> TitrantParameters() => new()
    {
        [TitrantNoPrimaryStandardConfigured] = [],
        [TitrantNoReferenceSolutionConfigured] = [],
        [TitrantNotConfiguredForStandardization] = [],
        [TitrantOnlyTitrantPreparations] = [],
        [TitrantPreparationExpiredOrDiscarded] = [],
        [TitrantReplicateBlankNotUsed] = ["replicateNo"],
        [TitrantReplicateBlankRequired] = ["replicateNo"],
        [TitrantReplicateCalculationFailed] = ["replicateNo", "message"],
        [TitrantReplicateCountMismatch] = ["count"],
        [TitrantReplicateReferenceExpiredOrDiscarded] = ["replicateNo"],
        [TitrantReplicateReferenceNoNominalStrength] = ["replicateNo"],
        [TitrantReplicateReferenceNotApplicable] = ["replicateNo"],
        [TitrantReplicateReferenceNotFound] = ["replicateNo"],
        [TitrantReplicateReferenceRequired] = ["replicateNo"],
        [TitrantReplicateReferenceSnapshotUnreadable] = ["replicateNo"],
        [TitrantReplicateReferenceStandardizationInvalid] = ["replicateNo"],
        [TitrantReplicateReferenceStrengthUnitMismatch] = ["replicateNo"],
        [TitrantReplicateReferenceWrongLab] = ["replicateNo"],
        [TitrantReplicateReferenceWrongSolution] = ["replicateNo"],
        [TitrantReplicateStandardFieldsNotApplicable] = ["replicateNo"],
        [TitrantReplicateStandardLotAndWeightRequired] = ["replicateNo"],
        [TitrantReplicateStandardLotNoPurity] = ["replicateNo", "batchNumber"],
        [TitrantReplicateStandardLotNotPrimary] = ["replicateNo", "batchNumber"],
        [TitrantReplicateStandardLotUnusable] = ["replicateNo", "batchNumber", "reason"],
        [TitrantReplicateStandardLotWrongLab] = ["replicateNo"],
        [TitrantReplicateStandardMaterialNotFound] = ["replicateNo"],
        [TitrantReplicateVolumeInvalid] = ["replicateNo"],
        [TitrantStandardLotUnitUnsupported] = ["lotLabel", "unit"],
        [TitrantStandardLotUnusable] = ["lotLabel", "reason"],
        [TitrantTemperatureOutOfRange] = [],
    };
}
