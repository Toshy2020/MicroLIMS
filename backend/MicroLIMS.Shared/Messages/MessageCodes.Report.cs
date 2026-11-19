namespace MicroLIMS.Shared.Messages;

// Report.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string ReportTrendingNumericOnly = "Report.TrendingNumericOnly";

    private static Dictionary<string, string[]> ReportParameters() => new()
    {
        [ReportTrendingNumericOnly] = ["testCode"],
    };
}
