namespace MicroLIMS.Shared.Messages;

// AfterCleaning.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string AfterCleaningMachineHasParts = "AfterCleaning.MachineHasParts";
    public const string AfterCleaningMachineNotFound = "AfterCleaning.MachineNotFound";
    public const string AfterCleaningMachinePartConfigNotFound = "AfterCleaning.MachinePartConfigNotFound";
    public const string AfterCleaningMachinePartConfigUsedBySamples = "AfterCleaning.MachinePartConfigUsedBySamples";
    public const string AfterCleaningMachinePartHasConfigs = "AfterCleaning.MachinePartHasConfigs";
    public const string AfterCleaningMachinePartNotFound = "AfterCleaning.MachinePartNotFound";
    public const string AfterCleaningMachineUsedBySamples = "AfterCleaning.MachineUsedBySamples";
    public const string AfterCleaningPartLimitFromHplcMethod = "AfterCleaning.PartLimitFromHplcMethod";
    public const string AfterCleaningPartLimitFromIcpTest = "AfterCleaning.PartLimitFromIcpTest";
    public const string AfterCleaningPartTestAlreadyConfigured = "AfterCleaning.PartTestAlreadyConfigured";

    private static Dictionary<string, string[]> AfterCleaningParameters() => new()
    {
        [AfterCleaningMachineHasParts] = ["name", "partCount"],
        [AfterCleaningMachineNotFound] = ["id"],
        [AfterCleaningMachinePartConfigNotFound] = ["id"],
        [AfterCleaningMachinePartConfigUsedBySamples] = ["testCode", "usedBy"],
        [AfterCleaningMachinePartHasConfigs] = ["configCount", "name"],
        [AfterCleaningMachinePartNotFound] = ["id"],
        [AfterCleaningMachineUsedBySamples] = ["name", "sampleCount"],
        [AfterCleaningPartLimitFromHplcMethod] = ["testCode"],
        [AfterCleaningPartLimitFromIcpTest] = ["testCode"],
        [AfterCleaningPartTestAlreadyConfigured] = ["testCode", "testType"],
    };
}
