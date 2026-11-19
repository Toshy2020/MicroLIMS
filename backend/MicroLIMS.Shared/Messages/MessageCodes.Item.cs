namespace MicroLIMS.Shared.Messages;

// Item.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ItemCannotDeleteNotApplicable = "Item.CannotDeleteNotApplicable";
    public const string ItemCannotDeleteUsed = "Item.CannotDeleteUsed";
    public const string ItemCategoryNotAllowed = "Item.CategoryNotAllowed";
    public const string ItemDuplicateCode = "Item.DuplicateCode";
    public const string ItemFileSizeRange = "Item.FileSizeRange";
    public const string ItemMinSampleQuantityPositive = "Item.MinSampleQuantityPositive";
    public const string ItemMinSampleQuantityUnitRequired = "Item.MinSampleQuantityUnitRequired";
    public const string ItemNoPreparationConfiguration = "Item.NoPreparationConfiguration";
    public const string ItemPreparationAlreadyApproved = "Item.PreparationAlreadyApproved";
    public const string ItemStmInDocumentLibrary = "Item.StmInDocumentLibrary";

    private static Dictionary<string, string[]> ItemParameters() => new()
    {
        [ItemCannotDeleteNotApplicable] = ["name", "naCount"],
        [ItemCannotDeleteUsed] = ["name", "sampleCount"],
        [ItemCategoryNotAllowed] = [],
        [ItemDuplicateCode] = ["code"],
        [ItemFileSizeRange] = [],
        [ItemMinSampleQuantityPositive] = [],
        [ItemMinSampleQuantityUnitRequired] = [],
        [ItemNoPreparationConfiguration] = ["itemId"],
        [ItemPreparationAlreadyApproved] = [],
        [ItemStmInDocumentLibrary] = [],
    };
}
