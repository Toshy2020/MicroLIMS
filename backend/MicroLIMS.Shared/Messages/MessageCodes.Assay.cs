namespace MicroLIMS.Shared.Messages;

// Assay.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string AssayHplcSubmitProblem = "Assay.HplcSubmitProblem";
    public const string AssayIcpSubmitProblem = "Assay.IcpSubmitProblem";
    public const string AssaySpecNotLinkedToElement = "Assay.SpecNotLinkedToElement";

    private static Dictionary<string, string[]> AssayParameters() => new()
    {
        [AssayHplcSubmitProblem] = ["message"],
        [AssayIcpSubmitProblem] = ["message"],
        [AssaySpecNotLinkedToElement] = ["parameter"],
    };
}
