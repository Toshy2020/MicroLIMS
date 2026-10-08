using System.Globalization;
using System.Text.RegularExpressions;

namespace MicroLIMS.Application.Services.InstrumentImport;

public enum EmpowerField
{
    SampleName,
    SampleType,
    Vial,
    InjectionNo,
    InjectionVolume,
    RunTime,
    DateAcquired,
    DateProcessed,
    AcquiredBy,
    SampleSetName,
    AcqMethodSet,
    ProcessingMethod,
    ChannelName,
    ProcChannelDescription,
    ReportedBy,
    ReportMethod,
    ReportMethodId
}

// Raw: the value as read. CrossCheckMismatch: the second OCR pass read it
// differently - the analyst must look at this one.
public sealed record ExtractedField(string Raw, double? Confidence, bool CrossCheckMismatch);

public sealed record EmpowerPeak(int PeakNo, decimal RetentionTime, decimal Area, decimal Height, double? Confidence);

public sealed class EmpowerReport
{
    public ReportTextSource Source { get; init; }
    public IReadOnlyDictionary<EmpowerField, ExtractedField> Fields { get; init; } = new Dictionary<EmpowerField, ExtractedField>();
    public IReadOnlyList<EmpowerPeak> Peaks { get; init; } = [];
    public IReadOnlyList<string> Problems { get; init; } = [];

    // OCR values are never trusted unchecked (ALCOA+ accuracy).
    public bool RequiresVerification => Source == ReportTextSource.Ocr || Problems.Count > 0;

    public string? Text(EmpowerField f) => Fields.TryGetValue(f, out var v) ? v.Raw : null;
    public int? Number(EmpowerField f) => EmpowerReportParser.FirstInt(Text(f));
    public DateTime? Date(EmpowerField f) => EmpowerReportParser.ParseDate(Text(f));

    public string? SampleName => Text(EmpowerField.SampleName);
    public string? SampleType => Text(EmpowerField.SampleType);
    public int? Vial => Number(EmpowerField.Vial);
    public int? InjectionNo => Number(EmpowerField.InjectionNo);
    public string? SampleSetName => Text(EmpowerField.SampleSetName);
    public string? ProcessingMethod => Text(EmpowerField.ProcessingMethod);
    public DateTime? DateAcquired => Date(EmpowerField.DateAcquired);
    public DateTime? DateProcessed => Date(EmpowerField.DateProcessed);
    public int? ReportMethodId => Number(EmpowerField.ReportMethodId);
}

// Reads a Waters Empower 3 single-injection report ("SAMPLE INFORMATION"
// header + RT/Area/Height peak table) from lines of text, whether they came
// from a PDF text layer or OCR. Labels are matched loosely because OCR
// drops spaces and misreads punctuation ("Sample SetName", "Proc. Chni.").
public static class EmpowerReportParser
{
    private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Order matters only for readability; overlapping matches keep the longest.
    private static readonly (EmpowerField Field, Regex Label)[] Labels =
    [
        (EmpowerField.SampleName, new(@"Sample\s*Name\s*[:;.,]?", Opt)),
        (EmpowerField.SampleType, new(@"Sample\s*Type\s*[:;.,]?", Opt)),
        (EmpowerField.Vial, new(@"\bV[il1]al\s*[:;.,]?", Opt)),
        (EmpowerField.InjectionNo, new(@"Injection\s*#\s*[:;.,]?", Opt)),
        (EmpowerField.InjectionVolume, new(@"Injection\s*Volume\s*[:;.,]?", Opt)),
        (EmpowerField.RunTime, new(@"Run\s*Time\s*[:;.,]?", Opt)),
        (EmpowerField.DateAcquired, new(@"Date\s*Acquired\s*[:;.,]?", Opt)),
        (EmpowerField.DateProcessed, new(@"Date\s*Processed\s*[:;.,]?", Opt)),
        (EmpowerField.AcquiredBy, new(@"Acquired\s*By\s*[:;.,]?", Opt)),
        (EmpowerField.SampleSetName, new(@"Sample\s*Set\s*Name\s*[:;.,]?", Opt)),
        (EmpowerField.AcqMethodSet, new(@"Acq\.?\s*Method\s*Set\s*[:;.,]?", Opt)),
        (EmpowerField.ProcessingMethod, new(@"Processing\s*M\w{4,6}\s*[:;.,]?", Opt)),
        (EmpowerField.ChannelName, new(@"Channel\s*Name\s*[:;.,]?", Opt)),
        (EmpowerField.ProcChannelDescription, new(@"Proc\.?\s*Ch\w*\.?\s*De\w*\.?\s*[:;.,]*", Opt)),
        (EmpowerField.ReportedBy, new(@"Reported\s*by\s*User\s*[:;.,]?", Opt)),
        (EmpowerField.ReportMethodId, new(@"Report\s*M\w{2,4}od\s*[:;.,]?\s*[I1l|(\[]\s*D\b\s*[:;.,]?", Opt)),
        (EmpowerField.ReportMethod, new(@"Report\s*M\w{2,4}od\s*[:;.,]", Opt)),
        // Not read, but they end the value before them on a shared line.
        ((EmpowerField)(-1), new(@"Project\s*Name\s*[:;.,’']?", Opt)),
        ((EmpowerField)(-1), new(@"Date\s*Printed\s*[:;.,’']?", Opt)),
        ((EmpowerField)(-1), new(@"Page\s*[:;.,]\s*\d+\s*of\s*\d+", Opt)),
    ];

    private static readonly EmpowerField[] Required =
    [
        EmpowerField.SampleName, EmpowerField.SampleType, EmpowerField.InjectionNo,
        EmpowerField.SampleSetName, EmpowerField.ProcessingMethod,
        EmpowerField.DateAcquired, EmpowerField.ReportMethodId
    ];

    private static readonly Regex PeakHeader = new(@"\bArea\b|\bHeight\b", Opt);
    private static readonly Regex PeakRow = new(
        @"^\W*(?:\d{1,3}\W+)?(\d+[.,]\d+)\W+(\d+(?:[.,]\d+)?)\W+(\d+(?:[.,]\d+)?)\W*$", Opt);
    private static readonly Regex DateRx = new(
        @"(\d{1,2})/(\d{1,2})/(\d{4})\s*(\d{1,2}):(\d{2}):(\d{2})\s*([AP])\.?\s*M", Opt);

    public static EmpowerReport Parse(ReportText text)
    {
        var fields = ReadFields(text.Lines);
        var cross = ReadFields(text.CrossCheckLines);
        var problems = new List<string>(text.ReaderProblems ?? []);

        var result = new Dictionary<EmpowerField, ExtractedField>();
        foreach (var (field, (raw, conf)) in fields)
        {
            var mismatch = cross.TryGetValue(field, out var other) && Normalize(other.Raw) != Normalize(raw);
            if (mismatch)
                problems.Add($"{field}: read as '{raw}' and '{other.Raw}' - check against the report.");
            result[field] = new ExtractedField(raw, conf, mismatch);
        }

        foreach (var f in Required.Where(f => !result.ContainsKey(f)))
            problems.Add($"{f}: not found on the report.");
        foreach (var f in new[] { EmpowerField.DateAcquired, EmpowerField.DateProcessed })
            if (result.TryGetValue(f, out var d) && ParseDate(d.Raw) is null)
                problems.Add($"{f}: '{d.Raw}' is not a date.");
        foreach (var f in new[] { EmpowerField.InjectionNo, EmpowerField.Vial, EmpowerField.ReportMethodId })
            if (result.TryGetValue(f, out var n) && FirstInt(n.Raw) is null)
                problems.Add($"{f}: '{n.Raw}' is not a number.");

        var peaks = ReadPeaks(text.Lines);
        if (peaks.Count == 0)
            problems.Add("Peak table: no peak rows found.");
        // A table border fused onto a digit turns 3.290 into 113.290.
        if (result.TryGetValue(EmpowerField.RunTime, out var run) && FirstDecimal(run.Raw) is { } runTime)
            foreach (var p in peaks.Where(p => p.RetentionTime > runTime))
                problems.Add($"Peak {p.PeakNo}: RT {p.RetentionTime} is past the {runTime} min run time - check the peak table.");

        return new EmpowerReport { Source = text.Source, Fields = result, Peaks = peaks, Problems = problems };
    }

    private static Dictionary<EmpowerField, (string Raw, double? Conf)> ReadFields(IReadOnlyList<ReportLine> lines)
    {
        var found = new Dictionary<EmpowerField, (string, double?)>();
        foreach (var line in lines)
        {
            var hits = FindLabels(line.Text);
            for (var i = 0; i < hits.Count; i++)
            {
                var (field, m) = hits[i];
                if ((int)field < 0 || found.ContainsKey(field)) continue;
                var end = i + 1 < hits.Count ? hits[i + 1].Match.Index : line.Text.Length;
                var value = Clean(line.Text[(m.Index + m.Length)..end]);
                if (value.Length > 0) found[field] = (value, line.Confidence);
            }
        }
        return found;
    }

    private static List<(EmpowerField Field, Match Match)> FindLabels(string line)
    {
        var all = Labels
            .SelectMany(l => l.Label.Matches(line).Select(m => (l.Field, Match: m)))
            .OrderBy(h => h.Match.Index).ThenByDescending(h => h.Match.Length)
            .ToList();
        var kept = new List<(EmpowerField, Match)>();
        var coveredTo = -1;
        foreach (var h in all)
        {
            if (h.Match.Index < coveredTo) continue;
            kept.Add(h);
            coveredTo = h.Match.Index + h.Match.Length;
        }
        return kept;
    }

    private static List<EmpowerPeak> ReadPeaks(IReadOnlyList<ReportLine> lines)
    {
        var peaks = new List<EmpowerPeak>();
        var inTable = false;
        foreach (var line in lines)
        {
            if (!inTable) { inTable = PeakHeader.IsMatch(line.Text); continue; }
            if (FindLabels(line.Text).Count > 0) break;
            var m = PeakRow.Match(line.Text.Trim());
            if (!m.Success) continue;
            peaks.Add(new EmpowerPeak(
                peaks.Count + 1, // the printed row number; OCR misreads it and it carries nothing else
                Dec(m.Groups[1].Value), Dec(m.Groups[2].Value), Dec(m.Groups[3].Value),
                line.Confidence));
        }
        return peaks;
    }

    // ponytail: Empower here prints M/d/yyyy (US) - a site set to d/M/yyyy
    // needs this as a per-report-method setting. Kind stays Unspecified: the
    // instrument's local time, not converted.
    internal static DateTime? ParseDate(string? raw)
    {
        if (raw is null) return null;
        var m = DateRx.Match(raw);
        if (!m.Success) return null;
        int G(int i) => int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
        var hour = G(4) % 12 + (m.Groups[7].Value.Equals("P", StringComparison.OrdinalIgnoreCase) ? 12 : 0);
        try { return new DateTime(G(3), G(1), G(2), hour, G(5), G(6)); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    internal static int? FirstInt(string? raw) =>
        raw is not null && Regex.Match(raw, @"\d+") is { Success: true } m && int.TryParse(m.Value, out var n) ? n : null;

    private static decimal? FirstDecimal(string raw) =>
        Regex.Match(raw, @"\d+(?:[.,]\d+)?") is { Success: true } m ? Dec(m.Value) : null;

    private static decimal Dec(string s) => decimal.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

    private static string Clean(string s) =>
        Regex.Replace(s.Trim(' ', '\t', ':', ';', ',', '|', '’', '\'', '‘'), @"\s+", " ");

    private static string Normalize(string s) =>
        Regex.Replace(s, @"[^0-9A-Za-z]", "").ToUpperInvariant();
}
