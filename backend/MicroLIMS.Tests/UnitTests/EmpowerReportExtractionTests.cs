using MicroLIMS.Application.Services.InstrumentImport;
using MicroLIMS.Infrastructure.InstrumentImport;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;
using Xunit.Abstractions;

namespace MicroLIMS.Tests.UnitTests;

// Empower 3 single-injection reports (Osteocare Tablets, Vit D3, report
// method 74613), photographed on paper by the lab. Expected values are read
// off the photos by eye. Needs Tesseract: TESSERACT_PATH, the default
// Windows install, or "tesseract" on PATH.
public class EmpowerReportExtractionTests(ITestOutputHelper output)
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EmpowerReports");

    private static ReportTextReader Reader()
    {
        var exe = Environment.GetEnvironmentVariable("TESSERACT_PATH");
        if (string.IsNullOrEmpty(exe))
            exe = File.Exists(@"C:\Program Files\Tesseract-OCR\tesseract.exe") ? @"C:\Program Files\Tesseract-OCR\tesseract.exe" : "tesseract";
        return new ReportTextReader(new TesseractOcr(exe, Path.GetTempPath()));
    }

    public sealed record Expected(
        string File, string SampleName, string SampleType, int Vial, int Injection,
        DateTime Acquired, DateTime Processed, decimal Rt, decimal Area, decimal Height);

    private static readonly Expected[] All =
    [
        new("std-vitd3-inj1.jpg", "Std Vit D3", "Standard", 100, 1,
            new(2026, 10, 1, 7, 57, 24), new(2026, 10, 1, 8, 43, 55), 3.290m, 21711m, 2490m),
        new("26033657-fp-inj1.jpg", "26033657 FP", "Unknown", 107, 1,
            new(2026, 10, 1, 9, 43, 45), new(2026, 10, 1, 11, 20, 45), 3.300m, 31128m, 4324m),
        new("26033657-fp-inj2.jpg", "26033657 FP", "Unknown", 107, 2,
            new(2026, 10, 1, 9, 50, 8), new(2026, 10, 1, 11, 20, 58), 3.287m, 32537m, 4264m),
    ];

    public static TheoryData<Expected> Photos() => new(All);

    [Theory]
    [MemberData(nameof(Photos))]
    public async Task Photo_is_read_by_ocr(Expected e)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(FixtureDir, e.File));
        var text = await Reader().ReadAsync(bytes, e.File);
        foreach (var l in text.Lines) output.WriteLine($"[{l.Confidence}] {l.Text}");
        var report = EmpowerReportParser.Parse(text);
        foreach (var p in report.Problems) output.WriteLine("PROBLEM " + p);
        AssertReport(report, e, ReportTextSource.Ocr);
    }

    [Fact]
    public async Task Scanned_pdf_is_read_by_ocr_of_its_page_image()
    {
        var e = All[0];
        var jpeg = await File.ReadAllBytesAsync(Path.Combine(FixtureDir, e.File));
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4).AddJpeg(jpeg, new PdfRectangle(0, 0, 595, 842));

        var report = EmpowerReportParser.Parse(await Reader().ReadAsync(builder.Build(), "scan.pdf"));

        AssertReport(report, e, ReportTextSource.Ocr);
    }

    [Fact]
    public async Task Text_pdf_is_read_from_its_text_layer_without_ocr()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        void At(double x, double y, string s) => page.AddText(s, 9, new PdfPoint(x, y), font);
        At(40, 780, "Sample Name:"); At(130, 780, "26033657 FP"); At(300, 780, "Acquired By:"); At(400, 780, "omar");
        At(40, 768, "Sample Type:"); At(130, 768, "Unknown"); At(300, 768, "Sample Set Name:"); At(400, 768, "Vit D3");
        At(40, 756, "Vial:"); At(130, 756, "107"); At(300, 756, "Acq. Method Set:"); At(400, 756, "Vit D3");
        At(40, 744, "Injection #:"); At(130, 744, "2"); At(300, 744, "Processing Method"); At(400, 744, "Vit D3");
        At(40, 720, "Date Acquired:"); At(130, 720, "10/1/2026 9:50:08 AM EET");
        At(40, 708, "Date Processed:"); At(130, 708, "10/1/2026 11:20:58 AM EET");
        At(120, 400, "RT"); At(160, 400, "Area"); At(210, 400, "Height");
        At(100, 388, "1"); At(120, 388, "3.287"); At(160, 388, "32537"); At(210, 388, "4264");
        At(40, 100, "Report Method ID 74613");

        var text = await Reader().ReadAsync(builder.Build(), "report.pdf");
        var report = EmpowerReportParser.Parse(text);

        Assert.Equal(ReportTextSource.TextLayer, text.Source);
        Assert.Empty(report.Problems);
        Assert.False(report.RequiresVerification);
        Assert.Equal("26033657 FP", report.SampleName);
        Assert.Equal(2, report.InjectionNo);
        Assert.Equal(new DateTime(2026, 10, 1, 9, 50, 8), report.DateAcquired);
        Assert.Equal(new EmpowerPeak(1, 3.287m, 32537m, 4264m, null), Assert.Single(report.Peaks));
    }

    [Fact]
    public void Parser_flags_fields_the_two_ocr_passes_read_differently()
    {
        var text = new ReportText(ReportTextSource.Ocr,
            [new("Injection #: 1 Processing Method Vit D3", 95)],
            [new("Injection #: 4 Processing Method VitD3", 80)]);

        var report = EmpowerReportParser.Parse(text);

        Assert.True(report.Fields[EmpowerField.InjectionNo].CrossCheckMismatch);
        Assert.False(report.Fields[EmpowerField.ProcessingMethod].CrossCheckMismatch); // spacing only
        Assert.Contains(report.Problems, p => p.StartsWith("InjectionNo"));
        Assert.True(report.RequiresVerification);
    }

    [Fact]
    public void Parser_flags_a_retention_time_past_the_run_time()
    {
        var text = new ReportText(ReportTextSource.Ocr,
            [new("Run Time: 5.0 Minutes", 90), new("RT Area Height", null), new("113.290 21711 2490", 60)], []);

        var report = EmpowerReportParser.Parse(text);

        Assert.Contains(report.Problems, p => p.Contains("past the 5.0 min run time"));
    }

    private static void AssertReport(EmpowerReport r, Expected e, ReportTextSource source)
    {
        Assert.Equal(source, r.Source);
        Assert.True(r.RequiresVerification);
        Assert.Equal(e.SampleName, r.SampleName);
        Assert.Equal(e.SampleType, r.SampleType);
        Assert.Equal(e.Vial, r.Vial);
        Assert.Equal(e.Injection, r.InjectionNo);
        Assert.Equal("VITD3", r.SampleSetName?.Replace(" ", "").ToUpperInvariant());
        Assert.Equal("VITD3", r.ProcessingMethod?.Replace(" ", "").ToUpperInvariant());
        Assert.Equal(e.Acquired, r.DateAcquired);
        Assert.Equal(e.Processed, r.DateProcessed);
        Assert.Equal(74613, r.ReportMethodId);
        var peak = Assert.Single(r.Peaks);
        Assert.Equal((1, e.Rt, e.Area, e.Height), (peak.PeakNo, peak.RetentionTime, peak.Area, peak.Height));
    }
}
