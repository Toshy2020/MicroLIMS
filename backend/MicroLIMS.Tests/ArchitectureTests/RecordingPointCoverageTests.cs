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
        ["AfterCleaningWorkflowEngine.PrepareAsync"] = "Creates empty sampling locations for the batch; no result or reading is recorded.",
        ["EMWorkflowEngine.PrepareAsync"] = "Creates empty sampling locations for the batch; no result or reading is recorded.",
        ["WaterWorkflowEngine.PrepareAsync"] = "Creates empty sampling locations for the batch; no result or reading is recorded.",
        ["RetestSampleFactory.AddRetestSampleAsync"] = "Clones empty sampling locations for future testing without recording any result or reading.",
        ["SampleApprovalService.DecideAsync"] = "A review decision; the retest clones fresh unread locations without any recorded value.",
        ["SampleCorrectionService.CorrectAsync"] = "Removes the locations of removed tests that have no entered result; it records nothing.",
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
        ["OosInvestigationService.OpenForReviewedSectionAsync"] = "Opens an OOS investigation from a signed review and links existing results; it records no test result (reviewer act, D1 Task 3).",
        ["OosInvestigationService.AddAnalystStatementAsync"] = "Appends an investigation statement; RecordedByUserId attributes an investigation entry, guarded by Oos.Contribute and a performer check.",
        ["OosInvestigationService.AddEntryAsync"] = "Attributes an investigation entry; hypothesis work is investigation evidence and does not record a reportable result.",
        ["OosInvestigationService.OpenManuallyAsync"] = "Opens a scoped investigation for an already reviewed failing order and links the existing result without changing it.",
        ["OosInvestigationService.AnswerChecklistAsync"] = "Appends an investigation checklist answer; RecordedByUserId attributes an investigation entry, guarded by Oos.Contribute.",
    };

    // Every competency exemption still applies to the SOP revision capture: a method that records no
    // laboratory result, reading or observation has no first step to capture a revision for.
    private static readonly Dictionary<string, string> SopCaptureExempt = new(CompetencyExempt, StringComparer.Ordinal);

    private const string Entities = "ParameterResult|CountTestReading|WorkflowStepResult|PathogenObservation|" +
        "ConfirmatoryPlateObservation|LocationPathogenObservation|TestAnalysis|Result|SampleLocation";
    private const string Sets = "ParameterResults|CountTestReadings|WorkflowStepResults|PathogenObservations|" +
        "ConfirmatoryPlateObservations|LocationPathogenObservations|TestAnalyses|Results|SampleLocations";
    private const string Mutators = "Add(?:Range)?|Update(?:Range)?|Attach(?:Range)?|Remove(?:Range)?";
    private static readonly Regex Write = new(
        $@"\bnew\s+(?:{Entities})\s*[({{]|\b_db\s*\.\s*Add(?:Range)?\s*\(" +
        $@"|\.\s*(?:{Sets}|Set\s*<\s*(?:{Entities})\s*>\s*\(\s*\))\s*\.\s*(?:{Mutators})\s*\(" +
        $@"|\.\s*(?:{Sets}|Set\s*<\s*(?:{Entities})\s*>\s*\(\s*\))\s*\.[^;]*?\bExecute(?:Update|Delete)(?:Async)?\s*\(" +
        @"|\b(?:CFUResult|CalculatedResult|ReportedResult|EnteredByUserId|SubmittedByUserId|ObservedByUserId|RecordedByUserId|AnalystDecisionByUserId|InvalidatedByUserId)\s*(?:\?\?=|=(?!=|>))");
    private static readonly Regex Competency = new(@"\b_competency\b");
    private static readonly Regex SopCapture = new(@"\b_sopCapture\b");
    private static readonly Regex DirectGuard = new(
        @"\b_competency\s*\.\s*(?:EvaluateAsync|EvaluateManyAsync|EvaluateAssigneeAsync|FilterAsync)\s*\(|\bRequireCompetentAsync\s*\(");
    private static readonly Regex DirectCapture = new(@"\b(?:CaptureSopRevisionAsync|_sopCapture\s*\.\s*CaptureIfNeededAsync)\s*\(");
    private static readonly Regex Class = new(
        @"\bclass\s+(?<name>\w+)(?:\s*\([^{}]*?\))?\s*(?::\s*(?<base>\w+)[^{]*)?\s*\{");
    private static readonly Regex Header = new(
        @"(?m)^[ \t]*(?:(?<access>public|protected|internal|private)\s+)?(?:(?:async|static|sealed|override|virtual|new|partial)\s+)*" +
        @"[\w<>,.?\[\]() \t]+?\s+(?<name>\w+)(?:<[^>{}]+>)?\s*\((?:[^(){}]|\([^(){}]*\))*\)\s*" +
        @"(?:where\s+\w+\s*:\s*[^{};=]+?\s*)?(?<body>\{|=>)");
    private static readonly Regex Call = new(@"(?<![\w.])(?:(?<receiver>\w+)\s*\.\s*)?(?<name>\w+)\s*(?:<[^>{};]+>)?\s*\(");

    private sealed record RecordingRule(string Name, Regex Write, Regex Satisfied, IReadOnlyDictionary<string, string> Exempt);
    private sealed record Method(string Owner, string Name, bool IsPublic, string Body)
    {
        public string Key => $"{Owner}.{Name}";
    }

    private static readonly RecordingRule[] Rules =
    [
        new("Competency", Write, Competency, CompetencyExempt),
        new("SopCapture", Write, SopCapture, SopCaptureExempt),
    ];
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
    public void Sop_capture_exemptions_mirror_the_competency_exemptions()
    {
        Assert.Equal(CompetencyExempt.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            SopCaptureExempt.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    // 1:1 pairing at the call site: a method that itself calls the competency guard must itself call the
    // SOP revision capture (the guard helper RequireCompetentAsync only evaluates; it never captures).
    [Fact]
    public void Every_competency_guarded_recording_point_is_paired_with_sop_capture()
    {
        var findings = UnpairedGuards(Sources());
        Assert.True(findings.Count == 0, string.Join(Environment.NewLine, findings));
    }

    [Fact]
    public void Pairing_scan_flags_a_guard_without_a_capture_and_accepts_a_pair()
    {
        string[] sources =
        [
            """
            public class Recorder : Support
            {
                public async Task Paired() { using var r = await RequireCompetentAsync(1, 2, ct); await CaptureSopRevisionAsync(order, ct); }
                public async Task Filtered() { var c = await _competency.FilterAsync(1, orders, ct); await _sopCapture.CaptureIfNeededAsync(o, ct); }
                public async Task Unpaired() { using var r = await RequireCompetentAsync(1, 2, ct); }
                public async Task UnpairedFilter() { var c = await _competency.FilterAsync(1, orders, ct); }
                public async Task Mentioned() { using var r = await RequireCompetentAsync(1, 2, ct); Log("CaptureSopRevisionAsync("); }
                protected async Task<IDisposable> RequireCompetentAsync(int u, int o, CancellationToken ct) { await _competency.EvaluateAsync(u, o, ct); return null; }
            }
            """,
        ];
        Assert.Equal(new[] { "Recorder.Mentioned", "Recorder.Unpaired", "Recorder.UnpairedFilter" },
            UnpairedGuards(sources).Order(StringComparer.Ordinal));
    }

    // The capture must come before the method's first write, so a step that goes on to write is never recorded
    // without it and a refusal after a write cannot leave a capture behind. Checked in the method that makes the call.
    [Fact]
    public void Sop_capture_precedes_the_first_write_in_every_method_that_makes_both()
    {
        var findings = CaptureAfterWrite(Sources());
        Assert.True(findings.Count == 0, string.Join(Environment.NewLine, findings));
    }

    [Fact]
    public void Order_scan_flags_a_capture_after_a_write()
    {
        string[] sources =
        [
            """
            public class Recorder : Support
            {
                public async Task Before() { await CaptureSopRevisionAsync(order, ct); _db.Results.Add(result); }
                public async Task After() { _db.Results.Add(result); await CaptureSopRevisionAsync(order, ct); }
                public async Task OnlyCapture() { await CaptureSopRevisionAsync(order, ct); }
            }
            """,
        ];
        Assert.Equal(new[] { "Recorder.After" }, CaptureAfterWrite(sources));
    }

    private static List<string> CaptureAfterWrite(IEnumerable<string> sources) =>
        ReadMethods(sources, out _)
            .Where(method => DirectCapture.Match(method.Body) is { Success: true } capture
                && Write.Match(method.Body) is { Success: true } write && write.Index < capture.Index)
            .Select(method => method.Key).Distinct().ToList();

    private static List<string> UnpairedGuards(IEnumerable<string> sources) =>
        ReadMethods(sources, out _)
            .Where(method => method.Name != "RequireCompetentAsync" && DirectGuard.IsMatch(method.Body) && !DirectCapture.IsMatch(method.Body))
            .Select(method => method.Key).Distinct().ToList();

    [Fact]
    public void Facade_never_calls_the_competency_guard()
    {
        var methods = ReadMethods(Sources(), out _).Where(method => method.Owner == "TestWorkflowEngine").ToList();
        Assert.NotEmpty(methods);
        Assert.DoesNotContain(methods, method => Competency.IsMatch(method.Body) ||
            Regex.IsMatch(method.Body, @"\b(?:EvaluateAsync|EvaluateManyAsync|EvaluateAssigneeAsync|EnsureAllowedAsync|RequireCompetentAsync|FilterAsync)\s*\("));
    }

    [Fact]
    public void Scan_finds_additional_write_shapes_and_qualified_calls()
    {
        string[] sources =
        [
            """
            public class Recorder : Support
            {
                public void Location() => _db.SampleLocations.Update(location);
                public void BulkLocation() => _db.SampleLocations.Where(l => l.Id == id).ExecuteUpdate(p => p.SetProperty(l => l.Unit, unit));
                public void ContextAdd() => _db.Add(location);
                public void ContextAddRange() => _db.AddRange(locations);
                public void GenericSet() => _db.Set<SampleLocation>().Remove(location);
                public void Receiver() => other.Write();
                void Write() => _db.Results.Add(result);
                public void BaseReceiver() => other.InheritedWrite();
                public void Generic<T>() where T : class, new() => _db.Results.Add(result);
            }
            public class Support
            {
                protected void InheritedWrite() => _db.Results.Add(result);
            }
            """,
        ];
        Assert.Equal(new[]
        {
            "Recorder.BaseReceiver", "Recorder.BulkLocation", "Recorder.ContextAdd", "Recorder.ContextAddRange",
            "Recorder.Generic", "Recorder.GenericSet", "Recorder.Location", "Recorder.Receiver",
        }, Inspect(sources, Rules[0] with { Exempt = new Dictionary<string, string>() }).Order(StringComparer.Ordinal));
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
        var rule = new RecordingRule("Competency", Write, Competency, new Dictionary<string, string>
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

    [Fact]
    public void Sop_capture_rule_accepts_the_helper_and_flags_a_recording_point_that_never_captures()
    {
        string[] sources =
        [
            """
            public class Recorder : Support
            {
                public Task Captures() => UnitOfWork.RunAsync(_db, () => CapturesCore());
                private async Task CapturesCore() { await CaptureSopRevisionAsync(order, ct); _db.Results.Add(result); }
                public Task Misses() => UnitOfWork.RunAsync(_db, () => MissesCore());
                private async Task MissesCore() { using var r = await RequireCompetentAsync(1, 2, ct); _db.Results.Add(result); }
                public void Exempt() => _db.Results.Add(result);
                public string Mentions() { _db.Results.Add(result); return "_sopCapture"; }
            }
            public class Support
            {
                protected Task CaptureSopRevisionAsync(TestOrder order, CancellationToken ct) => _sopCapture.CaptureIfNeededAsync(order, ct);
            }
            """,
        ];
        var rule = new RecordingRule("SopCapture", Write, SopCapture, new Dictionary<string, string>
        {
            ["Recorder.Exempt"] = "An explicitly justified exemption.",
        });
        Assert.Equal(new[] { "Recorder.Mentions", "Recorder.Misses" }, Inspect(sources, rule).Order(StringComparer.Ordinal));
    }

    private static List<string> Sources([CallerFilePath] string path = "")
    {
        var application = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", "MicroLIMS.Application"));
        return new[] { "Workflows", "Services", "Helpers" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(application, folder), "*.cs", SearchOption.AllDirectories))
            .Select(File.ReadAllText).ToList();
    }

    private static List<string> Inspect(IEnumerable<string> sources, RecordingRule rule)
    {
        var methods = ReadMethods(sources, out var bases);
        var methodLookup = methods.ToLookup(method => (method.Owner, method.Name));
        var writes = methods.Where(method => rule.Write.IsMatch(method.Body)).ToHashSet();
        var satisfied = methods.Where(method => rule.Satisfied.IsMatch(method.Body)).ToHashSet();
        var edges = methods.ToDictionary(method => method, method =>
        {
            return Call.Matches(method.Body).SelectMany(match =>
            {
                var receiver = match.Groups["receiver"].Value;
                var name = match.Groups["name"].Value;
                var inherited = bases.GetValueOrDefault(method.Owner);
                var candidates = (receiver == "base" ? [] : methodLookup[(method.Owner, name)])
                    .Concat(inherited is null ? [] : methodLookup[(inherited, name)]).ToList();
                return receiver is "" or "this" or "base" || candidates.Count == 1 ? candidates : [];
            }).Distinct().ToList();
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
                var depth = 0;
                foreach (Match header in Header.Matches(code, start))
                {
                    if (header.Index >= end)
                        break;
                    if (header.Index < nextMember)
                        continue;
                    for (var i = nextMember; i < header.Index; i++)
                        depth += code[i] == '{' ? 1 : code[i] == '}' ? -1 : 0;
                    nextMember = header.Index;
                    if (depth != 0)
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
