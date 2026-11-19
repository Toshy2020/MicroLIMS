namespace MicroLIMS.Shared.Messages;

// Standard.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string StandardCodeAllocationFailed = "Standard.CodeAllocationFailed";
    public const string StandardDocumentNotFound = "Standard.DocumentNotFound";
    public const string StandardDraftOnlyForDocuments = "Standard.DraftOnlyForDocuments";
    public const string StandardDraftOnlyForEdit = "Standard.DraftOnlyForEdit";
    public const string StandardFileEmpty = "Standard.FileEmpty";
    public const string StandardFileNameRequired = "Standard.FileNameRequired";
    public const string StandardFileTooLarge = "Standard.FileTooLarge";
    public const string StandardFileTypeUnsupported = "Standard.FileTypeUnsupported";
    public const string StandardLocationRequired = "Standard.LocationRequired";
    public const string StandardLotHasNoMasterEntry = "Standard.LotHasNoMasterEntry";
    public const string StandardLotHasOpenQualification = "Standard.LotHasOpenQualification";
    public const string StandardLotNotFound = "Standard.LotNotFound";
    public const string StandardMasterEntryInactive = "Standard.MasterEntryInactive";
    public const string StandardMasterEntryNotFound = "Standard.MasterEntryNotFound";
    public const string StandardMasterEntryNotReferenceStandard = "Standard.MasterEntryNotReferenceStandard";
    public const string StandardMasterEntryRequired = "Standard.MasterEntryRequired";
    public const string StandardMoistureRange = "Standard.MoistureRange";
    public const string StandardQualificationNotFound = "Standard.QualificationNotFound";
    public const string StandardQualificationWithIdNotFound = "Standard.QualificationWithIdNotFound";
    public const string StandardQuantityLocationOnLot = "Standard.QuantityLocationOnLot";
    public const string StandardQuantityRequired = "Standard.QuantityRequired";
    public const string StandardRequalificationLotRequired = "Standard.RequalificationLotRequired";
    public const string StandardSampleHasNoAssayResult = "Standard.SampleHasNoAssayResult";
    public const string StandardSampleHasNoItemName = "Standard.SampleHasNoItemName";
    public const string StandardSourceSampleOrNameExclusive = "Standard.SourceSampleOrNameExclusive";
    public const string StandardSourceSampleOrNameRequired = "Standard.SourceSampleOrNameRequired";

    private static Dictionary<string, string[]> StandardParameters() => new()
    {
        [StandardCodeAllocationFailed] = [],
        [StandardDocumentNotFound] = ["documentId"],
        [StandardDraftOnlyForDocuments] = [],
        [StandardDraftOnlyForEdit] = [],
        [StandardFileEmpty] = [],
        [StandardFileNameRequired] = [],
        [StandardFileTooLarge] = ["maxMb"],
        [StandardFileTypeUnsupported] = ["contentType"],
        [StandardLocationRequired] = [],
        [StandardLotHasNoMasterEntry] = [],
        [StandardLotHasOpenQualification] = ["lot", "openCode"],
        [StandardLotNotFound] = [],
        [StandardMasterEntryInactive] = ["name"],
        [StandardMasterEntryNotFound] = [],
        [StandardMasterEntryNotReferenceStandard] = ["name"],
        [StandardMasterEntryRequired] = [],
        [StandardMoistureRange] = [],
        [StandardQualificationNotFound] = [],
        [StandardQualificationWithIdNotFound] = ["id"],
        [StandardQuantityLocationOnLot] = [],
        [StandardQuantityRequired] = [],
        [StandardRequalificationLotRequired] = [],
        [StandardSampleHasNoAssayResult] = [],
        [StandardSampleHasNoItemName] = [],
        [StandardSourceSampleOrNameExclusive] = [],
        [StandardSourceSampleOrNameRequired] = [],
    };
}
