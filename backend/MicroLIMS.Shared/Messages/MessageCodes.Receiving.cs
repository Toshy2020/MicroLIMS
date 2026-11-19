namespace MicroLIMS.Shared.Messages;

// Receiving.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ReceivingAllTestsNotApplicable = "Receiving.AllTestsNotApplicable";
    public const string ReceivingChooseAtLeastOneLab = "Receiving.ChooseAtLeastOneLab";
    public const string ReceivingGradeAppliesToNoTests = "Receiving.GradeAppliesToNoTests";
    public const string ReceivingGradeHasNoSpecification = "Receiving.GradeHasNoSpecification";
    public const string ReceivingGradeRequired = "Receiving.GradeRequired";
    public const string ReceivingItemFrozen = "Receiving.ItemFrozen";
    public const string ReceivingItemHasNoAssignedTests = "Receiving.ItemHasNoAssignedTests";
    public const string ReceivingItemHasNoTestsForSection = "Receiving.ItemHasNoTestsForSection";
    public const string ReceivingItemNotConfigured = "Receiving.ItemNotConfigured";
    public const string ReceivingProductionStageRequired = "Receiving.ProductionStageRequired";
    public const string ReceivingProductionStageUnknown = "Receiving.ProductionStageUnknown";
    public const string ReceivingReferenceNumberClash = "Receiving.ReferenceNumberClash";
    public const string ReceivingSelectedLabsDoNotExist = "Receiving.SelectedLabsDoNotExist";
    public const string ReceivingUnsupportedCategory = "Receiving.UnsupportedCategory";
    public const string ReceivingValidationFailed = "Receiving.ValidationFailed";

    private static Dictionary<string, string[]> ReceivingParameters() => new()
    {
        [ReceivingAllTestsNotApplicable] = ["item"],
        [ReceivingChooseAtLeastOneLab] = [],
        [ReceivingGradeAppliesToNoTests] = ["grade", "item"],
        [ReceivingGradeHasNoSpecification] = ["grade"],
        [ReceivingGradeRequired] = [],
        [ReceivingItemFrozen] = ["item"],
        [ReceivingItemHasNoAssignedTests] = ["item"],
        [ReceivingItemHasNoTestsForSection] = ["item", "section"],
        [ReceivingItemNotConfigured] = ["itemId"],
        [ReceivingProductionStageRequired] = [],
        [ReceivingProductionStageUnknown] = ["stage"],
        [ReceivingReferenceNumberClash] = ["referenceNumber"],
        [ReceivingSelectedLabsDoNotExist] = [],
        [ReceivingUnsupportedCategory] = ["category"],
        [ReceivingValidationFailed] = ["message"],
    };
}
