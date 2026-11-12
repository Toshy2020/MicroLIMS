using System.Globalization;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Labels;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;

namespace MicroLIMS.Infrastructure.Labels;

public sealed class LabelPdfRenderer(IBarcodeEncoder encoder) : ILabelRenderer
{
    private const double MmToPt = 2.83465;
    private const double MarginMm = 1.5;
    private const double MinModuleWidthMm = 0.25;
    private const double GapMm = 0.8;
    private const double MinCode128BarHeightMm = 2.0;
    private const double MaxCode128BarHeightMm = 8.0;

    // ISO/IEC 15417 §4.3: Code 128 requires a minimum quiet zone of 10X (10 modules) on each end.
    private const int Code128QuietZoneModules = 10;

    // ISO/IEC 16022 §4.2 / §5.2.6: Data Matrix requires a minimum quiet zone of 1X (1 module) on all four sides.
    private const int DataMatrixQuietZoneModules = 1;

    private static readonly PdfColor Black = PdfColor.FromHex("000000");

    private readonly IBarcodeEncoder _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));

    private sealed record PreparedLabel(
        LabelRenderItem Item,
        double PageWidthPt,
        double PageHeightPt,
        LabelSymbology Symbology,
        bool[]? Code128Modules,
        bool[,]? DataMatrixModules,
        double BarHeightPt,
        double TextBoxX,
        double TextBoxY,
        double TextBoxW,
        double TextBoxH,
        TextMetrics Metrics);

    private readonly record struct TextMetrics(double CodeSize, double CodeStep, double BodySize, double BodyStep);

    public byte[] Render(IReadOnlyList<LabelRenderItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("At least one label item is required.", nameof(items));
        }

        var prepared = new List<PreparedLabel>(items.Count);
        foreach (var item in items)
        {
            prepared.Add(Prepare(item));
        }

        PdfCanvas? canvas = null;
        foreach (var label in prepared)
        {
            for (var copy = 0; copy < label.Item.Copies; copy++)
            {
                if (canvas is null)
                {
                    canvas = new PdfCanvas(label.PageWidthPt, label.PageHeightPt);
                }
                else
                {
                    canvas.NewPage(label.PageWidthPt, label.PageHeightPt);
                }

                DrawLabel(canvas, label);
            }
        }

        return canvas is null
            ? throw new ArgumentException("At least one label item is required.", nameof(items))
            : canvas.Build("Labels");
    }

    private PreparedLabel Prepare(LabelRenderItem item)
    {
        if (item is null)
        {
            throw new ArgumentException("Label item cannot be null.", nameof(item));
        }
        if (item.Content is null)
        {
            throw new ArgumentException("Label content cannot be null.", nameof(item));
        }
        if (item.Copies < 1)
        {
            throw new ArgumentException("Copies must be at least 1.", nameof(item));
        }
        if (item.WidthMm <= 0 || item.HeightMm <= 0)
        {
            throw new ArgumentException("Label width and height must be positive.", nameof(item));
        }

        var payload = LabelPayload.Build(item.Content.Type, item.Content.Code);
        var symbology = LabelPayload.SymbologyOf(item.Content.Type);

        var pageWidthPt = (double)item.WidthMm * MmToPt;
        var pageHeightPt = (double)item.HeightMm * MmToPt;
        var marginPt = MarginMm * MmToPt;
        var modulePt = MinModuleWidthMm * MmToPt;
        var gapPt = GapMm * MmToPt;
        var contentW = pageWidthPt - 2 * marginPt;
        var contentH = pageHeightPt - 2 * marginPt;

        if (contentW <= 0 || contentH <= 0)
        {
            throw DoesNotFit(item);
        }

        if (symbology == LabelSymbology.DataMatrix)
        {
            var matrix = _encoder.EncodeDataMatrix(payload);
            var rows = matrix.GetLength(0);
            var cols = matrix.GetLength(1);
            var barcodeW = (cols + 2 * DataMatrixQuietZoneModules) * modulePt;
            var barcodeH = (rows + 2 * DataMatrixQuietZoneModules) * modulePt;

            if (barcodeW > contentW || barcodeH > contentH)
            {
                throw DoesNotFit(item);
            }

            var textX = marginPt + barcodeW + gapPt;
            var textW = (pageWidthPt - marginPt) - textX;
            if (textW <= 0)
            {
                throw DoesNotFit(item);
            }

            var metrics = ChooseTextMetrics(contentH);
            return new PreparedLabel(
                item,
                pageWidthPt,
                pageHeightPt,
                symbology,
                null,
                matrix,
                0,
                textX,
                marginPt,
                textW,
                contentH,
                metrics);
        }

        if (symbology == LabelSymbology.Code128)
        {
            var modules = _encoder.EncodeCode128(payload);
            var barcodeW = (modules.Length + 2 * Code128QuietZoneModules) * modulePt;
            if (barcodeW > contentW)
            {
                throw DoesNotFit(item);
            }

            var hasReprint = item.PrintNumber > 1;
            var compact = CompactTextMetrics();
            var mandatoryCompactH = compact.CodeStep + compact.BodyStep * (hasReprint ? 2 : 1);
            var maxBarH = contentH - gapPt - mandatoryCompactH;
            var minBarH = MinCode128BarHeightMm * MmToPt;

            if (maxBarH < minBarH)
            {
                throw DoesNotFit(item);
            }

            var desiredBarH = Math.Min(contentH * 0.30, MaxCode128BarHeightMm * MmToPt);
            var barH = Math.Max(minBarH, Math.Min(desiredBarH, maxBarH));
            var textY = marginPt + barH + gapPt;
            var textH = (pageHeightPt - marginPt) - textY;
            var metrics = ChooseTextMetrics(textH);

            return new PreparedLabel(
                item,
                pageWidthPt,
                pageHeightPt,
                symbology,
                modules,
                null,
                barH,
                marginPt,
                textY,
                contentW,
                textH,
                metrics);
        }

        throw new ArgumentOutOfRangeException(nameof(item), symbology, "Unsupported label symbology.");
    }

    private static void DrawLabel(PdfCanvas canvas, PreparedLabel label)
    {
        var marginPt = MarginMm * MmToPt;
        var modulePt = MinModuleWidthMm * MmToPt;
        var contentW = label.PageWidthPt - 2 * marginPt;

        if (label.Symbology == LabelSymbology.DataMatrix && label.DataMatrixModules is not null)
        {
            var matrix = label.DataMatrixModules;
            var rows = matrix.GetLength(0);
            var cols = matrix.GetLength(1);
            var originX = marginPt + DataMatrixQuietZoneModules * modulePt;
            var originY = marginPt + DataMatrixQuietZoneModules * modulePt;

            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    if (!matrix[r, c])
                    {
                        continue;
                    }

                    canvas.FillRect(
                        originX + c * modulePt,
                        originY + r * modulePt,
                        modulePt,
                        modulePt,
                        Black);
                }
            }
        }
        else if (label.Symbology == LabelSymbology.Code128 && label.Code128Modules is not null)
        {
            var modules = label.Code128Modules;
            var totalW = (modules.Length + 2 * Code128QuietZoneModules) * modulePt;
            var barcodeBoxX = marginPt + (contentW - totalW) / 2.0;
            var barsOriginX = barcodeBoxX + Code128QuietZoneModules * modulePt;

            for (var i = 0; i < modules.Length; i++)
            {
                if (!modules[i])
                {
                    continue;
                }

                canvas.FillRect(
                    barsOriginX + i * modulePt,
                    marginPt,
                    modulePt,
                    label.BarHeightPt,
                    Black);
            }
        }

        DrawTextBlock(canvas, label);
    }

    private static void DrawTextBlock(PdfCanvas canvas, PreparedLabel label)
    {
        var item = label.Item;
        var metrics = label.Metrics;
        var y = label.TextBoxY;

        // Bold code line is mandatory and never dropped.
        var codeText = canvas.Ellipsize(item.Content.Code, label.TextBoxW, metrics.CodeSize, PdfFont.Bold);
        canvas.Text(codeText, label.TextBoxX, y, metrics.CodeSize, PdfFont.Bold, Black);
        y += metrics.CodeStep;

        var footerLines = new List<string>(2);
        if (item.PrintNumber > 1)
        {
            footerLines.Add($"REPRINT {item.PrintNumber - 1}");
        }
        footerLines.Add($"Printed {item.PrintDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");

        var footerHeight = footerLines.Count * metrics.BodyStep;
        var optionalBottomLimit = label.TextBoxY + label.TextBoxH - footerHeight;

        var fields = (item.Layout == LabelLayout.Small
            ? item.Content.SmallFields
            : item.Content.StandardFields) ?? Array.Empty<LabelField>();

        foreach (var field in fields)
        {
            if (y + metrics.BodyStep > optionalBottomLimit + 0.001)
            {
                break;
            }

            var line = canvas.Ellipsize($"{field.Name}: {field.Value}", label.TextBoxW, metrics.BodySize, PdfFont.Regular);
            canvas.Text(line, label.TextBoxX, y, metrics.BodySize, PdfFont.Regular, Black);
            y += metrics.BodyStep;
        }

        foreach (var footer in footerLines)
        {
            var font = footer.StartsWith("REPRINT ", StringComparison.Ordinal) ? PdfFont.Bold : PdfFont.Regular;
            var line = canvas.Ellipsize(footer, label.TextBoxW, metrics.BodySize, font);
            canvas.Text(line, label.TextBoxX, y, metrics.BodySize, font, Black);
            y += metrics.BodyStep;
        }
    }

    private static TextMetrics ChooseTextMetrics(double availableHeightPt) =>
        availableHeightPt >= 26.0
            ? new TextMetrics(CodeSize: 6.5, CodeStep: 7.8, BodySize: 5.8, BodyStep: 6.7)
            : CompactTextMetrics();

    private static TextMetrics CompactTextMetrics() =>
        new(CodeSize: 6.0, CodeStep: 7.0, BodySize: 5.5, BodyStep: 6.2);

    private static LabelDoesNotFitException DoesNotFit(LabelRenderItem item) =>
        new($"The code does not fit a {FormatMm(item.WidthMm)}×{FormatMm(item.HeightMm)} mm label. Increase the label size in Label Formats.");

    private static string FormatMm(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
