namespace MicroLIMS.Shared.Messages;

// Result.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ResultCountTestReadingMissingSample = "Result.CountTestReadingMissingSample";
    public const string ResultCountTestReadingMissingTestOrder = "Result.CountTestReadingMissingTestOrder";
    public const string ResultCountTestReadingNotFound = "Result.CountTestReadingNotFound";
    public const string ResultParameterResultMissingSample = "Result.ParameterResultMissingSample";
    public const string ResultParameterResultMissingTestAnalysis = "Result.ParameterResultMissingTestAnalysis";
    public const string ResultParameterResultMissingTestOrder = "Result.ParameterResultMissingTestOrder";
    public const string ResultParameterResultNotFound = "Result.ParameterResultNotFound";
    public const string ResultSampleLocationMissingResult = "Result.SampleLocationMissingResult";
    public const string ResultSampleLocationMissingSample = "Result.SampleLocationMissingSample";
    public const string ResultSampleLocationMissingTestOrder = "Result.SampleLocationMissingTestOrder";
    public const string ResultSampleLocationNotFound = "Result.SampleLocationNotFound";
    public const string ResultSampleNotFound = "Result.SampleNotFound";
    public const string ResultTestCodeMissingWorkflowTemplate = "Result.TestCodeMissingWorkflowTemplate";
    public const string ResultTestOrderMissingSample = "Result.TestOrderMissingSample";
    public const string ResultTestOrderMissingWorkflowStepResult = "Result.TestOrderMissingWorkflowStepResult";
    public const string ResultTestOrderNotFound = "Result.TestOrderNotFound";

    private static Dictionary<string, string[]> ResultParameters() => new()
    {
        [ResultCountTestReadingMissingSample] = ["countTestReadingId", "testOrderId"],
        [ResultCountTestReadingMissingTestOrder] = ["countTestReadingId"],
        [ResultCountTestReadingNotFound] = ["countTestReadingId"],
        [ResultParameterResultMissingSample] = ["parameterResultId", "testOrderId"],
        [ResultParameterResultMissingTestAnalysis] = ["parameterResultId"],
        [ResultParameterResultMissingTestOrder] = ["parameterResultId"],
        [ResultParameterResultNotFound] = ["parameterResultId"],
        [ResultSampleLocationMissingResult] = ["sampleLocationId"],
        [ResultSampleLocationMissingSample] = ["sampleLocationId"],
        [ResultSampleLocationMissingTestOrder] = ["sampleLocationId"],
        [ResultSampleLocationNotFound] = ["sampleLocationId"],
        [ResultSampleNotFound] = ["sampleId"],
        [ResultTestCodeMissingWorkflowTemplate] = ["testCode"],
        [ResultTestOrderMissingSample] = ["testOrderId"],
        [ResultTestOrderMissingWorkflowStepResult] = ["testOrderId"],
        [ResultTestOrderNotFound] = ["testOrderId"],
    };
}
