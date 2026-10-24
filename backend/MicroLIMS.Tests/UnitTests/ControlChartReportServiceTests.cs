using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Spc;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Word;
using MicroLIMS.Persistence.DbContext;
using UglyToad.PdfPig;
using Xunit;
using static MicroLIMS.Tests.UnitTests.ControlChartServiceTests;

namespace MicroLIMS.Tests.UnitTests;

public class ControlChartReportServiceTests
{
    private static ControlChartReportService NewService(MicroLimsDbContext db) =>
        new(ControlChartServiceTests.NewService(db), new PdfGenerator(), new WordGenerator(), new DataExportAuditService(db), db);

    private static readonly string Image = Convert.ToBase64String(ReportImageAndWordRenderingTests.Png());

    private static ControlChartReportSection Section(string subject = "Filling Room", string image = "", string? title = null) =>
        new("EM_TAMC", subject, SampleCategory.EnvironmentalMonitoring, null, null, null, null, false,
            new ControlChartSettings(Rules: new[] { 1, 2, 3, 4, 5, 6, 7, 8 }), CapabilityBasis.SpecLimit, null, null,
            "I-MR chart", title, image == "" ? Image : image);

    private static ControlChartReportRequest Request(ControlChartReportFormat format, params ControlChartReportSection[] sections) =>
        new("Annual EM limit review 2026", "Prepared for QA review.\nSecond line.", format, true, sections.ToList());

    private static async Task<(MicroLimsDbContext Db, int UserId)> SeedAsync()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedRoomAsync(db, "Filling Room");
        await SeedRoomAsync(db, "Weighing Room");
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, Counts);
        await SeedResultsAsync(db, "Weighing Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, 1, 1, 2, 30, 1, 2);
        return (db, user.Id);
    }

    [Fact]
    public async Task Pdf_HoldsEveryChart_WithServerComputedFigures_AndIsLoggedAsAnExport()
    {
        var (db, userId) = await SeedAsync();

        var report = await NewService(db).GenerateAsync(
            Request(ControlChartReportFormat.Pdf, Section("Filling Room"), Section("Weighing Room", title: "Weighing room trend")), userId);

        Assert.Equal("application/pdf", report.ContentType);
        Assert.EndsWith(".pdf", report.FileName);
        using var pdf = PdfDocument.Open(report.Content);
        Assert.Equal(2, pdf.GetPages().Sum(p => p.GetImages().Count()));
        var text = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        Assert.Contains("Annual EM limit review 2026", text);
        Assert.Contains("Weighing room trend", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Prepared for QA review.", text);
        Assert.DoesNotContain("?within", text);

        var expectedMean = ControlChartCalculator.Compute(Counts, new ControlChartSettings()).Mean!.Value;
        Assert.Contains(Math.Round(expectedMean, 2).ToString(System.Globalization.CultureInfo.InvariantCulture), text);

        var log = await db.DataExportLogs.SingleAsync();
        Assert.Equal("ControlChartReportPdf", log.ExportType);
        Assert.Equal(2, log.RowCount);
        Assert.Equal(userId, log.UserId);
        Assert.DoesNotContain(Image[..40], log.FilterJson);
        Assert.Equal(2, JsonDocument.Parse(log.FilterJson).RootElement.GetProperty("sections").GetArrayLength());
    }

    [Fact]
    public async Task Word_HoldsEveryChart()
    {
        var (db, userId) = await SeedAsync();

        var report = await NewService(db).GenerateAsync(Request(ControlChartReportFormat.Word, Section("Filling Room"), Section("Weighing Room")), userId);

        Assert.EndsWith(".docx", report.FileName);
        using var word = WordprocessingDocument.Open(new MemoryStream(report.Content), false);
        Assert.Equal(2, word.MainDocumentPart!.ImageParts.Count());
        Assert.Contains("Weighing Room", word.MainDocumentPart.Document.Body!.InnerText);
        Assert.Equal("ControlChartReportWord", (await db.DataExportLogs.SingleAsync()).ExportType);
    }

    [Fact]
    public async Task DataUrlPrefix_IsAccepted()
    {
        var (db, userId) = await SeedAsync();
        var report = await NewService(db).GenerateAsync(Request(ControlChartReportFormat.Pdf, Section(image: "data:image/png;base64," + Image)), userId);
        Assert.NotEmpty(report.Content);
    }

    [Theory]
    [InlineData("not base64 !!")]
    [InlineData("JVBERi0xLjQ=")] // "%PDF-1.4" - base64 of something that is not a PNG
    public async Task InvalidImage_IsRefused_AndNothingIsLogged(string image)
    {
        var (db, userId) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).GenerateAsync(Request(ControlChartReportFormat.Pdf, Section(image: image)), userId));
        Assert.Empty(db.DataExportLogs);
    }

    [Fact]
    public async Task EmptyOrOversizedReports_AreRefused()
    {
        var (db, userId) = await SeedAsync();
        var service = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(Request(ControlChartReportFormat.Pdf), userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(
            Request(ControlChartReportFormat.Pdf, Enumerable.Range(0, ControlChartReportService.MaxSections + 1).Select(_ => Section()).ToArray()), userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(
            Request(ControlChartReportFormat.Pdf, Section()) with { Title = " " }, userId));
    }
}
