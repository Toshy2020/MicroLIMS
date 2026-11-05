using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.InstrumentImport;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.InstrumentImport;
using Xunit;
using static MicroLIMS.Tests.UnitTests.IcpRunServiceTests;
using static MicroLIMS.Tests.UnitTests.IcpRunSampleTests;

namespace MicroLIMS.Tests.UnitTests;

// Auto-fill: the sample's results proposed from its uploaded instrument report. Read-only.
public class IcpReportAutofillTests
{
    // Serves canned report lines in the Syngistix "SampleID / Analyte / Mean" layout.
    private sealed class FakeReader(params string[] lines) : IReportTextReader
    {
        public int Calls;
        public Task<ReportText> ReadAsync(byte[] content, string fileName, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new ReportText(ReportTextSource.TextLayer, lines.Select(l => new ReportLine(l, null)).ToList(), []));
        }
    }

    private const string Rule = "----------";

    private static string[] Report(params string[] body) =>
        new[] { "13/08/2026 10:00:00", "MY RUN", "SampleID Analyte Mean" }.Concat(body).ToArray();

    private static string[] Solution(string label, params string[] readings) =>
        new[] { $"{label} {Rule}" }.Concat(readings).ToArray();

    private static readonly byte[] DummyPdf = { 0x25, 0x50, 0x44, 0x46, 0x2D };

    private sealed record Arrangement(Fixture F, IcpRunService Svc, int RunSampleId);

    // A confirmed run with the sample assigned and (optionally) its report uploaded. The service reads with `reader`.
    private static async Task<Arrangement> ArrangeAsync(
        IReportTextReader? reader, string reference, byte[]? report = null, bool uploadReport = true)
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s, IcpTestMode.Assay, IcpSampleForm.Solid, withCalcium: true);
        var order = await AddOrderAsync(f);
        (await s.Db.Samples.FindAsync(order.SampleId))!.ReferenceNumber = reference;
        await s.Db.SaveChangesAsync();

        var run = await ConfirmedRunAsync(s);
        var svc = TestServiceFactory.IcpRun(s.Db, clock: NewClock(), storage: new InMemoryFileStorageService(), reportReader: reader);
        run = await svc.AssignSamplesAsync(run.Id, new AssignIcpSamplesRequest(new List<int> { order.Id }, Password, null), s.UserId, null);
        var rsId = run.Samples.Single().Id;
        if (uploadReport)
            await svc.UploadEvidenceAsync(run.Id, rsId, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport,
                "s.pdf", "application/pdf", new MemoryStream(report ?? DummyPdf), s.UserId);
        return new Arrangement(f, svc, rsId);
    }

    [Fact]
    public async Task OneSolutionWithEveryNeededElement_IsChosen_AndItsValuesProposed()
    {
        var reader = new FakeReader(Report(
            Solution("blank", "Zn 213.857", "Ca 317.933")
                .Concat(Solution("FP-7777 Top Zn.Ca", "Ca 317.933 19.0 mg/L", "Zn 213.857 9.5 mg/L"))
                .Concat(Solution("FP-8888 Top Zn.Ca", "Ca 317.933 1.0 mg/L", "Zn 213.857 2.0 mg/L")).ToArray()));
        var a = await ArrangeAsync(reader, "FP-7777");

        var dto = await a.Svc.GetReportValuesAsync(a.RunSampleId, null, a.F.S.UserId);

        Assert.Equal("s.pdf", dto.FileName);
        Assert.Equal(dto.MatchedIndex, dto.SelectedIndex);
        Assert.NotNull(dto.SelectedIndex);
        Assert.Equal(new[] { 9.5m, 19.0m }, dto.Elements.OrderBy(e => e.Symbol == "Zn" ? 0 : 1).Select(e => e.ResultPpm!.Value).ToArray());
        Assert.Equal(213.857m, dto.Elements.Single(e => e.Symbol == "Zn").WavelengthNm);
        Assert.All(dto.Elements, e => Assert.Null(e.Note));
        // The sample's solution is listed first; the other sample is still selectable.
        Assert.Equal(2, dto.Solutions.Count);
        Assert.True(dto.Solutions[0].MatchesSample);
        Assert.Equal("FP-7777 Top Zn.Ca", dto.Solutions[0].Label);
        Assert.False(dto.Solutions[1].MatchesSample);
        Assert.False(dto.RequiresVerification);
    }

    [Fact]
    public async Task SeveralSolutionsForTheSample_ArePresentedForTheAnalystToChoose()
    {
        var reader = new FakeReader(Report(
            Solution("FP-7777 T Zn.Ca", "Ca 317.933 19.0 mg/L", "Zn 213.857 9.5 mg/L")
                .Concat(Solution("FP-7777 B Zn.Ca", "Ca 317.933 18.0 mg/L", "Zn 213.857 9.0 mg/L")).ToArray()));
        var a = await ArrangeAsync(reader, "FP-7777");

        var dto = await a.Svc.GetReportValuesAsync(a.RunSampleId, null, a.F.S.UserId);

        Assert.Null(dto.MatchedIndex);
        Assert.Null(dto.SelectedIndex);
        Assert.Empty(dto.Elements);
        Assert.Contains(dto.Problems, p => p == "2 solutions in the report match sample FP-7777 - choose the one to use.");

        var bottom = dto.Solutions.Single(x => x.Label.EndsWith("B Zn.Ca"));
        var chosen = await a.Svc.GetReportValuesAsync(a.RunSampleId, bottom.Index, a.F.S.UserId);
        Assert.Equal(bottom.Index, chosen.SelectedIndex);
        Assert.Equal(9.0m, chosen.Elements.Single(e => e.Symbol == "Zn").ResultPpm);
    }

    [Fact]
    public async Task NoSolutionLabelledWithTheSample_AsksTheAnalystToChoose()
    {
        var reader = new FakeReader(Report(Solution("FP-0000 Top", "Zn 213.857 9.5 mg/L", "Ca 317.933 19.0 mg/L")));
        var a = await ArrangeAsync(reader, "FP-7777");

        var dto = await a.Svc.GetReportValuesAsync(a.RunSampleId, null, a.F.S.UserId);

        Assert.Null(dto.SelectedIndex);
        Assert.Contains(dto.Problems, p => p == "No solution in the report is labelled with sample FP-7777 - choose the one to use.");
        Assert.Single(dto.Solutions);
        Assert.False(dto.Solutions[0].MatchesSample);
    }

    [Fact]
    public async Task ElementMissingNegativeOrInAnotherUnit_ComesWithANote()
    {
        var reader = new FakeReader(Report(
            Solution("FP-7777 Top", "Zn 213.857 -0.012 mg/L", "Ca 317.933 19 mg/kg")));
        var a = await ArrangeAsync(reader, "FP-7777");

        var dto = await a.Svc.GetReportValuesAsync(a.RunSampleId, null, a.F.S.UserId);

        Assert.Equal("Negative reading as printed - the entry does not accept negative values.", dto.Elements.Single(e => e.Symbol == "Zn").Note);
        Assert.Equal("Reported in mg/kg, not mg/L - check before using.", dto.Elements.Single(e => e.Symbol == "Ca").Note);
        Assert.Contains(dto.Problems, p => p.Contains("mg/kg"));
        Assert.True(dto.RequiresVerification);

        var partial = await ArrangeAsync(new FakeReader(Report(Solution("FP-7777 Top", "Zn 213.857 9.5 mg/L"))), "FP-7777");
        var dto2 = await partial.Svc.GetReportValuesAsync(partial.RunSampleId, null, partial.F.S.UserId);
        var ca = dto2.Elements.Single(e => e.Symbol == "Ca");
        Assert.Null(ca.ResultPpm);
        Assert.Equal("No reading for this element in the chosen solution.", ca.Note);
    }

    [Fact]
    public async Task ABlankOrStandardSolution_CannotBeChosen()
    {
        var reader = new FakeReader(Report(
            Solution("blank", "Zn 213.857", "Ca 317.933").Concat(Solution("FP-7777 Top", "Zn 213.857 9.5 mg/L", "Ca 317.933 19 mg/L")).ToArray()));
        var a = await ArrangeAsync(reader, "FP-7777");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            a.Svc.GetReportValuesAsync(a.RunSampleId, 0, a.F.S.UserId));
        Assert.Equal("That solution is not a sample reading in the report.", ex.Message);
    }

    [Fact]
    public async Task WithoutAnUploadedReport_Throws()
    {
        var reader = new FakeReader(Report());
        var a = await ArrangeAsync(reader, "FP-7777", uploadReport: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => a.Svc.GetReportValuesAsync(a.RunSampleId, null, a.F.S.UserId));
        Assert.Equal("Upload the sample result report first.", ex.Message);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task WithoutAReportReader_Throws()
    {
        var a = await ArrangeAsync(null, "FP-7777");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => a.Svc.GetReportValuesAsync(a.RunSampleId, null, a.F.S.UserId));
        Assert.Equal("Reading instrument reports is not available.", ex.Message);
    }

    [Fact]
    public async Task OnlyTheCurrentReportIsRead_AndNothingIsSaved()
    {
        var reader = new FakeReader(Report(Solution("FP-7777 Top", "Zn 213.857 9.5 mg/L", "Ca 317.933 19 mg/L")));
        var a = await ArrangeAsync(reader, "FP-7777");

        await a.Svc.GetReportValuesAsync(a.RunSampleId, null, a.F.S.UserId);

        // Read-only: no results were stored, the analyst still has to save them.
        Assert.Empty(a.F.S.Db.IcpSampleConcentrations);
        Assert.Null((await a.Svc.GetSampleEntryAsync(a.RunSampleId, a.F.S.UserId)).ActualWeightG);
    }

    // The lab's real 4-page bulk report (text layer): every reference appears several times, so it is the analyst's call.
    [Fact]
    public async Task RealBulkReport_ListsTheSamplesSolutionsFirst_AndReadsTheChosenOne()
    {
        var reader = new ReportTextReader(new TesseractOcr("tesseract", Path.GetTempPath())); // not used: text PDF
        var pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "IcpReports", "icp-26032059-64-bulk.pdf"));
        var a = await ArrangeAsync(reader, "26032061", pdf);
        // The test needs Zn and Ca; the report has Zn.Fe solutions - rename Ca to Fe on the test.
        a.F.Definition.IcpElements.Single(e => e.Symbol == "Ca").Symbol = "Fe";
        await a.F.S.Db.SaveChangesAsync();

        var dto = await a.Svc.GetReportValuesAsync(a.RunSampleId, null, a.F.S.UserId);

        Assert.Equal("TextLayer", dto.Source);
        Assert.Equal(6, dto.Solutions.Count(x => x.MatchesSample)); // T/B x Mg, Cu, Zn.Fe
        Assert.All(dto.Solutions.Take(6), x => Assert.Contains("26032061", x.Label));
        Assert.Null(dto.SelectedIndex); // T and B both have Zn and Fe: the analyst chooses

        var top = dto.Solutions.Single(x => x.Label == "26032061 T Zn.Fe");
        var chosen = await a.Svc.GetReportValuesAsync(a.RunSampleId, top.Index, a.F.S.UserId);
        Assert.Equal(0.635m, chosen.Elements.Single(e => e.Symbol == "Zn").ResultPpm);
        Assert.Equal(0.850m, chosen.Elements.Single(e => e.Symbol == "Fe").ResultPpm);
    }
}
