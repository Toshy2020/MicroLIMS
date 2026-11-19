namespace MicroLIMS.Shared.Messages;

// Cryovial.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string CryovialAlreadyDecided = "Cryovial.AlreadyDecided";
    public const string CryovialBatchExpired = "Cryovial.BatchExpired";
    public const string CryovialCannotApproveOwnBatch = "Cryovial.CannotApproveOwnBatch";
    public const string CryovialCodeClash = "Cryovial.CodeClash";
    public const string CryovialConfirmationMediaNotFound = "Cryovial.ConfirmationMediaNotFound";
    public const string CryovialConfirmationMediaNotUsable = "Cryovial.ConfirmationMediaNotUsable";
    public const string CryovialDestroyedCannotMove = "Cryovial.DestroyedCannotMove";
    public const string CryovialIdentityConfirmationRequired = "Cryovial.IdentityConfirmationRequired";
    public const string CryovialMaterialNotLyophilized = "Cryovial.MaterialNotLyophilized";
    public const string CryovialMaterialOrganismRequired = "Cryovial.MaterialOrganismRequired";
    public const string CryovialNoVialsRemaining = "Cryovial.NoVialsRemaining";
    public const string CryovialNotApprovedCannotMove = "Cryovial.NotApprovedCannotMove";
    public const string CryovialNotApprovedForUse = "Cryovial.NotApprovedForUse";
    public const string CryovialNotFound = "Cryovial.NotFound";
    public const string CryovialPhysicalCheckRequired = "Cryovial.PhysicalCheckRequired";
    public const string CryovialStorageMoveReasonRequired = "Cryovial.StorageMoveReasonRequired";
    public const string CryovialThawedVialDestroyed = "Cryovial.ThawedVialDestroyed";
    public const string CryovialThawedVialExpired = "Cryovial.ThawedVialExpired";
    public const string CryovialThawedVialNotFound = "Cryovial.ThawedVialNotFound";
    public const string CryovialWithdrawnForDestruction = "Cryovial.WithdrawnForDestruction";

    private static Dictionary<string, string[]> CryovialParameters() => new()
    {
        [CryovialAlreadyDecided] = ["code", "status"],
        [CryovialBatchExpired] = ["code"],
        [CryovialCannotApproveOwnBatch] = [],
        [CryovialCodeClash] = ["code"],
        [CryovialConfirmationMediaNotFound] = ["mediaId"],
        [CryovialConfirmationMediaNotUsable] = ["lotNumber"],
        [CryovialDestroyedCannotMove] = ["code"],
        [CryovialIdentityConfirmationRequired] = [],
        [CryovialMaterialNotLyophilized] = ["materialName"],
        [CryovialMaterialOrganismRequired] = ["materialName"],
        [CryovialNoVialsRemaining] = ["code"],
        [CryovialNotApprovedCannotMove] = ["code"],
        [CryovialNotApprovedForUse] = ["code"],
        [CryovialNotFound] = ["cryovialId"],
        [CryovialPhysicalCheckRequired] = [],
        [CryovialStorageMoveReasonRequired] = [],
        [CryovialThawedVialDestroyed] = ["code", "thawedAt"],
        [CryovialThawedVialExpired] = ["code", "thawedAt", "hours"],
        [CryovialThawedVialNotFound] = ["thawEventId"],
        [CryovialWithdrawnForDestruction] = ["code"],
    };
}
