using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

public class RecordingPointCoverageTests
{
    private static readonly Dictionary<string, string> CompetencyExempt = new(StringComparer.Ordinal)
    {
        ["PathogenSessionService.ResetSessionAsync"] = "Supervisory reset invalidates existing records under TestWorkflow.Supervise.",
        ["PathogenConfirmationRecorder.RecordBiochemicalReviewDecisionAsync"] = "This is a technical review decision outside analyst competency scope.",
        ["WaterWorkflowEngine.CalculateAndCompareAsync"] = "Legacy POST api/water/calculate has no production client caller.",
        ["LocationPathogenObservationService.RecordPrimaryObservationAsync"] = "This direct observation writer has no production caller.",
        ["AccessReviewService.AddFindingAsync"] = "RecordedByUserId attributes an access review finding, outside laboratory recording.",
        ["AccessReviewService.UpdateFindingAsync"] = "RecordedByUserId attributes an access review finding, outside laboratory recording.",
        ["AuditTrailReviewService.AddFindingAsync"] = "RecordedByUserId attributes an audit review finding, outside laboratory recording.",
        ["BiologicalWasteDestructionService.RecordAsync"] = "RecordedByUserId attributes a biological waste destruction record.",
        ["AnalystAvailabilityService.CreateAsync"] = "EnteredByUserId attributes an analyst availability entry, outside laboratory recording.",
        ["AnalystAvailabilityService.UpdateAsync"] = "EnteredByUserId attributes an analyst availability entry, outside laboratory recording.",
        ["SampleSummaryService.GetCertificateSummaryAsync"] = "Certificate generation projects result attribution into a read model.",
        ["SampleSummaryService.GetSummaryAsync"] = "The summary projects result attribution into a read model.",
        ["SampleSummaryService.GenerateSummaryPdfAsync"] = "The PDF renders a summary projection of existing results.",
        ["SampleSummaryService.GenerateSummaryWordAsync"] = "The Word document renders a summary projection of existing results.",
        ["SampleSummaryService.BuildReportDocumentAsync"] = "The report projects existing result attribution without recording a result.",
        ["SampleSummaryTestOrderBuilder.Build"] = "The summary builder projects existing values into presentation models.",
    };

    private const string Entities = "ParameterResult|CountTestReading|WorkflowStepResult|PathogenObservation|" +
        "ConfirmatoryPlateObservation|LocationPathogenObservation|TestAnalysis|Result";
    private const string Sets = "ParameterResults|CountTestReadings|WorkflowStepResults|PathogenObservations|" +
        "ConfirmatoryPlateObservations|LocationPathogenObservations|TestAnalyses|Results";
    private static readonly Regex Write = new(
        $@"\bnew\s+(?:{Entities})\s*[({{]|\.\s*(?:{Sets})\s*\.\s*(?:Add(?:Range)?|Update(?:Range)?|Attach|Remove(?:Range)?)\s*\(" +
        $@"|\.\s*(?:{Sets})\s*\.[^;{{}}]*?\bExecute(?:Update|Delete)(?:Async)?\s*\(" +
        @"|\b(?:CFUResult|CalculatedResult|ReportedResult|EnteredByUserId|SubmittedByUserId|ObservedByUserId|RecordedByUserId|AnalystDecisionByUserId|InvalidatedByUserId)\s*(?:\?\?=|=(?!=|>))");
    private static readonly Regex Competency = new(@"\b_competency\b");
    private static readonly Regex Class = new(
        @"\bclass\s+(?<name>\w+)(?:\s*\([^{}]*?\))?\s*(?::\s*(?<base>\w+)[^{]*)?\s*\{");
    private static readonly Regex Header = new(
        @"(?m)^\s*(?<access>public|protected|internal|private)\s+(?:(?:async|static|sealed|override|virtual|new|partial)\s+)*" +
        @"[\w<>,.?\[\]()\s]+?\s+(?<name>\w+)(?:<[^>{}]+>)?\s*\((?:[^(){}]|\([^(){}]*\))*\)\s*(?<body>\{|=>)");
    private static readonly Regex Call = new(@"(?<![\w.])(?:(?:this|base)\s*\.\s*)?(?<name>\w+)\s*(?:<[^>{};]+>)?\s*\(");

    private sealed record RecordingRule(string Name, Regex Satisfied, IReadOnlyDictionary<string, string> Exempt);
    private sealed record Method(string Owner, string Name, bool IsPublic, string Body)
    {
        public string Key => $"{Owner}.{Name}";
    }

    private static readonly RecordingRule[] Rules = [new("Competency", Competency, CompetencyExempt)];
    public static IEnumerable<object[]> RuleNames => Rules.Select(rule => new object[] { rule.Name });

    [Theory]
    [MemberData(nameof(RuleNames))]
    public void Public_recording_points_satisfy_the_rule(string name)
    {
        var sources = Sources();
        var findings = Inspect(sources, Rules.Single(rule => rule.Name == name));
        Assert.True(findings.Count == 0, string.Join(Environment.NewLine, findings));
    }

    [Fact]
    public void Facade_never_calls_the_competency_guard()
    {
        var methods = ReadMethods(Sources(), out _).Where(method => method.Owner == "TestWorkflowEngine").ToList();
        Assert.NotEmpty(methods);
        Assert.DoesNotContain(methods, method => Competency.IsMatch(method.Body) ||
            Regex.IsMatch(method.Body, @"\b(?:EvaluateAsync|EvaluateManyAsync|EvaluateAssigneeAsync|EnsureAllowedAsync|RequireCompetentAsync|FilterAsync)\s*\("));
    }

    [Fact]
    public void Scan_matches_recording_writes_guards_and_exemptions()
    {
        string[] sources =
        [
            """
            public class Recorder : Support
            {
                public void Unguarded() => _db.Results.Add(result);
                public void UnguardedThroughCore() => UnitOfWork.RunAsync(_db, () => WriteResult());
                private void WriteResult() { _db.Results.Add(result); }
                public void Guarded() => UnitOfWork.RunAsync(_db, () => Core());
                private void Core() { Require(); _db.Results.Add(result); }
                public void Exempt() => _db.Results.Add(result);
                public void Reset() => _db.Results.RemoveRange(results);
                public string Read() => "_db.Results.Add(result)"; // _db.Results.Add(result);
                public void Interpolation() { _db.Results.Add(result); Log($"{Format("_competency")}"); }
            }
            """,
            """
            public class Support
            {
                protected void Require() => _competency.EvaluateAsync(actor, id);
            }
            """,
        ];
        var rule = new RecordingRule("Competency", Competency, new Dictionary<string, string>
        {
            ["Recorder.Exempt"] = "An explicitly justified exemption.",
            ["Recorder.Gone"] = "An explicitly justified exemption.",
        });
        Assert.Equal(
            new[]
            {
                "Recorder.Interpolation", "Recorder.Reset", "Recorder.Unguarded", "Recorder.UnguardedThroughCore",
                "Stale exemption: Recorder.Gone",
            },
            Inspect(sources, rule).Order(StringComparer.Ordinal));
    }

    private static List<string> Sources([CallerFilePath] string path = "")
    {
        var application = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", "MicroLIMS.Application"));
        return new[] { "Workflows", "Services" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(application, folder), "*.cs", SearchOption.AllDirectories))
            .Select(File.ReadAllText).ToList();
    }

    private static List<string> Inspect(IEnumerable<string> sources, RecordingRule rule)
    {
        var methods = ReadMethods(sources, out var bases);
        var methodLookup = methods.ToLookup(method => (method.Owner, method.Name));
        var writes = methods.Where(method => Write.IsMatch(method.Body)).ToHashSet();
        var satisfied = methods.Where(method => rule.Satisfied.IsMatch(method.Body)).ToHashSet();
        var edges = methods.ToDictionary(method => method, method =>
        {
            var names = Call.Matches(method.Body).Select(match => match.Groups["name"].Value).Distinct();
            var inherited = bases.GetValueOrDefault(method.Owner);
            return names.SelectMany(name => methodLookup[(method.Owner, name)]
                .Concat(inherited is null ? [] : methodLookup[(inherited, name)])).ToList();
        });

        Spread(writes, edges);
        Spread(satisfied, edges);

        var recordingPoints = methods.Where(method => method.IsPublic && writes.Contains(method)).ToList();
        var findings = recordingPoints.Where(method => !satisfied.Contains(method) && !rule.Exempt.ContainsKey(method.Key))
            .Select(method => method.Key).Distinct().ToList();
        foreach (var (key, reason) in rule.Exempt)
        {
            if (!methods.Any(method => method.IsPublic && method.Key == key))
                findings.Add($"Stale exemption: {key}");
            if (reason.Trim().Length < 20)
                findings.Add($"Exemption requires a written reason: {key}");
        }
        return findings;
    }

    private static void Spread(HashSet<Method> matches, Dictionary<Method, List<Method>> edges)
    {
        int previous;
        do
        {
            previous = matches.Count;
            foreach (var (method, called) in edges)
                if (called.Any(matches.Contains))
                    matches.Add(method);
        } while (matches.Count != previous);
    }

    private static List<Method> ReadMethods(IEnumerable<string> sources, out Dictionary<string, string> bases)
    {
        bases = new(StringComparer.Ordinal);
        var methods = new List<Method>();
        foreach (var source in sources)
        {
            var code = BlankNonCode(source);
            foreach (Match type in Class.Matches(code))
            {
                var owner = type.Groups["name"].Value;
                if (type.Groups["base"].Success)
                    bases[owner] = type.Groups["base"].Value;
                var start = type.Index + type.Length;
                var end = EndOfBody(code, start, 1, '}');
                var nextMember = start;
                foreach (Match header in Header.Matches(code, start))
                {
                    if (header.Index >= end)
                        break;
                    if (header.Index < nextMember)
                        continue;
                    var bodyStart = header.Index + header.Length;
                    var block = header.Groups["body"].Value == "{";
                    nextMember = EndOfBody(code, bodyStart, block ? 1 : 0, block ? '}' : ';') + 1;
                    methods.Add(new(owner, header.Groups["name"].Value, header.Groups["access"].Value == "public",
                        code[bodyStart..(nextMember - 1)]));
                }
            }
        }
        return methods;
    }

    private static int EndOfBody(string code, int start, int braces, char terminator)
    {
        for (var i = start; i < code.Length; i++)
        {
            if (code[i] == '{')
                braces++;
            if (code[i] == '}')
                braces--;
            if (braces == 0 && code[i] == terminator)
                return i;
        }
        return code.Length;
    }

    private static string BlankNonCode(string source)
    {
        var code = source.ToCharArray();
        for (var i = 0; i < source.Length;)
        {
            var end = NonCodeEnd(source, i);
            if (end == i)
            {
                i++;
                continue;
            }
            for (; i < end; i++)
                if (code[i] is not ('\r' or '\n'))
                    code[i] = ' ';
        }
        return new string(code);
    }

    // Return the exclusive end of a comment or literal, or start for code.
    private static int NonCodeEnd(string source, int start)
    {
        if (source.AsSpan(start).StartsWith("//"))
        {
            var newline = source.IndexOf('\n', start + 2);
            return newline < 0 ? source.Length : newline;
        }
        if (source.AsSpan(start).StartsWith("/*"))
            return DelimiterEnd(source, start + 2, "*/");

        var quote = start;
        var interpolated = false;
        var verbatim = false;
        while (quote < source.Length && source[quote] is '$' or '@')
        {
            interpolated |= source[quote] == '$';
            verbatim |= source[quote++] == '@';
        }
        if (quote >= source.Length || source[quote] is not ('"' or '\''))
            return start;
        var delimiter = source[quote];
        var count = 1;
        while (quote + count < source.Length && source[quote + count] == '"')
            count++;
        if (delimiter == '"' && count >= 3 && !verbatim)
            return DelimiterEnd(source, quote + count, new string('"', count));

        for (var i = quote + 1; i < source.Length; i++)
        {
            if (!verbatim && source[i] == '\\')
            {
                i++;
                continue;
            }
            if (source[i] == delimiter)
            {
                if (!verbatim || i + 1 == source.Length || source[i + 1] != delimiter)
                    return i + 1;
                i++;
            }
            else if (interpolated && source[i] == '{')
            {
                i = i + 1 < source.Length && source[i + 1] == '{'
                    ? i + 1
                    : EndInterpolation(source, i + 1);
            }
        }
        return source.Length;
    }

    private static int EndInterpolation(string source, int start)
    {
        var depth = 1;
        for (var i = start; i < source.Length; i++)
        {
            var end = NonCodeEnd(source, i);
            if (end > i)
            {
                i = end - 1;
                continue;
            }
            if (source[i] == '{')
                depth++;
            if (source[i] == '}' && --depth == 0)
                return i;
        }
        return source.Length;
    }

    private static int DelimiterEnd(string source, int start, string delimiter)
    {
        var closing = source.IndexOf(delimiter, start, StringComparison.Ordinal);
        return closing < 0 ? source.Length : closing + delimiter.Length;
    }
}
