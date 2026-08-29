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
        }
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
        EnsureSpace(18);
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
        var metaH = b.MetaLines.Count * 10.5;
        var rowsH = b.Rows.Count > 0 ? b.Rows.Count * 11.5 + 8 : 0;
        var tableH = b.TableRows.Count > 0 ? TableHeaderRowH + b.TableRows.Count * TableRowH + 8 : 0;
        var footerH = string.IsNullOrEmpty(b.FooterLeft) && string.IsNullOrEmpty(b.FooterRight) ? 0 : 16.0;
        var total = headerH + detailH + statsH + (metaH > 0 ? metaH + 6 : 0) + rowsH + tableH + footerH;

        EnsureSpace(total);
        var top = _y;
        _c.StrokeRect(Left, top, Width, total, BorderStrong);

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

        if (b.MetaLines.Count > 0)
        {
            foreach (var meta in b.MetaLines)
            {
                _c.Text(_c.Ellipsize(meta, Width - CardPad * 2, 8, PdfFont.Regular), Left + CardPad, y, 8, PdfFont.Regular, TextSecondary);
                y += 10.5;
            }
            y += 6;
        }

        if (b.Rows.Count > 0)
        {
            y += 4;
            foreach (var (left, right) in b.Rows)
            {
                _c.Text(_c.Ellipsize(left, Width * 0.45, 8, PdfFont.Bold), Left + CardPad, y, 8, PdfFont.Regular, Black);
                _c.Text(_c.Ellipsize(right, Width * 0.48, 8, PdfFont.Regular), Left, y, 8, PdfFont.Regular, TextSecondary, PdfAlign.Right, Width - CardPad);
                y += 11.5;
            }
            y += 4;
        }

        if (b.TableRows.Count > 0)
            DrawTable(b, ref y);

        if (footerH > 0)
        {
            _c.FillRect(Left, y, Width, footerH, SurfaceMuted);
            _c.Line(Left, y, Left + Width, y, Border);
            _c.Text(_c.Ellipsize(b.FooterLeft, Width * 0.6, 8, PdfFont.Regular), Left + CardPad, y + 4, 8, PdfFont.Regular, TextSecondary);
            _c.Text(b.FooterRight, Left, y + 4, 8, PdfFont.Bold, tone, PdfAlign.Right, Width - CardPad);
        }

        _y = top + total + BlockGap;
    }

    // A real grid: header row + one row per data item, evenly bordered
    // columns - used for the EM/After Cleaning per-location breakdown,
    // where the flat Rows list-of-pairs can't show several fields side by
    // side per location.
    private void DrawTable(CardBlock b, ref double y)
    {
        var usableWidth = Width - CardPad * 2;
        var colX = new double[b.TableColumns.Count];
        var colW = new double[b.TableColumns.Count];
        var x = Left + CardPad;
        for (var i = 0; i < b.TableColumns.Count; i++)
        {
            colX[i] = x;
            colW[i] = usableWidth * b.TableColumns[i].WidthFraction;
            x += colW[i];
        }

        _c.FillRect(Left, y, Width, TableHeaderRowH, SurfaceMuted);
        for (var i = 0; i < b.TableColumns.Count; i++)
            _c.Text(b.TableColumns[i].Header.ToUpperInvariant(), colX[i], y + 4, 6.5, PdfFont.Regular, TextQuaternary);
        y += TableHeaderRowH;
        _c.Line(Left, y, Left + Width, y, Border);

        foreach (var row in b.TableRows)
        {
            for (var i = 0; i < b.TableColumns.Count && i < row.Count; i++)
            {
                var cell = row[i];
                if (i == b.TableStatusColumnIndex)
                    _c.Text(_c.Ellipsize(cell, colW[i] - 4, 7.5, PdfFont.Bold), colX[i], y + 3, 7.5, PdfFont.Bold, LocationStatusColor(cell));
                else
                    _c.Text(_c.Ellipsize(cell, colW[i] - 4, 7.5, PdfFont.Regular), colX[i], y + 3, 7.5, PdfFont.Regular, TextSecondary);
            }
            y += TableRowH;
            _c.Line(Left, y, Left + Width, y, Border, 0.4);
        }
        y += 4;
    }

    // Same palette as the frontend's StatusBadge color map, for both the
    // CFU/count severity ladder and the pathogen Detected/Absent call.
    private static PdfColor LocationStatusColor(string status) => status switch
    {
        "WithinLimits" or "Absent" => Positive,
        "LimitsNotConfigured" or "AlertLimitExceeded" => Warning,
        "ActionLimitExceeded" => ActionOrange,
        "OutOfSpecification" or "Detected" => Danger,
        _ => TextTertiary
    };

    private void DrawList(ListBlock b)
    {
        var rows = b.Rows.Count == 0 ? 1 : b.Rows.Count;
        var height = CardPad * 2 + rows * 11.5;

        EnsureSpace(height);
        _c.FillAndStrokeRect(Left, _y, Width, height, White, Border);

        var y = _y + CardPad;
        if (b.Rows.Count == 0)
        {
            _c.Text(b.EmptyText, Left + CardPad, y, 8, PdfFont.Regular, TextQuaternary);
        }
        else
        {
            foreach (var (left, right) in b.Rows)
            {
                _c.Text(_c.Ellipsize(left, Width * 0.42, 8, PdfFont.Bold), Left + CardPad, y, 8, PdfFont.Bold, Black);
                _c.Text(_c.Ellipsize(right, Width * 0.5, 8, PdfFont.Regular), Left, y, 8, PdfFont.Regular, TextSecondary, PdfAlign.Right, Width - CardPad);
                y += 11.5;
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
