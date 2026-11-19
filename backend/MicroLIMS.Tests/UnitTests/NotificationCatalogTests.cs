using System.Text.RegularExpressions;
using MicroLIMS.Application.Services.Notifications;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class NotificationCatalogTests
{
    private static readonly string[] ExpectedEmailedTypes =
    [
        "CalibrationDue",
        "Escalation",
        "ItemOverdue",
        "MediaExpiry",
        "SolutionExpiry",
        "StrainExpiry",
    ];

    private static string ApplicationDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "MicroLIMS.Application"));

    // Current service publishing: `new NotificationRequest(Type: "..." ...)` or a positional first string arg.
    private static readonly Regex NotificationRequestTypeRegex =
        new(@"new\s+NotificationRequest\s*\(\s*(?:Type\s*:\s*)?""([^""]+)""", RegexOptions.Singleline);

    [Fact]
    public void EveryTypeWrittenInApplicationCode_IsInCatalog()
    {
        var appDir = ApplicationDirectory();
        var sep = Path.DirectorySeparatorChar;
        var csFiles = Directory.EnumerateFiles(appDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .ToList();
        Assert.NotEmpty(csFiles);

        var discovered = new HashSet<string>(StringComparer.Ordinal);

        // 1. Direct `new NotificationLog { ... Type = "..." ... }`
        var directRegex = new Regex(@"new\s+NotificationLog\s*\{[^}]*?\bType\s*=\s*""([^""]+)""", RegexOptions.Singleline);
        // 2. ComputedNotification("...", ...) and MonitoringReminder("...", ...) passed into NotificationLog.Type
        var computedRegex = new Regex(@"new\s+(?:ComputedNotification|MonitoringReminder)\s*\(\s*""([^""]+)""", RegexOptions.Singleline);
        // 3. NotificationType constants passed to NotifyAsync / NotificationLog in DocumentReassignmentService
        var constRegex = new Regex(@"(?:NotificationType|OverdueNotificationType)\s*=\s*""([^""]+)""", RegexOptions.Singleline);

        foreach (var file in csFiles)
        {
            var source = File.ReadAllText(file);
            foreach (Match m in directRegex.Matches(source)) discovered.Add(m.Groups[1].Value);
            foreach (Match m in computedRegex.Matches(source)) discovered.Add(m.Groups[1].Value);
            foreach (Match m in constRegex.Matches(source)) discovered.Add(m.Groups[1].Value);
            foreach (Match m in NotificationRequestTypeRegex.Matches(source)) discovered.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(discovered);
        var missing = discovered.Where(t => !NotificationCatalog.Types.ContainsKey(t)).OrderBy(t => t).ToList();
        Assert.True(missing.Count == 0, "Notification types written in Application are missing from NotificationCatalog: " + string.Join(", ", missing));
    }

    [Fact]
    public void NotificationRequestTypeRegex_FindsNamedAndPositionalType()
    {
        const string named = "new NotificationRequest(\n    Type: \"CalibrationDue\",\n    UserIds: ids)";
        const string positional = "new NotificationRequest(\n    \"CalibrationDue\",\n    ids)";

        var namedMatch = NotificationRequestTypeRegex.Match(named);
        var positionalMatch = NotificationRequestTypeRegex.Match(positional);

        Assert.True(namedMatch.Success);
        Assert.Equal("CalibrationDue", namedMatch.Groups[1].Value);
        Assert.True(positionalMatch.Success);
        Assert.Equal("CalibrationDue", positionalMatch.Groups[1].Value);
    }

    [Fact]
    public void OnlyTheSixTypes_AllowEmail()
    {
        var actual = NotificationCatalog.Types.Values
            .Where(t => t.EmailAllowed)
            .Select(t => t.Type)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedEmailedTypes, actual);
    }

    [Fact]
    public void Get_UnknownType_Throws()
    {
        Assert.Throws<ArgumentException>(() => NotificationCatalog.Get("NotARealNotificationType"));
    }

    [Fact]
    public void AllTitleTemplates_AreNonEmptyAndContainNoSensitiveWords()
    {
        var forbidden = new[] { "result", "limit", "password", "token" };
        foreach (var info in NotificationCatalog.Types.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(info.TitleTemplate), $"Empty TitleTemplate for {info.Type}");
            Assert.False(string.IsNullOrWhiteSpace(info.DefaultSeverity), $"Empty DefaultSeverity for {info.Type}");
            foreach (var word in forbidden)
            {
                Assert.DoesNotContain(word, info.TitleTemplate, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
