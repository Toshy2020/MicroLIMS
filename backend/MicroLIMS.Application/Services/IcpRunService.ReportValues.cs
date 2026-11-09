using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Application.Services.InstrumentImport;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// One solution of the instrument report that could be this sample (Index is its position in the report).
// MatchesSample: its Sample ID contains the sample's number. Elements: the symbols it has readings for.
public record IcpReportSolutionDto(int Index, string Label, int Page, bool MatchesSample, string Elements);

// What the report says for one element the test needs. Note explains a value the analyst must look at.
public record IcpReportElementValueDto(int IcpTestElementId, string Symbol, decimal? ResultPpm, decimal? WavelengthNm, string? Note);

public record IcpReportValuesDto(
    int EvidenceId, string FileName, string Source, bool RequiresVerification, List<string> Problems,
    List<IcpReportSolutionDto> Solutions, int? MatchedIndex, int? SelectedIndex,
    List<IcpReportElementValueDto> Elements);

// Auto-fill: proposes the sample's results from its uploaded instrument report. Read-only - the analyst reviews
// the values and saves them through SaveEntryAsync like any typed entry.
public partial class IcpRunService
{
    public async Task<IcpReportValuesDto> GetReportValuesAsync(int runSampleId, int? solutionIndex, int userId, CancellationToken ct = default)
    {
        if (_reportReader == null)
            throw new InvalidOperationException("Reading instrument reports is not available.");

        var context = await IcpSampleEntryContext.LoadAsync(_db, runSampleId, ct, _clock);
        await EnsureAccessAsync(context.Run, userId, ct, SectionAccess.Read);

        var evidence = context.Run.Evidence
            .Where(e => e.IcpRunSampleId == runSampleId && e.Kind == IcpEvidenceKind.SampleReport && e.SupersededByEvidenceId == null)
            .OrderByDescending(e => e.UploadedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("Upload the sample result report first.");

        var bytes = await _storage.ReadAsync(evidence.FilePath);
        var text = await _reportReader.ReadAsync(bytes, evidence.FileName, ct);
        var report = IcpReportParser.Parse(text);
        var problems = report.Problems.ToList();

        var candidates = report.Solutions
            .Select((s, i) => (Solution: s, Index: i))
            .Where(x => x.Solution.Kind == IcpSolutionKind.Sample)
            .ToList();

        // The report labels a solution with the sample's number ("26032059 T Mg"), often several times (top/bottom,
        // one per element group). One that has every element the test needs is chosen for the analyst only when
        // it is the single such solution; otherwise the analyst chooses.
        var reference = context.Sample.ReferenceNumber;
        var neededSymbols = context.NeededElements.Select(e => e.Symbol).ToList();
        bool Covers(IcpSolution sol) => neededSymbols.All(n =>
            sol.Readings.Any(r => r.Value is not null && string.Equals(r.Symbol, n, StringComparison.OrdinalIgnoreCase)));
        bool Matches(IcpSolution sol) => sol.Label is { } label && label.Contains(reference, StringComparison.OrdinalIgnoreCase);

        var matches = candidates.Where(x => Matches(x.Solution)).ToList();
        var complete = matches.Where(x => Covers(x.Solution)).ToList();
        int? matched = complete.Count == 1 ? complete[0].Index
            : complete.Count == 0 && matches.Count == 1 ? matches[0].Index
            : null;
        if (matches.Count == 0 && candidates.Count > 0)
            problems.Add($"No solution in the report is labelled with sample {reference} - choose the one to use.");
        else if (matched is null && matches.Count > 1)
            problems.Add($"{(complete.Count > 1 ? complete.Count : matches.Count)} solutions in the report match sample {reference} - choose the one to use.");

        int? selected = solutionIndex ?? matched;
        if (selected is int chosen && candidates.All(c => c.Index != chosen))
            throw new InvalidOperationException("That solution is not a sample reading in the report.");

        var elements = new List<IcpReportElementValueDto>();
        if (selected is int sel)
        {
            var solution = report.Solutions[sel];
            foreach (var needed in context.NeededElements)
            {
                var reading = solution.Readings.FirstOrDefault(r =>
                    string.Equals(r.Symbol, needed.Symbol, StringComparison.OrdinalIgnoreCase) && r.Value is not null);
                if (reading == null)
                {
                    elements.Add(new IcpReportElementValueDto(needed.Id, needed.Symbol, null, null, "No reading for this element in the chosen solution."));
                    continue;
                }
                string? note = null;
                if (reading.Unit is not null && reading.Unit != "mg/L")
                    note = $"Reported in {reading.Unit}, not mg/L - check before using.";
                else if (reading.Value < 0m)
                    note = "Negative reading as printed - the entry does not accept negative values.";
                elements.Add(new IcpReportElementValueDto(needed.Id, needed.Symbol, reading.Value, reading.WavelengthNm, note));
            }
        }

        return new IcpReportValuesDto(
            evidence.Id, evidence.FileName, text.Source.ToString(), report.RequiresVerification, problems,
            candidates
                .OrderByDescending(c => Matches(c.Solution)).ThenBy(c => c.Index)
                .Select(c => new IcpReportSolutionDto(
                    c.Index, c.Solution.Label ?? $"(no Sample ID, page {c.Solution.Page})", c.Solution.Page, Matches(c.Solution),
                    string.Join(",", c.Solution.Readings.Where(r => r.Value is not null).Select(r => r.Symbol))))
                .ToList(),
            matched, selected, elements);
    }
}
