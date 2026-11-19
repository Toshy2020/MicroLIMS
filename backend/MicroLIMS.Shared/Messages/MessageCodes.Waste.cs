namespace MicroLIMS.Shared.Messages;

// Waste.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string WasteCryovialBatchNoVialsLeft = "Waste.CryovialBatchNoVialsLeft";
    public const string WasteCryovialBatchNotWithdrawn = "Waste.CryovialBatchNotWithdrawn";
    public const string WasteCryovialNotFound = "Waste.CryovialNotFound";
    public const string WasteCycleNumberRequired = "Waste.CycleNumberRequired";
    public const string WasteCycleStartInFuture = "Waste.CycleStartInFuture";
    public const string WasteDestructionNotFound = "Waste.DestructionNotFound";
    public const string WasteEquipmentNotAutoclave = "Waste.EquipmentNotAutoclave";
    public const string WasteItemRequired = "Waste.ItemRequired";
    public const string WasteOtherItemDescriptionRequired = "Waste.OtherItemDescriptionRequired";
    public const string WasteOtherItemQuantityPositive = "Waste.OtherItemQuantityPositive";
    public const string WasteOtherItemUnitRequired = "Waste.OtherItemUnitRequired";
    public const string WasteProgramInactive = "Waste.ProgramInactive";
    public const string WasteProgramWrongAutoclave = "Waste.ProgramWrongAutoclave";
    public const string WasteThawedVialAlreadyDestroyed = "Waste.ThawedVialAlreadyDestroyed";
    public const string WasteThawedVialNotFound = "Waste.ThawedVialNotFound";
    public const string WasteThawedVialWithinUseWindow = "Waste.ThawedVialWithinUseWindow";

    private static Dictionary<string, string[]> WasteParameters() => new()
    {
        [WasteCryovialBatchNoVialsLeft] = ["code"],
        [WasteCryovialBatchNotWithdrawn] = ["code"],
        [WasteCryovialNotFound] = ["id"],
        [WasteCycleNumberRequired] = [],
        [WasteCycleStartInFuture] = [],
        [WasteDestructionNotFound] = ["id"],
        [WasteEquipmentNotAutoclave] = [],
        [WasteItemRequired] = [],
        [WasteOtherItemDescriptionRequired] = [],
        [WasteOtherItemQuantityPositive] = ["description"],
        [WasteOtherItemUnitRequired] = ["description"],
        [WasteProgramInactive] = ["programCode"],
        [WasteProgramWrongAutoclave] = ["autoclaveCode"],
        [WasteThawedVialAlreadyDestroyed] = ["vial"],
        [WasteThawedVialNotFound] = ["id"],
        [WasteThawedVialWithinUseWindow] = ["hours", "vial"],
    };
}
