namespace MicroLIMS.Shared.Messages;

// MasterData.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string MasterDataCoaOrderOutOfRange = "MasterData.CoaOrderOutOfRange";
    public const string MasterDataCoaOrderOutOfRangeAllowClear = "MasterData.CoaOrderOutOfRangeAllowClear";
    public const string MasterDataCommentTooLong = "MasterData.CommentTooLong";
    public const string MasterDataFileEmpty = "MasterData.FileEmpty";
    public const string MasterDataFileNameTooLong = "MasterData.FileNameTooLong";
    public const string MasterDataFileTooLarge = "MasterData.FileTooLarge";
    public const string MasterDataImportNotFound = "MasterData.ImportNotFound";
    public const string MasterDataLoadAlreadyVerified = "MasterData.LoadAlreadyVerified";
    public const string MasterDataRetiredSpecificationCannotCopy = "MasterData.RetiredSpecificationCannotCopy";
    public const string MasterDataSectionsOutsideMembership = "MasterData.SectionsOutsideMembership";
    public const string MasterDataSelectSpecificationToCopy = "MasterData.SelectSpecificationToCopy";
    public const string MasterDataSourceItemNotFound = "MasterData.SourceItemNotFound";
    public const string MasterDataSpecificationNotOnSource = "MasterData.SpecificationNotOnSource";
    public const string MasterDataTargetItemNotFound = "MasterData.TargetItemNotFound";
    public const string MasterDataTestDefinitionNotFound = "MasterData.TestDefinitionNotFound";
    public const string MasterDataVerifierMustBeSecondPerson = "MasterData.VerifierMustBeSecondPerson";
    public const string MasterDataWorkbookFormatError = "MasterData.WorkbookFormatError";
    public const string MasterDataWorkbookTransactionRequired = "MasterData.WorkbookTransactionRequired";

    private static Dictionary<string, string[]> MasterDataParameters() => new()
    {
        [MasterDataCoaOrderOutOfRange] = ["max"],
        [MasterDataCoaOrderOutOfRangeAllowClear] = ["max"],
        [MasterDataCommentTooLong] = ["maxCommentLength"],
        [MasterDataFileEmpty] = [],
        [MasterDataFileNameTooLong] = ["maxFileNameLength"],
        [MasterDataFileTooLarge] = ["maxMb"],
        [MasterDataImportNotFound] = ["id"],
        [MasterDataLoadAlreadyVerified] = [],
        [MasterDataRetiredSpecificationCannotCopy] = ["id"],
        [MasterDataSectionsOutsideMembership] = ["sections"],
        [MasterDataSelectSpecificationToCopy] = [],
        [MasterDataSourceItemNotFound] = ["sourceItemId"],
        [MasterDataSpecificationNotOnSource] = ["id", "sourceItemId"],
        [MasterDataTargetItemNotFound] = ["targetItemId"],
        [MasterDataTestDefinitionNotFound] = ["id"],
        [MasterDataVerifierMustBeSecondPerson] = [],
        [MasterDataWorkbookFormatError] = ["message"],
        [MasterDataWorkbookTransactionRequired] = [],
    };
}
