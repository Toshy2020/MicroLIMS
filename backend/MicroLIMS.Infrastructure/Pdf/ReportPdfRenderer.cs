using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Infrastructure.Imaging;
namespace MicroLIMS.Infrastructure.Pdf;

// Draws a ReportDocument onto a PdfCanvas in the approved report layout:
// header with status badge, bordered cards, stat boxes, signature cards,
// and the controlled-document footer.
//
// Blocks are measured before they are drawn so a card is never split
// across a page boundary - the print CSS achieves this with
// page-break-inside:avoid; here it has to be done explicitly.
public class ReportPdfRenderer
{
    private static readonly PdfColor Black = PdfColor.FromHex("111111");
    private static readonly PdfColor TextSecondary = PdfColor.FromHex("444444");
    private static readonly PdfColor TextTertiary = PdfColor.FromHex("666666");
    private static readonly PdfColor TextQuaternary = PdfColor.FromHex("888888");
    private static readonly PdfColor Border = PdfColor.FromHex("BBBBBB");
    private static readonly PdfColor BorderStrong = PdfColor.FromHex("000000");
    private static readonly PdfColor SurfaceMuted = PdfColor.FromHex("F0F0F0");
    private static readonly PdfColor Surface = PdfColor.FromHex("FAFAFA");
    private static readonly PdfColor White = PdfColor.FromHex("FFFFFF");
    private static readonly PdfColor Positive = PdfColor.FromHex("16A34A");
    private static readonly PdfColor Danger = PdfColor.FromHex("DC2626");
    private static readonly PdfColor Warning = PdfColor.FromHex("D97706");
    private static readonly PdfColor ActionOrange = PdfColor.FromHex("EA580C");

    private const double TableHeaderRowH = 14.0;
    private const double TableRowH = 13.0;
    private const double TableLineStep = 9.0;
    private const double TableHeaderLineStep = 8.0;
    private const double TableCaptionH = 12.0;
    private const double TableCellSize = 7.5;
    private const double TableHeaderSize = 6.5;
    // A cell wraps instead of truncating, but a runaway value (a pasted
    // paragraph) is still capped so one cell cannot swallow a page.
    private const int MaxCellLines = 6;
    private const double RowLineStep = 10.0;

    private const double CardPad = 10;
    private const double BlockGap = 10;
    private const double RowHeight = 12.5;

    private PdfCanvas _c = null!;
    private double _y;
    private string _documentId = string.Empty;

    private static double Left => PdfCanvas.MarginX;
    private static double Width => PdfCanvas.ContentWidth;
    private static double PageBottom => PdfCanvas.PageHeight - PdfCanvas.MarginBottom;

    public byte[] Render(ReportDocument doc)
    {
        _c = new PdfCanvas();
        _y = PdfCanvas.MarginTop;
        _documentId = doc.DocumentId;

        DrawHeader(doc);
        foreach (var block in doc.Blocks) DrawBlock(block);

        // Footers are stamped last, once the page count is final, so each
        // one can carry a real "Page n of m".
        for (var i = 0; i < _c.PageCount; i++)
        {
            _c.SelectPage(i);
            DrawFooter(i + 1, _c.PageCount);
        }

        return _c.Build($"{doc.Kind} - {doc.DocumentId}");
    }

    private static PdfColor ToneColor(ReportTone tone) => tone switch
    {
        ReportTone.Positive => Positive,
        ReportTone.Danger => Danger,
        ReportTone.Warning => Warning,
        _ => TextTertiary
    };

    // Reserves vertical space, starting a new page (and repeating the
    // running header) when the block will not fit on the current one.
    private void EnsureSpace(double height)
    {
        if (_y + height <= PageBottom) return;
        // Already at the top of a fresh page: a forced break (or a block
        // taller than a page) must not leave a blank page behind.
        if (_y <= PdfCanvas.MarginTop + 14 && _c.PageCount > 1) return;
        _c.NewPage();
        _y = PdfCanvas.MarginTop;
        _c.Text($"{_documentId} (continued)", Left, _y, 7.5, PdfFont.Regular, TextQuaternary);
        _y += 14;
    }

    private void DrawHeader(ReportDocument doc)
    {
        _c.Text(doc.Kind.ToUpperInvariant(), Left, _y, 7, PdfFont.Regular, TextQuaternary);
        _c.Text(doc.DocumentId, Left, _y + 9, 22, PdfFont.Bold, Black);
        DrawSubtitle(doc);

        // Badge, right-aligned and sized to its text.
        var badgeFont = 9.0;
        var badgeW = _c.MeasureText(doc.BadgeText, badgeFont, PdfFont.Bold) + 16;
        var badgeX = Left + Width - badgeW;
        _c.FillAndStrokeRect(badgeX, _y, badgeW, 18, SurfaceMuted, BorderStrong, 1.0);
        _c.Text(doc.BadgeText, badgeX, _y + 4.5, badgeFont, PdfFont.Bold, Black, PdfAlign.Center, badgeW);
        _c.Text(doc.HeaderNote, Left, _y + 22, 8, PdfFont.Regular, TextQuaternary, PdfAlign.Right, Width);

        _y += 50;
        _c.Line(Left, _y, Left + Width, _y, BorderStrong, 1.5);
        _y += BlockGap + 4;
    }

    // Splits the subtitle around SubtitleEmphasis (typically the item/
    // machine/point name) so that one substring renders bold within an
    // otherwise regular-weight line - the canvas has no rich-text run,
    // so this measures and draws three separate text calls instead.
    private void DrawSubtitle(ReportDocument doc)
    {
        var y = _y + 36;
        if (string.IsNullOrEmpty(doc.SubtitleEmphasis))
        {
            _c.Text(doc.Subtitle, Left, y, 9, PdfFont.Regular, TextSecondary);
            return;
        }

        var idx = doc.Subtitle.IndexOf(doc.SubtitleEmphasis, StringComparison.Ordinal);
        if (idx < 0)
        {
            _c.Text(doc.Subtitle, Left, y, 9, PdfFont.Regular, TextSecondary);
            return;
        }

        var before = doc.Subtitle[..idx];
        var after = doc.Subtitle[(idx + doc.SubtitleEmphasis.Length)..];
        var x = Left;

        _c.Text(before, x, y, 9, PdfFont.Regular, TextSecondary);
        x += _c.MeasureText(before, 9, PdfFont.Regular);
        _c.Text(doc.SubtitleEmphasis, x, y, 9, PdfFont.Bold, Black);
        x += _c.MeasureText(doc.SubtitleEmphasis, 9, PdfFont.Bold);
        _c.Text(after, x, y, 9, PdfFont.Regular, TextSecondary);
    }

    private void DrawBlock(ReportBlock block)
    {
        switch (block)
        {
            case TwoColumnBlock b: DrawTwoColumn(b); break;
            case StripBlock b: DrawStrip(b); break;
            case HeadingBlock b: DrawHeading(b); break;
            case CardBlock b: DrawCard(b); break;
            case ListBlock b: DrawList(b); break;
            case SignatureBlock b: DrawSignatures(b); break;
            case TableBlock b: DrawTableBlock(b); break;
            case ImageBlock b: DrawImage(b); break;
            case PageBreakBlock: EnsureSpace(double.MaxValue / 2); break;
        }
    }

    // Scaled to the content width (never enlarged past its pixel size at
    // 2 px/pt) and capped at most of a page, so a tall chart is shrunk to
    // fit rather than split.
    private void DrawImage(ImageBlock b)
    {
        var (pxW, pxH, rgb) = PngRaster.DecodeToRgb(b.Png);
        var id = _c.AddImage(pxW, pxH, rgb);
        var captionH = string.IsNullOrWhiteSpace(b.Caption) ? 0 : 12.0;
        var maxH = PageBottom - PdfCanvas.MarginTop - 14 - captionH - BlockGap;
        var w = Math.Min(Width, pxW / 2.0);
        var h = w * pxH / pxW;
        if (h > maxH) { h = maxH; w = h * pxW / pxH; }

        EnsureSpace(h + captionH + 2);
        var x = Left + (Width - w) / 2;
        _c.DrawImage(id, x, _y, w, h);
        _c.StrokeRect(x, _y, w, h, Border, 0.5);
        _y += h + 2;
        if (captionH > 0)
        {
            _c.Text(_c.Ellipsize(b.Caption!, Width, 7.5, PdfFont.Regular), Left, _y + 1, 7.5, PdfFont.Regular, TextTertiary, PdfAlign.Center, Width);
            _y += captionH;
        }
        _y += BlockGap;
    }

    private void DrawTwoColumn(TwoColumnBlock b)
    {
        var colW = (Width - BlockGap) / 2;
        var rows = Math.Max(b.Left.Count, b.Right.Count);
        var height = CardPad * 2 + 12 + rows * RowHeight;

        EnsureSpace(height);
        DrawKeyValueCard(Left, colW, b.LeftLabel, b.Left, height);
        DrawKeyValueCard(Left + colW + BlockGap, colW, b.RightLabel, b.Right, height);
        _y += height + BlockGap;
    }

    private void DrawKeyValueCard(double x, double w, string label, List<(string Key, string Value)> pairs, double height)
    {
        _c.FillAndStrokeRect(x, _y, w, height, White, Border);
        _c.Text(label.ToUpperInvariant(), x + CardPad, _y + CardPad, 7, PdfFont.Regular, TextQuaternary);

        var keyW = 78.0;
        var valueW = w - CardPad * 2 - keyW;
        var rowY = _y + CardPad + 12;
        foreach (var (key, value) in pairs)
        {
            _c.Text(key, x + CardPad, rowY, 8.5, PdfFont.Regular, TextTertiary);
            _c.Text(_c.Ellipsize(value, valueW, 8.5, PdfFont.Bold), x + CardPad + keyW, rowY, 8.5, PdfFont.Bold, Black);
            rowY += RowHeight;
        }
    }

    private void DrawStrip(StripBlock b)
    {
        var hasSub = b.Items.Any(i => !string.IsNullOrEmpty(i.Sub));
        var height = CardPad * 2 + 12 + 22 + (hasSub ? 10 : 0);

        EnsureSpace(height);
        _c.FillAndStrokeRect(Left, _y, Width, height, White, Border);
        _c.Text(b.Label.ToUpperInvariant(), Left + CardPad, _y + CardPad, 7, PdfFont.Regular, TextQuaternary);

        var count = Math.Max(b.Items.Count, 1);
        var colW = (Width - CardPad * 2) / count;
        var x = Left + CardPad;
        foreach (var item in b.Items)
        {
            _c.Text(item.Label, x, _y + CardPad + 12, 7.5, PdfFont.Regular, TextTertiary);
            _c.Text(_c.Ellipsize(item.Value, colW - 6, 9, PdfFont.Bold), x, _y + CardPad + 22, 9, PdfFont.Bold, Black);
            if (!string.IsNullOrEmpty(item.Sub))
                _c.Text(_c.Ellipsize(item.Sub!, colW - 6, 7, PdfFont.Regular), x, _y + CardPad + 33, 7, PdfFont.Regular, TextQuaternary);
            x += colW;
        }
        _y += height + BlockGap;
    }

    private void DrawHeading(HeadingBlock b)
    {
        // Room for the heading and the start of what it introduces, so a
        // heading is never left alone at the foot of a page.
        EnsureSpace(18 + 80);
        _c.Text(b.Text.ToUpperInvariant(), Left, _y, 7, PdfFont.Regular, TextQuaternary);
        var textW = _c.MeasureText(b.Text.ToUpperInvariant(), 7, PdfFont.Regular);
        _c.Line(Left + textW + 6, _y + 4, Left + Width, _y + 4, Border);
        _y += 14;
    }

    private void DrawCard(CardBlock b)
    {
        var tone = ToneColor(b.Tone);
        var headerH = 30.0;
        var detailH = b.Details.Count > 0 ? 28.0 : 0;
        var statsH = b.Stats.Count > 0 ? 40.0 : 0;
        var metaLines = b.MetaLines.SelectMany(m => _c.WrapText(m, Width - CardPad * 2, 8, PdfFont.Regular)).ToList();
        // Without stat boxes above them the meta lines would sit on the
        // header band's rule, so they get their own top padding.
        var metaPad = metaLines.Count > 0 && b.Stats.Count == 0 ? 6.0 : 0;
        var metaH = metaLines.Count * 10.5 + metaPad;
        var rowLines = b.Rows.Select(r => (r.Left, Right: WrapCell(r.Right, Width * 0.48, 8, PdfFont.Regular))).ToList();
        var rowsH = rowLines.Count > 0 ? rowLines.Sum(r => RowHeightFor(r.Right.Count)) + 8 : 0;
        var tables = new List<ReportTable>();
        if (b.TableRows.Count > 0)
            tables.Add(new ReportTable { Columns = b.TableColumns, Rows = b.TableRows, StatusColumnIndex = b.TableStatusColumnIndex });
        tables.AddRange(b.Tables.Where(t => t.Rows.Count > 0));
        var tableH = tables.Sum(t => MeasureTable(t, Width - CardPad * 2) + (t.Caption is null ? 0 : TableCaptionH) + 4);
        var footerH = string.IsNullOrEmpty(b.FooterLeft) && string.IsNullOrEmpty(b.FooterRight) ? 0 : 16.0;
        var total = headerH + detailH + statsH + (metaH > 0 ? metaH + 6 : 0) + rowsH + tableH + footerH;

        EnsureSpace(total);
        var top = _y;
        // Header band
        _c.FillRect(Left, top, Width, headerH, SurfaceMuted);
        _c.Line(Left, top + headerH, Left + Width, top + headerH, Border);
        _c.Text(_c.Ellipsize(b.Title, Width * 0.62, 10.5, PdfFont.Bold), Left + CardPad, top + 6, 10.5, PdfFont.Bold, Black);
        _c.Text(_c.Ellipsize(b.Subtitle, Width * 0.62, 7.5, PdfFont.Regular), Left + CardPad, top + 19, 7.5, PdfFont.Regular, TextTertiary);
        if (!string.IsNullOrEmpty(b.HeadlineValue))
        {
            _c.Text(b.HeadlineValue, Left, top + 5, 15, PdfFont.Bold, tone, PdfAlign.Right, Width - CardPad);
            _c.Text(b.HeadlineUnit, Left, top + 21, 7, PdfFont.Regular, TextQuaternary, PdfAlign.Right, Width - CardPad);
        }

        var y = top + headerH;

        if (b.Details.Count > 0)
        {
            var colW = (Width - CardPad * 2) / b.Details.Count;
            var x = Left + CardPad;
            foreach (var (label, value) in b.Details)
            {
                _c.Text(label, x, y + 6, 7, PdfFont.Regular, TextQuaternary);
                _c.Text(_c.Ellipsize(value, colW - 6, 8.5, PdfFont.Bold), x, y + 15, 8.5, PdfFont.Bold, Black);
                x += colW;
            }
            y += detailH;
            _c.Line(Left, y, Left + Width, y, Border);
        }

        if (b.Stats.Count > 0)
        {
            _c.FillRect(Left, y, Width, statsH + (metaH > 0 ? metaH + 6 : 0), Surface);
            var gap = 6.0;
            var boxW = (Width - CardPad * 2 - gap * (b.Stats.Count - 1)) / b.Stats.Count;
            var x = Left + CardPad;
            foreach (var (label, value) in b.Stats)
            {
                _c.FillAndStrokeRect(x, y + 6, boxW, 28, White, Border, 0.5);
                _c.Text(label, x, y + 10, 7, PdfFont.Regular, TextQuaternary, PdfAlign.Center, boxW);
                _c.Text(_c.Ellipsize(value, boxW - 6, 12, PdfFont.Bold), x, y + 19, 12, PdfFont.Bold, Black, PdfAlign.Center, boxW);
                x += boxW + gap;
            }
            y += statsH;
        }

        if (metaLines.Count > 0)
        {
            y += metaPad;
            foreach (var meta in metaLines)
            {
                _c.Text(meta, Left + CardPad, y, 8, PdfFont.Regular, TextSecondary);
                y += 10.5;
            }
            y += 6;
        }

        if (rowLines.Count > 0)
        {
            y += 4;
            foreach (var (left, right) in rowLines)
            {
                _c.Text(_c.Ellipsize(left, Width * 0.45, 8, PdfFont.Bold), Left + CardPad, y, 8, PdfFont.Regular, Black);
                for (var i = 0; i < right.Count; i++)
                    _c.Text(right[i], Left, y + i * RowLineStep, 8, PdfFont.Regular, TextSecondary, PdfAlign.Right, Width - CardPad);
                y += RowHeightFor(right.Count);
            }
            y += 4;
        }

        foreach (var table in tables)
        {
            if (table.Caption is not null)
            {
                _c.Text(table.Caption.ToUpperInvariant(), Left + CardPad, y + 3, 6.5, PdfFont.Bold, TextTertiary);
                y += TableCaptionH;
            }
            DrawTableHeader(table, Width - CardPad * 2, ref y);
            foreach (var row in table.Rows.Select((cells, i) => (cells, i)))
                DrawTableRow(table, Width - CardPad * 2, row.cells, row.i, ref y);
            y += 4;
        }

        if (footerH > 0)
        {
            _c.FillRect(Left, y, Width, footerH, SurfaceMuted);
            _c.Line(Left, y, Left + Width, y, Border);
            _c.Text(_c.Ellipsize(b.FooterLeft, Width * 0.6, 8, PdfFont.Regular), Left + CardPad, y + 4, 8, PdfFont.Regular, TextSecondary);
            _c.Text(b.FooterRight, Left, y + 4, 8, PdfFont.Bold, tone, PdfAlign.Right, Width - CardPad);
        }

        // Stroked last so the header band and table fills, which span the
        // full width, cannot paint over the card's edge.
        _c.StrokeRect(Left, top, Width, total, BorderStrong);
        _y = top + total + BlockGap;
    }

    private static double RowHeightFor(int lines) => 11.5 + Math.Max(lines - 1, 0) * RowLineStep;

    // Wraps a value to its box, honouring explicit "\n" breaks. Each line
    // is still ellipsized, since one unbroken token (a long lot number)
    // can be wider than the box on its own; past MaxCellLines the last
    // line is ellipsized too.
    private List<string> WrapCell(string? text, double width, double size, PdfFont font)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;
        foreach (var part in text.Split('\n'))
            lines.AddRange(_c.WrapText(part, width, size, font).Select(l => _c.Ellipsize(l, width, size, font)));
        if (lines.Count > MaxCellLines)
        {
            lines = lines.Take(MaxCellLines).ToList();
            lines[^1] = _c.Ellipsize(lines[^1] + " ...", width, size, font);
        }
        return lines;
    }

    private static double[] ColumnWidths(ReportTable t, double usableWidth) =>
        t.Columns.Select(c => usableWidth * c.WidthFraction).ToArray();

    private ReportTone? CellTone(ReportTable t, int row, int col) =>
        t.CellTones is not null && row < t.CellTones.Count && col < t.CellTones[row].Count ? t.CellTones[row][col] : null;

    private List<List<string>> HeaderLines(ReportTable t, double usableWidth)
    {
        var widths = ColumnWidths(t, usableWidth);
        return t.Columns.Select((c, i) => WrapCell(c.Header.ToUpperInvariant(), widths[i] - 4, TableHeaderSize, PdfFont.Regular)).ToList();
    }

    private double HeaderHeight(ReportTable t, double usableWidth) =>
        TableHeaderRowH + Math.Max(HeaderLines(t, usableWidth).Max(l => l.Count) - 1, 0) * TableHeaderLineStep;

    private List<List<string>> RowLines(ReportTable t, double usableWidth, List<string> row, int rowIndex)
    {
        var widths = ColumnWidths(t, usableWidth);
        var result = new List<List<string>>();
        for (var i = 0; i < t.Columns.Count; i++)
        {
            var cell = i < row.Count ? row[i] : "";
            // Status cells carry the raw enum name (which picks the colour)
            // but print as English: "Action limit exceeded".
            if (i == t.StatusColumnIndex && Enum.TryParse<ResultStatus>(cell, out _)) cell = ReportVocabulary.Humanize(cell);
            var bold = i == t.StatusColumnIndex || CellTone(t, rowIndex, i) is not null;
            result.Add(WrapCell(cell, widths[i] - 4, TableCellSize, bold ? PdfFont.Bold : PdfFont.Regular));
        }
        return result;
    }

    private double TableRowHeight(ReportTable t, double usableWidth, List<string> row, int rowIndex) =>
        TableRowH + Math.Max(RowLines(t, usableWidth, row, rowIndex).Max(l => l.Count) - 1, 0) * TableLineStep;

    private double MeasureTable(ReportTable t, double usableWidth) =>
        HeaderHeight(t, usableWidth) + t.Rows.Select((r, i) => TableRowHeight(t, usableWidth, r, i)).Sum();

    // A real grid: header row + one row per data item, with every cell
    // wrapped in full rather than cut off - used for the per-location
    // breakdown, incubation stages, analysis parameters and readings.
    private void DrawTableHeader(ReportTable t, double usableWidth, ref double y)
    {
        var widths = ColumnWidths(t, usableWidth);
        var lines = HeaderLines(t, usableWidth);
        var h = HeaderHeight(t, usableWidth);
        _c.FillRect(Left, y, Width, h, SurfaceMuted);
        var x = Left + CardPad;
        for (var i = 0; i < t.Columns.Count; i++)
        {
            for (var l = 0; l < lines[i].Count; l++)
                _c.Text(lines[i][l], x, y + 4 + l * TableHeaderLineStep, TableHeaderSize, PdfFont.Regular, TextQuaternary);
            x += widths[i];
        }
        y += h;
        _c.Line(Left, y, Left + Width, y, Border);
    }

    private void DrawTableRow(ReportTable t, double usableWidth, List<string> row, int rowIndex, ref double y)
    {
        var widths = ColumnWidths(t, usableWidth);
        var lines = RowLines(t, usableWidth, row, rowIndex);
        var x = Left + CardPad;
        for (var i = 0; i < t.Columns.Count; i++)
        {
            var tone = CellTone(t, rowIndex, i);
            var isStatus = i == t.StatusColumnIndex;
            var color = tone is not null ? ToneColor(tone.Value) : isStatus ? LocationStatusColor(i < row.Count ? row[i] : "") : TextSecondary;
            var font = tone is not null || isStatus ? PdfFont.Bold : PdfFont.Regular;
            for (var l = 0; l < lines[i].Count; l++)
                _c.Text(lines[i][l], x, y + 3 + l * TableLineStep, TableCellSize, font, color);
            x += widths[i];
        }
        y += TableRowH + Math.Max(lines.Max(l => l.Count) - 1, 0) * TableLineStep;
        _c.Line(Left, y, Left + Width, y, Border, 0.4);
    }

    // A standalone table (results matrix, analysis readings). It may run
    // over several pages: rows are never split, and the column header is
    // repeated at the top of every continuation page.
    private void DrawTableBlock(TableBlock b)
    {
        var t = b.Table;
        var usable = Width - CardPad * 2;
        var titleH = 14.0 + (b.Summary is null ? 0 : 11);
        var firstRowH = t.Rows.Count > 0 ? TableRowHeight(t, usable, t.Rows[0], 0) : 0;
        EnsureSpace(titleH + HeaderHeight(t, usable) + firstRowH);

        _c.Text(b.Label.ToUpperInvariant(), Left, _y, 7, PdfFont.Regular, TextQuaternary);
        if (b.Summary is not null)
            _c.Text(_c.Ellipsize(b.Summary, Width * 0.7, 8, PdfFont.Regular), Left, _y + 11, 8, PdfFont.Regular, TextSecondary);
        if (!string.IsNullOrEmpty(b.BadgeText))
        {
            var badgeW = _c.MeasureText(b.BadgeText, 8, PdfFont.Bold) + 12;
            var badgeX = Left + Width - badgeW;
            _c.FillAndStrokeRect(badgeX, _y, badgeW, 15, White, ToneColor(b.BadgeTone), 0.75);
            _c.Text(b.BadgeText, badgeX, _y + 3.5, 8, PdfFont.Bold, ToneColor(b.BadgeTone), PdfAlign.Center, badgeW);
        }
        _y += titleH;

        var y = _y;
        DrawTableHeader(t, usable, ref y);
        for (var i = 0; i < t.Rows.Count; i++)
        {
            var rowH = TableRowHeight(t, usable, t.Rows[i], i);
            if (y + rowH > PageBottom)
            {
                _y = y;
                EnsureSpace(HeaderHeight(t, usable) + rowH);
                y = _y;
                DrawTableHeader(t, usable, ref y);
            }
            DrawTableRow(t, usable, t.Rows[i], i, ref y);
        }
        _y = y + 4;

        if (!string.IsNullOrEmpty(b.Legend))
        {
            EnsureSpace(12);
            _c.Text(b.Legend, Left, _y, 7, PdfFont.Regular, TextQuaternary);
            _y += 10;
        }
        _y += BlockGap;
    }

    // Same palette as the frontend's StatusBadge color map, for both the
    // CFU/count severity ladder and the pathogen Detected/Absent call.
    private static PdfColor LocationStatusColor(string cell) =>
        Enum.TryParse<ResultStatus>(cell, out var status) ? LocationStatusColor(status) : TextTertiary;

    private static PdfColor LocationStatusColor(ResultStatus status) => status switch
    {
        ResultStatus.WithinLimits or ResultStatus.Absent => Positive,
        ResultStatus.LimitsNotConfigured or ResultStatus.AlertLimitExceeded => Warning,
        ResultStatus.ActionLimitExceeded => ActionOrange,
        ResultStatus.OutOfSpecification or ResultStatus.Detected => Danger,
        _ => TextTertiary
    };

    private void DrawList(ListBlock b)
    {
        var rows = b.Rows.Select(r => (r.Left, Right: WrapCell(r.Right, Width * 0.5, 8, PdfFont.Regular))).ToList();
        var height = CardPad * 2 + (rows.Count == 0 ? 11.5 : rows.Sum(r => RowHeightFor(r.Right.Count)));

        EnsureSpace(height);
        _c.FillAndStrokeRect(Left, _y, Width, height, White, Border);

        var y = _y + CardPad;
        if (rows.Count == 0)
        {
            _c.Text(b.EmptyText, Left + CardPad, y, 8, PdfFont.Regular, TextQuaternary);
        }
        else
        {
            foreach (var (left, right) in rows)
            {
                _c.Text(_c.Ellipsize(left, Width * 0.42, 8, PdfFont.Bold), Left + CardPad, y, 8, PdfFont.Bold, Black);
                for (var i = 0; i < right.Count; i++)
                    _c.Text(right[i], Left, y + i * RowLineStep, 8, PdfFont.Regular, TextSecondary, PdfAlign.Right, Width - CardPad);
                y += RowHeightFor(right.Count);
            }
        }
        _y += height + BlockGap;
    }

    private void DrawSignatures(SignatureBlock b)
    {
        if (b.Signatures.Count == 0) return;

        var colW = (Width - BlockGap) / 2;
        for (var i = 0; i < b.Signatures.Count; i += 2)
        {
            var pair = b.Signatures.Skip(i).Take(2).ToList();
            var height = pair.Max(s => SignatureHeight(s, colW));

            EnsureSpace(height);
            for (var j = 0; j < pair.Count; j++)
                DrawSignatureCard(pair[j], Left + j * (colW + BlockGap), colW, height);
            _y += height + BlockGap;
        }
    }

    private double SignatureHeight(SignatureEntry s, double colW)
    {
        var meaningLines = _c.WrapText(s.Meaning, colW - CardPad * 2, 7, PdfFont.Regular).Count;
        var commentLines = string.IsNullOrWhiteSpace(s.Comment)
            ? 0 : _c.WrapText($"“{s.Comment}”", colW - CardPad * 2, 7, PdfFont.Regular).Count;
        return CardPad * 2 + 40 + meaningLines * 9 + commentLines * 9;
    }

    private void DrawSignatureCard(SignatureEntry s, double x, double w, double height)
    {
        _c.FillAndStrokeRect(x, _y, w, height, White, BorderStrong);

        var y = _y + CardPad;
        _c.Text(_c.Ellipsize(s.PrintedName, w - CardPad * 2, 10, PdfFont.Bold), x + CardPad, y, 10, PdfFont.Bold, Black);
        // The username is what proves two signatures came from different
        // people - full names collide across accounts.
        _c.Text($"@{s.Username}", x + CardPad, y + 12, 7, PdfFont.Regular, TextQuaternary);
        _c.Text(s.Role, x + CardPad, y + 21, 8, PdfFont.Regular, TextTertiary);
        _c.Text(s.SignedAt, x + CardPad, y + 31, 8, PdfFont.Regular, TextQuaternary);

        var textY = y + 42;
        textY += _c.TextWrapped(s.Meaning, x + CardPad, textY, w - CardPad * 2, 7, PdfFont.Regular, TextQuaternary, 9);
        if (!string.IsNullOrWhiteSpace(s.Comment))
            _c.TextWrapped($"“{s.Comment}”", x + CardPad, textY, w - CardPad * 2, 7, PdfFont.Regular, TextSecondary, 9);
    }

    // Sits just inside the bottom margin. Kept ~13mm clear of the paper
    // edge because most office printers cannot image closer than about
    // 10mm - any lower and the controlled-document notice risks being
    // clipped off the printed copy.
    private void DrawFooter(int pageNumber, int pageCount)
    {
        var y = PageBottom + 4;
        _c.Line(Left, y, Left + Width, y, BorderStrong);
        _c.Text("This is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled.",
            Left, y + 5, 7, PdfFont.Bold, TextTertiary, PdfAlign.Center, Width);
        _c.Text($"Document ID: {_documentId}  |  Generated: {DateTime.UtcNow:dd-MMM-yyyy HH:mm} UTC  |  Page {pageNumber} of {pageCount}",
            Left, y + 14, 7, PdfFont.Regular, TextQuaternary, PdfAlign.Center, Width);
    }
}
