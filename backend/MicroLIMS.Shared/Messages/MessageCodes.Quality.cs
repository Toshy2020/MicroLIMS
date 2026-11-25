namespace MicroLIMS.Shared.Messages;

public static partial class MessageCodes
{
    public const string QualityNotFound = "Quality.NotFound";
    public const string QualityAccessDenied = "Quality.AccessDenied";
    public const string QualityInvalidType = "Quality.InvalidType";
    public const string QualityInvalidTarget = "Quality.InvalidTarget";
    public const string QualityInvalidText = "Quality.InvalidText";
    public const string QualityStale = "Quality.Stale";
    public const string QualityVersionRequired = "Quality.VersionRequired";
    public const string QualityRaiserOrOwner = "Quality.RaiserOrOwner";
    public const string QualityTransactionRequired = "Quality.TransactionRequired";
    public const string QualitySelfLink = "Quality.SelfLink";
    public const string QualityDuplicateLink = "Quality.DuplicateLink";
    public const string QualitySignedLink = "Quality.SignedLink";
    public const string QualityInvalidAttachment = "Quality.InvalidAttachment";

    private static Dictionary<string, string[]> QualityParameters() => new()
    {
        [QualityNotFound] = [],
        [QualityAccessDenied] = [],
        [QualityInvalidType] = [],
        [QualityInvalidTarget] = [],
        [QualityInvalidText] = ["max"],
        [QualityStale] = [],
        [QualityVersionRequired] = [],
        [QualityRaiserOrOwner] = [],
        [QualityTransactionRequired] = [],
        [QualitySelfLink] = [],
        [QualityDuplicateLink] = [],
        [QualitySignedLink] = [],
        [QualityInvalidAttachment] = [],
    };
}
