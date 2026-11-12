using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Labels;
using MicroLIMS.Infrastructure.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class LabelPdfRendererTests
{
    private const double MmToPt = 2.83465;
    private const double MarginMm = 1.5;
    private static readonly DateOnly DefaultPrintDate = new(2026, 11, 12);

    private readonly ILabelRenderer _renderer = new LabelPdfRenderer(new ZXingBarcodeEncoder());

    private static LabelContent MediaContent(
        string code = "TSA/03/26",
        IReadOnlyList<LabelField>? standardFields = null,
        IReadOnlyList<LabelField>? smallFields = null) =>
        new(
            LabelEntityType.MediaLot,
            code,
            standardFields ?? new LabelField[]
            {
                new("Media", "Tryptic Soy Agar"),
                new("Prepared", "10/11/2026"),
                new("Expiry", "10/12/2026"),
                new("By", "MM")
            },
            smallFields ?? new LabelField[]
            {
                new("Expiry", "10/12/2026")
            });

    private static LabelContent SampleContent(
        string code = "FP1026001",
        IReadOnlyList<LabelField>? standardFields = null,
        IReadOnlyList<LabelField>? smallFields = null) =>
        new(
            LabelEntityType.Sample,
            code,
            standardFields ?? new LabelField[]
            {
                new("Item", "Wellman Original"),
                new("Batch", "26021072"),
                new("Received", "10/11/2026")
            },
            smallFields ?? Array.Empty<LabelField>());

    private static string PageText(Page page) =>
        string.Join(" ", page.GetWords().Select(w => w.Text));

    [Fact]
    public void Existing_A4_canvas_is_unchanged()
    {
        var canvas = new PdfCanvas();
        Assert.Equal(PdfCanvas.PageWidth, canvas.PageWidthPt);
        Assert.Equal(PdfCanvas.PageHeight, canvas.PageHeightPt);
        canvas.Text("Page 1", PdfCanvas.MarginX, PdfCanvas.MarginTop, 10, PdfFont.Regular, PdfColor.FromHex("000000"));
        canvas.NewPage();
        Assert.Equal(PdfCanvas.PageWidth, canvas.PageWidthPt);
        Assert.Equal(PdfCanvas.PageHeight, canvas.PageHeightPt);
        var bytes = canvas.Build("A4 Regression Check");

        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(2, pdf.NumberOfPages);

        foreach (var page in pdf.GetPages())
        {
            Assert.InRange(page.Width, 595.276 - 0.01, 595.276 + 0.01);
            Assert.InRange(page.Height, 841.890 - 0.01, 841.890 + 0.01);
        }
    }

    [Fact]
    public void Three_copies_produce_three_pages_of_label_size()
    {
        var item = new LabelRenderItem(
            MediaContent(),
            WidthMm: 50m,
            HeightMm: 25m,
            LabelLayout.Standard,
            Copies: 3,
            PrintNumber: 1,
            DefaultPrintDate);

        var bytes = _renderer.Render(new[] { item });

        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(3, pdf.NumberOfPages);

        var expectedText = PageText(pdf.GetPage(1));
        foreach (var page in pdf.GetPages())
        {
            Assert.InRange(page.Width, 141.73 - 0.1, 141.73 + 0.1);
            Assert.InRange(page.Height, 70.87 - 0.1, 70.87 + 0.1);
            Assert.Equal(expectedText, PageText(page));
        }
    }

    [Fact]
    public void Code_and_print_date_are_always_present()
    {
        var standardItem = new LabelRenderItem(
            MediaContent(),
            WidthMm: 40m,
            HeightMm: 20m,
            LabelLayout.Standard,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        using (var pdf = PdfDocument.Open(_renderer.Render(new[] { standardItem })))
        {
            var page = pdf.GetPage(1);
            var text = PageText(page);
            Assert.Contains("TSA/03/26", text);
            Assert.Contains("Printed 12/11/2026", text);

            var boldText = string.Concat(page.Letters.Where(l => l.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true).Select(l => l.Value));
            Assert.Contains("TSA/03/26", boldText);
        }

        // A label so short (25x9 mm -> 6 mm content height) that all optional fields are dropped.
        var shortItem = new LabelRenderItem(
            MediaContent(
                code: "TSA/03/26",
                standardFields: new LabelField[]
                {
                    new("Media", "Tryptic Soy Agar"),
                    new("Prepared", "10/11/2026"),
                    new("Expiry", "10/12/2026"),
                    new("By", "MM")
                }),
            WidthMm: 25m,
            HeightMm: 9m,
            LabelLayout.Standard,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        using (var pdf = PdfDocument.Open(_renderer.Render(new[] { shortItem })))
        {
            var text = PageText(pdf.GetPage(1));
            Assert.Contains("TSA/03/26", text);
            Assert.Contains("Printed 12/11/2026", text);
            Assert.DoesNotContain("By: MM", text);
            Assert.DoesNotContain("Expiry: 10/12/2026", text);
        }
    }

    [Fact]
    public void Reprint_number_is_printed()
    {
        var firstPrint = new LabelRenderItem(
            MediaContent(),
            WidthMm: 50m,
            HeightMm: 25m,
            LabelLayout.Standard,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        using (var pdf = PdfDocument.Open(_renderer.Render(new[] { firstPrint })))
        {
            var text = PageText(pdf.GetPage(1));
            Assert.DoesNotContain("REPRINT", text);
        }

        var thirdPrint = firstPrint with { PrintNumber = 3 };
        using (var pdf = PdfDocument.Open(_renderer.Render(new[] { thirdPrint })))
        {
            var text = PageText(pdf.GetPage(1));
            Assert.Contains("REPRINT 2", text);
        }
    }

    [Fact]
    public void Small_layout_prints_only_small_fields()
    {
        var content = new LabelContent(
            LabelEntityType.MediaLot,
            "TSA/03/26",
            StandardFields: new LabelField[]
            {
                new("Media", "Tryptic Soy Agar"),
                new("BatchOnlyStandard", "STD-999")
            },
            SmallFields: new LabelField[]
            {
                new("Exp", "10/12/2026")
            });

        var item = new LabelRenderItem(
            content,
            WidthMm: 50m,
            HeightMm: 25m,
            LabelLayout.Small,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        using var pdf = PdfDocument.Open(_renderer.Render(new[] { item }));
        var text = PageText(pdf.GetPage(1));

        Assert.Contains("TSA/03/26", text);
        Assert.Contains("Exp: 10/12/2026", text);
        Assert.DoesNotContain("BatchOnlyStandard", text);
        Assert.DoesNotContain("Tryptic Soy Agar", text);
    }

    [Theory]
    [InlineData(LabelEntityType.MediaLot)]
    [InlineData(LabelEntityType.Sample)]
    public void Long_field_value_is_ellipsized_within_the_text_box(LabelEntityType entityType)
    {
        var longValue = new string('W', 200);
        var content = new LabelContent(
            entityType,
            entityType == LabelEntityType.Sample ? "FP1026001" : "TSA/03/26",
            StandardFields: new LabelField[] { new("Name", longValue) },
            SmallFields: new LabelField[] { new("Name", longValue) });

        var item = new LabelRenderItem(
            content,
            WidthMm: 50m,
            HeightMm: 25m,
            LabelLayout.Standard,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        var bytes = _renderer.Render(new[] { item });
        using var pdf = PdfDocument.Open(bytes);
        var page = pdf.GetPage(1);

        var marginPt = MarginMm * MmToPt;
        var text = PageText(page);
        Assert.Contains("...", text);
        Assert.DoesNotContain(longValue, text);

        // No text glyph x beyond page width - margin.
        foreach (var letter in page.Letters)
        {
            Assert.True(
                letter.BoundingBox.Right <= page.Width - marginPt + 0.05,
                $"Glyph '{letter.Value}' right edge {letter.BoundingBox.Right} exceeded page right margin {page.Width - marginPt}.");
        }

        // No text glyph overlaps the barcode box (drawn filled rectangles).
        var rects = page.Paths
            .SelectMany(p => p)
            .Select(sp => sp.GetBoundingRectangle())
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();
        Assert.NotEmpty(rects);

        var barcodeMinX = rects.Min(r => r.Left);
        var barcodeMaxX = rects.Max(r => r.Right);
        var barcodeMinY = rects.Min(r => r.Bottom);
        var barcodeMaxY = rects.Max(r => r.Top);

        foreach (var letter in page.Letters)
        {
            var g = letter.BoundingBox;
            var overlaps = g.Right > barcodeMinX && g.Left < barcodeMaxX &&
                           g.Top > barcodeMinY && g.Bottom < barcodeMaxY;
            Assert.False(overlaps, $"Glyph '{letter.Value}' overlapped the barcode bounding box.");
        }
    }

    [Fact]
    public void Code_that_does_not_fit_throws_LabelDoesNotFitException()
    {
        var longCode = new string('A', 250);
        var item = new LabelRenderItem(
            new LabelContent(LabelEntityType.Cryovial, longCode, Array.Empty<LabelField>(), Array.Empty<LabelField>()),
            WidthMm: 10m,
            HeightMm: 10m,
            LabelLayout.Small,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        var ex = Assert.Throws<LabelDoesNotFitException>(() => _renderer.Render(new[] { item }));
        Assert.Equal("The code does not fit a 10×10 mm label. Increase the label size in Label Formats.", ex.Message);

        var wideCode128 = new LabelRenderItem(
            SampleContent(code: "EQUIPMENT-SAMPLE-LONG-CODE-123456"),
            WidthMm: 25m,
            HeightMm: 25m,
            LabelLayout.Standard,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        var ex128 = Assert.Throws<LabelDoesNotFitException>(() => _renderer.Render(new[] { wideCode128 }));
        Assert.Equal("The code does not fit a 25×25 mm label. Increase the label size in Label Formats.", ex128.Message);
    }

    [Fact]
    public void Barcode_modules_are_drawn_as_filled_rectangles_at_min_size()
    {
        var fakeEncoder = new FakeTwoByTwoEncoder();
        var renderer = new LabelPdfRenderer(fakeEncoder);

        var item = new LabelRenderItem(
            MediaContent(),
            WidthMm: 50m,
            HeightMm: 25m,
            LabelLayout.Standard,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        var bytes = renderer.Render(new[] { item });
        using var pdf = PdfDocument.Open(bytes);
        var page = pdf.GetPage(1);

        // Pattern has 3 dark modules: (0,0), (0,1), (1,1).
        var filledPaths = page.Paths.Where(p => p.IsFilled).ToList();
        Assert.Equal(3, filledPaths.Count);

        foreach (var path in filledPaths)
        {
            var bounds = path.GetBoundingRectangle();
            Assert.True(bounds.HasValue);
            Assert.InRange(bounds.Value.Width, 0.7087 - 0.001, 0.7087 + 0.001);
            Assert.InRange(bounds.Value.Height, 0.7087 - 0.001, 0.7087 + 0.001);
        }
    }

    [Fact]
    public void Mixed_sizes_in_one_request_give_pages_of_each_size()
    {
        var item1 = new LabelRenderItem(
            SampleContent(),
            WidthMm: 50m,
            HeightMm: 25m,
            LabelLayout.Standard,
            Copies: 2,
            PrintNumber: 1,
            DefaultPrintDate);

        var item2 = new LabelRenderItem(
            new LabelContent(
                LabelEntityType.Cryovial,
                "ECOLI/02/26",
                Array.Empty<LabelField>(),
                new LabelField[] { new("Exp", "10/12/2026") }),
            WidthMm: 25m,
            HeightMm: 10m,
            LabelLayout.Small,
            Copies: 1,
            PrintNumber: 1,
            DefaultPrintDate);

        var bytes = _renderer.Render(new[] { item1, item2 });
        using var pdf = PdfDocument.Open(bytes);

        Assert.Equal(3, pdf.NumberOfPages);

        Assert.InRange(pdf.GetPage(1).Width, 141.73 - 0.1, 141.73 + 0.1);
        Assert.InRange(pdf.GetPage(1).Height, 70.87 - 0.1, 70.87 + 0.1);

        Assert.InRange(pdf.GetPage(2).Width, 141.73 - 0.1, 141.73 + 0.1);
        Assert.InRange(pdf.GetPage(2).Height, 70.87 - 0.1, 70.87 + 0.1);

        Assert.InRange(pdf.GetPage(3).Width, 70.87 - 0.1, 70.87 + 0.1);
        Assert.InRange(pdf.GetPage(3).Height, 28.35 - 0.1, 28.35 + 0.1);
    }

    [Fact]
    public void Invalid_inputs_throw_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _renderer.Render(Array.Empty<LabelRenderItem>()));

        var zeroCopies = new LabelRenderItem(MediaContent(), 50m, 25m, LabelLayout.Standard, 0, 1, DefaultPrintDate);
        Assert.Throws<ArgumentException>(() => _renderer.Render(new[] { zeroCopies }));

        var nonPositiveWidth = new LabelRenderItem(MediaContent(), 0m, 25m, LabelLayout.Standard, 1, 1, DefaultPrintDate);
        Assert.Throws<ArgumentException>(() => _renderer.Render(new[] { nonPositiveWidth }));

        var nonPositiveHeight = new LabelRenderItem(MediaContent(), 50m, -5m, LabelLayout.Standard, 1, 1, DefaultPrintDate);
        Assert.Throws<ArgumentException>(() => _renderer.Render(new[] { nonPositiveHeight }));
    }

    private sealed class FakeTwoByTwoEncoder : IBarcodeEncoder
    {
        public bool[] EncodeCode128(string content) => throw new NotSupportedException();

        public bool[,] EncodeDataMatrix(string content) => new[,]
        {
            { true, false },
            { true, true }
        };
    }
}
