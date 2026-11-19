namespace MicroLIMS.Shared.Messages;

// Organism.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string OrganismCodeAlreadyExists = "Organism.CodeAlreadyExists";
    public const string OrganismCodeChangeReasonRequired = "Organism.CodeChangeReasonRequired";
    public const string OrganismCodePrefixRequired = "Organism.CodePrefixRequired";
    public const string OrganismCodeRequired = "Organism.CodeRequired";
    public const string OrganismCodeTooLong = "Organism.CodeTooLong";
    public const string OrganismCodeUnchanged = "Organism.CodeUnchanged";
    public const string OrganismCultureCollectionRequired = "Organism.CultureCollectionRequired";
    public const string OrganismHasStockChangeCode = "Organism.HasStockChangeCode";
    public const string OrganismHasStockIdentityLocked = "Organism.HasStockIdentityLocked";
    public const string OrganismInactive = "Organism.Inactive";
    public const string OrganismNotFound = "Organism.NotFound";
    public const string OrganismOnlyOwnerLab = "Organism.OnlyOwnerLab";
    public const string OrganismSpeciesNotFound = "Organism.SpeciesNotFound";
    public const string OrganismStrainAlreadyListed = "Organism.StrainAlreadyListed";
    public const string OrganismStrainNumberRequired = "Organism.StrainNumberRequired";
    public const string OrganismStrainNumberTooLong = "Organism.StrainNumberTooLong";

    private static Dictionary<string, string[]> OrganismParameters() => new()
    {
        [OrganismCodeAlreadyExists] = ["code"],
        [OrganismCodeChangeReasonRequired] = [],
        [OrganismCodePrefixRequired] = ["prefix"],
        [OrganismCodeRequired] = [],
        [OrganismCodeTooLong] = [],
        [OrganismCodeUnchanged] = [],
        [OrganismCultureCollectionRequired] = [],
        [OrganismHasStockChangeCode] = [],
        [OrganismHasStockIdentityLocked] = [],
        [OrganismInactive] = ["code", "name"],
        [OrganismNotFound] = ["id"],
        [OrganismOnlyOwnerLab] = [],
        [OrganismSpeciesNotFound] = ["organismId"],
        [OrganismStrainAlreadyListed] = ["code", "collection", "strainNumber"],
        [OrganismStrainNumberRequired] = [],
        [OrganismStrainNumberTooLong] = [],
    };
}
