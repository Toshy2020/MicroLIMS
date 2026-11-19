namespace MicroLIMS.Shared.Messages;

// ReferenceList.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ReferenceListCauseOfTestingExists = "ReferenceList.CauseOfTestingExists";
    public const string ReferenceListCauseOfTestingInUse = "ReferenceList.CauseOfTestingInUse";
    public const string ReferenceListCauseOfTestingNotFound = "ReferenceList.CauseOfTestingNotFound";
    public const string ReferenceListNameRequired = "ReferenceList.NameRequired";
    public const string ReferenceListProductionStageExists = "ReferenceList.ProductionStageExists";
    public const string ReferenceListProductionStageNotFound = "ReferenceList.ProductionStageNotFound";
    public const string ReferenceListSamplerExists = "ReferenceList.SamplerExists";
    public const string ReferenceListSamplerNotFound = "ReferenceList.SamplerNotFound";

    private static Dictionary<string, string[]> ReferenceListParameters() => new()
    {
        [ReferenceListCauseOfTestingExists] = ["name"],
        [ReferenceListCauseOfTestingInUse] = ["name", "sampleCount"],
        [ReferenceListCauseOfTestingNotFound] = ["id"],
        [ReferenceListNameRequired] = [],
        [ReferenceListProductionStageExists] = ["name"],
        [ReferenceListProductionStageNotFound] = ["id"],
        [ReferenceListSamplerExists] = ["name"],
        [ReferenceListSamplerNotFound] = ["id"],
    };
}
