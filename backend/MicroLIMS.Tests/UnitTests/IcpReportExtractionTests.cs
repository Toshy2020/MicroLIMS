using MicroLIMS.Application.Services.InstrumentImport;
using MicroLIMS.Infrastructure.InstrumentImport;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The lab's ICP-OES "SampleID / Analyte / Mean" PDF reports (text layer, no
// OCR). Expected values are taken from the PDFs.
public class IcpReportExtractionTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "IcpReports");

    private static async Task<IcpReport> Read(string file)
    {
        var reader = new ReportTextReader(new TesseractOcr("tesseract", Path.GetTempPath())); // not used: text PDFs
        var text = await reader.ReadAsync(await File.ReadAllBytesAsync(Path.Combine(FixtureDir, file)), file);
        Assert.Equal(ReportTextSource.TextLayer, text.Source);
        return IcpReportParser.Parse(text);
    }

    private static decimal? Value(IcpSolution s, string symbol) => s.Readings.Single(r => r.Symbol == symbol).Value;

    [Fact]
    public async Task Bulk_report_reads_calibration_and_all_36_sample_solutions_across_4_pages()
    {
        var r = await Read("icp-26032059-64-bulk.pdf");

        Assert.Empty(r.Problems);
        Assert.False(r.RequiresVerification);
        Assert.Equal(new DateOnly(2026, 8, 13), r.ReportDate);
        Assert.Equal("26033059--64 BULK", r.Title);

        Assert.Equal(IcpSolutionKind.Blank, r.Solutions[0].Kind);
        var standards = r.Solutions.Where(s => s.Kind == IcpSolutionKind.Standard).ToList();
        Assert.Equal([".1 ppm", ".5 ppm", "1 ppm", "3 ppm", "6 ppm"], standards.Select(s => s.Label));
        Assert.All(standards[1].Readings, x => Assert.Equal((0.5m, true), (x.Value!.Value, x.Defined)));

        var samples = r.Solutions.Where(s => s.Kind == IcpSolutionKind.Sample).ToList();
        Assert.Equal(36, samples.Count);
        Assert.Equal("26032059 T Mg", samples[0].Label);
        Assert.Equal(1.570m, Value(samples[0], "Mg"));
        Assert.Equal(285.213m, samples[0].Readings[0].WavelengthNm);
        Assert.Equal("26032064 B Zn.Fe", samples[^1].Label);
        Assert.Equal((0.853m, 0.761m), (Value(samples[^1], "Fe"), Value(samples[^1], "Zn")));
        Assert.Equal(4, samples[^1].Page);
    }

    [Fact]
    public async Task Readings_printed_without_a_sample_id_are_kept_apart_and_flagged()
    {
        var r = await Read("icp-26108065-80-fp.pdf");

        Assert.True(r.RequiresVerification);
        Assert.Equal(new DateOnly(2026, 8, 18), r.ReportDate);
        var unlabelled = r.Solutions.Where(s => s.Label is null).ToList();
        // A second blank, repeats after 26108072/75/76/79 and two Cu/Mn
        // repeats on page 4 - all printed with no Sample ID line.
        Assert.Equal(10, unlabelled.Count);
        Assert.Equal(IcpSolutionKind.Blank, unlabelled[0].Kind);
        Assert.Equal((0.231m, 1.072m, 0.286m), (Value(unlabelled[1], "Ca"), Value(unlabelled[1], "Fe"), Value(unlabelled[1], "Zn")));
        Assert.Equal((0.317m, 0.406m), (Value(unlabelled[^2], "Cu"), Value(unlabelled[^2], "Mn")));
        Assert.Equal((0.231m, 0.247m), (Value(unlabelled[^1], "Cu"), Value(unlabelled[^1], "Mn")));
        Assert.Contains(r.Problems, p => p.Contains("no Sample ID line"));
        // Ca/Fe/Zn printed under a Cu,Mn Sample ID.
        Assert.Contains(r.Problems, p => p.StartsWith("'26108085 Top Cu,Mn': a Ca reading"));
        Assert.Contains(r.Problems, p => p == "'26108085 Top Cu,Mn' was measured 2 times - choose which reading to use.");
        // Negative readings are kept as printed.
        Assert.Contains(r.Solutions, s => s.Readings.Any(x => x is { Symbol: "Mn", Value: -0.012m }));
    }

    [Theory]
    [InlineData("icp-wellbaby-26111001-fp.pdf", 2026, 7, 22, 6, 5)]
    [InlineData("icp-wellbaby-mp-26111001.pdf", 2026, 7, 19, 26, 5)]
    [InlineData("icp-wellkid-liquid-mp.pdf", 2026, 7, 19, 12, 0)]
    public async Task Other_reports_read_cleanly(string file, int y, int m, int d, int samples, int standards)
    {
        var r = await Read(file);

        Assert.Empty(r.Problems);
        Assert.Equal(new DateOnly(y, m, d), r.ReportDate);
        Assert.Equal(samples, r.Solutions.Count(s => s.Kind == IcpSolutionKind.Sample));
        Assert.Equal(standards, r.Solutions.Count(s => s.Kind == IcpSolutionKind.Standard));
    }

    [Fact]
    public async Task Wellkid_last_solution_values()
    {
        var r = await Read("icp-wellkid-liquid-mp.pdf");
        var last = r.Solutions[^1];

        Assert.Equal("Zn,Fe6", last.Label);
        Assert.Equal((0.511m, 0.389m), (Value(last, "Fe"), Value(last, "Zn")));
    }
}
