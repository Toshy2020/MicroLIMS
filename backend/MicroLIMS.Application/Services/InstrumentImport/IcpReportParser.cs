using System.Globalization;
using System.Text.RegularExpressions;

namespace MicroLIMS.Application.Services.InstrumentImport;

public enum IcpSolutionKind
{
    Blank,      // no values printed
    Standard,   // values in brackets: the defined concentration, not a measurement
    Sample      // measured concentrations
}

// One element line: "Fe 238.204 0.816 mg/L". Value is null on a blank;
// Defined marks a bracketed standard value ("[0.5] mg/L").
public sealed record IcpReading(string Symbol, decimal WavelengthNm, decimal? Value, string? Unit, bool Defined);

// Label: the Sample ID line ("26032059 T Mg", "Zn,Fe3", "std1"); null when
// the report printed readings with no Sample ID line above them.
public sealed record IcpSolution(string? Label, IcpSolutionKind Kind, IReadOnlyList<IcpReading> Readings, int Page);

public sealed class IcpReport
{
    public DateOnly? ReportDate { get; init; }
    public string? ReportTime { get; init; } // as printed; its AM/PM marker prints as "?"
    public string? Title { get; init; }
    public IReadOnlyList<IcpSolution> Solutions { get; init; } = [];
    public IReadOnlyList<string> Problems { get; init; } = [];
    public bool RequiresVerification { get; init; }
}

// Reads the ICP-OES "SampleID / Analyte / Mean" report: each solution is a
// Sample ID line closed by a dashed rule, then one line per element.
public static class IcpReportParser
{
    private static readonly Regex PageHeader = new(@"^(\d{2}/\d{2}/\d{4})\s+(\d{1,2}:\d{2}:\d{2})");
    private static readonly Regex ColumnHeader = new(@"^SampleID\b", RegexOptions.IgnoreCase);
    private static readonly Regex SolutionHeader = new(@"^(?<label>.*?)\s*-{10,}\s*$");
    private static readonly Regex ReadingLine = new(
        @"^(?<sym>[A-Z][a-z]?)\s+(?<wl>\d{3}\.\d{1,3})(?:\s+(?<val>\[\s*-?\d+(?:\.\d+)?\s*\]|-?\d+(?:\.\d+)?)\s*(?<unit>\S+)?)?$");
    private static readonly Regex PageNumber = new(@"^\d{1,3}$");
    // "26032059 T Zn.Fe", "Zn,Fe3", "Cu,Mn" -> the elements the solution was prepared for.
    private static readonly Regex ElementGroup = new(@"(?:^|\s)((?:[A-Z][a-zA-Z]?[,.])*[A-Z][a-zA-Z]?)\d*$");

    public static IcpReport Parse(ReportText text)
    {
        var problems = new List<string>(text.ReaderProblems ?? []);
        DateOnly? date = null;
        string? time = null, title = null;
        var solutions = new List<IcpSolution>();
        string? label = null;
        var readings = new List<IcpReading>();
        var page = 0;
        var inBody = false;

        void Close()
        {
            if (label is null && readings.Count == 0) return;
            solutions.Add(new IcpSolution(label, KindOf(label, readings, problems), readings, page));
            label = null;
            readings = [];
        }

        foreach (var raw in text.Lines)
        {
            var line = raw.Text.Trim();
            if (line.Length == 0) continue;

            if (PageHeader.Match(line) is { Success: true } ph)
            {
                page++;
                if (date is null && DateOnly.TryParseExact(ph.Groups[1].Value, "dd/MM/yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    date = d;
                time ??= ph.Groups[2].Value;
                continue;
            }
            if (ColumnHeader.IsMatch(line)) { inBody = true; continue; }
            if (!inBody) { title ??= line; continue; } // run title, above the first column header
            if (SolutionHeader.Match(line) is { Success: true } sh)
            {
                Close();
                label = sh.Groups["label"].Value.Trim();
                continue;
            }
            if (ReadingLine.Match(line) is { Success: true } m)
            {
                var val = m.Groups["val"];
                var reading = new IcpReading(
                    m.Groups["sym"].Value,
                    decimal.Parse(m.Groups["wl"].Value, CultureInfo.InvariantCulture),
                    val.Success ? decimal.Parse(val.Value.Trim('[', ']', ' '), CultureInfo.InvariantCulture) : null,
                    m.Groups["unit"].Success ? m.Groups["unit"].Value : null,
                    val.Success && val.Value.StartsWith('['));

                // An element already read in this solution: the report printed
                // another solution without a Sample ID line.
                if (readings.Any(x => x.Symbol == reading.Symbol))
                {
                    var after = label ?? solutions.LastOrDefault()?.Label ?? "the first solution";
                    Close();
                    problems.Add($"Page {page}: {reading.Symbol} read again after '{after}' with no Sample ID line - " +
                        "the readings that follow are kept unlabelled; match them to a sample by hand.");
                }
                readings.Add(reading);
                continue;
            }
            if (PageNumber.IsMatch(line)) continue;
            problems.Add($"Page {page}: line not understood: '{line}'.");
        }
        Close();

        foreach (var s in solutions.Where(s => s.Label is not null))
        {
            if (s.Kind == IcpSolutionKind.Sample && DeclaredElements(s.Label!) is { } declared)
                foreach (var r in s.Readings.Where(r => !declared.Contains(r.Symbol, StringComparer.OrdinalIgnoreCase)))
                    problems.Add($"'{s.Label}': a {r.Symbol} reading under a Sample ID for {string.Join(",", declared)} - check which solution it belongs to.");
            foreach (var r in s.Readings.Where(r => r.Value is not null && r.Unit != "mg/L"))
                problems.Add($"'{s.Label}': {r.Symbol} is in '{r.Unit}', not mg/L.");
        }
        foreach (var dup in solutions.Where(s => s.Label is not null && s.Kind == IcpSolutionKind.Sample)
                     .GroupBy(s => s.Label!, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            problems.Add($"'{dup.Key}' was measured {dup.Count()} times - choose which reading to use.");
        if (date is null) problems.Add("Report date not found.");
        if (!solutions.Any(s => s.Kind == IcpSolutionKind.Sample)) problems.Add("No sample readings found.");

        return new IcpReport
        {
            ReportDate = date,
            ReportTime = time,
            Title = title,
            Solutions = solutions,
            Problems = problems,
            RequiresVerification = text.Source == ReportTextSource.Ocr || problems.Count > 0
        };
    }

    private static IcpSolutionKind KindOf(string? label, List<IcpReading> readings, List<string> problems)
    {
        if (readings.All(r => r.Value is null)) return IcpSolutionKind.Blank;
        if (readings.All(r => r.Defined)) return IcpSolutionKind.Standard;
        if (readings.Any(r => r.Defined || r.Value is null))
            problems.Add($"'{label}': mixes measured values with blank or bracketed ones.");
        return IcpSolutionKind.Sample;
    }

    // The element symbols named at the end of a Sample ID, or null if none.
    private static string[]? DeclaredElements(string label)
    {
        var m = ElementGroup.Match(label);
        if (!m.Success) return null;
        var symbols = m.Groups[1].Value.Split(',', '.');
        return symbols.All(s => Elements.Contains(s)) ? symbols : null;
    }

    private static readonly HashSet<string> Elements = new(
        ("H He Li Be B C N O F Ne Na Mg Al Si P S Cl Ar K Ca Sc Ti V Cr Mn Fe Co Ni Cu Zn Ga Ge As Se Br Kr " +
         "Rb Sr Y Zr Nb Mo Tc Ru Rh Pd Ag Cd In Sn Sb Te I Xe Cs Ba La Ce Pr Nd Pm Sm Eu Gd Tb Dy Ho Er Tm Yb " +
         "Lu Hf Ta W Re Os Ir Pt Au Hg Tl Pb Bi Po At Rn Fr Ra Ac Th Pa U Np Pu").Split(' '),
        StringComparer.OrdinalIgnoreCase);
}
