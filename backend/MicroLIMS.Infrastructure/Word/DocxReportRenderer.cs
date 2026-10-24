using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Infrastructure.Word;

// Writes a ReportDocument as a Word (.docx) file: the same header, blocks
// and controlled-document footer as ReportPdfRenderer, but editable.
// SimpleDocxWriter remains the flat-text path; this is the laid-out one.
//
// Like PdfCanvas it is dependency-free - a .docx is a zip of a handful of
// XML parts, and owning ~400 lines of WordprocessingML is cheaper in a
// validated system than qualifying a document library on every update.
// Tables repeat their header row across pages; cards and strips become
// bordered tables; images are embedded PNGs at the content width.
public class DocxReportRenderer
{
    private const int PageWidthTwips = 11906;   // A4 portrait
    private const int PageHeightTwips = 16838;
    private const int MarginXTwips = 907;       // 16mm, as the PDF
    private const int ContentWidthTwips = PageWidthTwips - 2 * MarginXTwips;
    private const long EmuPerTwip = 635;
    private const long EmuPerPoint = 12700;

    private const string Black = "111111";
    private const string TextSecondary = "444444";
    private const string TextTertiary = "666666";
    private const string TextQuaternary = "888888";
    private const string Border = "BBBBBB";
    private const string SurfaceMuted = "F0F0F0";

    private readonly StringBuilder _body = new();
    private readonly List<byte[]> _images = new();
    private string _documentId = string.Empty;

    public byte[] Render(ReportDocument doc)
    {
        _documentId = doc.DocumentId;
        DrawHeader(doc);
        foreach (var block in doc.Blocks) DrawBlock(block);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", ContentTypes());
            Add(zip, "_rels/.rels", RootRels());
            Add(zip, "docProps/core.xml", CoreProperties($"{doc.Kind} - {doc.DocumentId}"));
            Add(zip, "word/document.xml", Document());
            Add(zip, "word/styles.xml", Styles());
            Add(zip, "word/footer1.xml", Footer());
            Add(zip, "word/_rels/document.xml.rels", DocumentRels());
            for (var i = 0; i < _images.Count; i++)
            {
                var entry = zip.CreateEntry($"word/media/image{i + 1}.png", CompressionLevel.NoCompression);
                using var s = entry.Open();
                s.Write(_images[i]);
            }
        }
        return ms.ToArray();
    }

    // ---- Blocks ---------------------------------------------------------

    private void DrawHeader(ReportDocument doc)
    {
        _body.Append(P(new[] { R(doc.Kind.ToUpperInvariant(), size: 7, color: TextQuaternary) }));
        _body.Append(P(new[] { R(doc.DocumentId, size: 22, bold: true, color: Black) }, after: 0));

        var subtitle = new List<string>();
        var idx = string.IsNullOrEmpty(doc.SubtitleEmphasis) ? -1 : doc.Subtitle.IndexOf(doc.SubtitleEmphasis, StringComparison.Ordinal);
        if (idx < 0) subtitle.Add(R(doc.Subtitle, size: 9, color: TextSecondary));
        else
        {
            subtitle.Add(R(doc.Subtitle[..idx], size: 9, color: TextSecondary));
            subtitle.Add(R(doc.SubtitleEmphasis!, size: 9, bold: true, color: Black));
            subtitle.Add(R(doc.Subtitle[(idx + doc.SubtitleEmphasis!.Length)..], size: 9, color: TextSecondary));
        }
        _body.Append(P(subtitle, after: 60));

        var badge = string.IsNullOrEmpty(doc.BadgeText) ? new List<string>() : new List<string> { R($" {doc.BadgeText} ", size: 9, bold: true, color: Black, shade: SurfaceMuted) };
        if (!string.IsNullOrEmpty(doc.HeaderNote)) badge.Add(R($"   {doc.HeaderNote}", size: 8, color: TextQuaternary));
        _body.Append(P(badge, after: 200, bottomBorder: true));
    }

    private void DrawBlock(ReportBlock block)
    {
        switch (block)
        {
            case HeadingBlock b: Label(b.Text, heading: true); break;
            case StripBlock b: DrawStrip(b); break;
            case TwoColumnBlock b: DrawTwoColumn(b); break;
            case TableBlock b: DrawTableBlock(b); break;
            case CardBlock b: DrawCard(b); break;
            case ListBlock b: DrawList(b); break;
            case SignatureBlock b: DrawSignatures(b); break;
            case ImageBlock b: DrawImage(b); break;
            case PageBreakBlock: _body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>"); break;
        }
    }

    private void Label(string text, bool heading = false) =>
        _body.Append(P(new[] { R(text.ToUpperInvariant(), size: heading ? 9 : 7, bold: heading, color: heading ? TextSecondary : TextQuaternary) },
            style: heading ? "Heading1" : null, before: heading ? 240 : 120, after: 60, keepNext: true, bottomBorder: heading));

    private void DrawStrip(StripBlock b)
    {
        Label(b.Label);
        var cells = b.Items.Select(i =>
        {
            var paras = new StringBuilder();
            paras.Append(P(new[] { R(i.Label, size: 7.5, color: TextTertiary) }, after: 0));
            paras.Append(P(new[] { R(i.Value, size: 9, bold: true, color: Black) }, after: 0));
            if (!string.IsNullOrEmpty(i.Sub)) paras.Append(P(new[] { R(i.Sub!, size: 7, color: TextQuaternary) }, after: 0));
            return paras.ToString();
        }).ToList();
        if (cells.Count == 0) return;
        var w = ContentWidthTwips / cells.Count;
        _body.Append(Table(Enumerable.Repeat(w, cells.Count).ToList(), new List<List<string>> { cells }, headerRows: 0));
        Spacer();
    }

    private void DrawTwoColumn(TwoColumnBlock b)
    {
        string Card(string label, List<(string Key, string Value)> pairs)
        {
            var sb = new StringBuilder(P(new[] { R(label.ToUpperInvariant(), size: 7, color: TextQuaternary) }, after: 40));
            foreach (var (k, v) in pairs)
                sb.Append(P(new[] { R($"{k}  ", size: 8.5, color: TextTertiary), R(v, size: 8.5, bold: true, color: Black) }, after: 20));
            return sb.ToString();
        }
        var half = ContentWidthTwips / 2;
        _body.Append(Table(new List<int> { half, half }, new List<List<string>> { new() { Card(b.LeftLabel, b.Left), Card(b.RightLabel, b.Right) } }, headerRows: 0));
        Spacer();
    }

    private void DrawTableBlock(TableBlock b)
    {
        var title = new List<string> { R(b.Label.ToUpperInvariant(), size: 7, color: TextQuaternary) };
        if (!string.IsNullOrEmpty(b.BadgeText)) title.Add(R($"   {b.BadgeText}", size: 8, bold: true, color: ToneHex(b.BadgeTone)));
        _body.Append(P(title, before: 120, after: 20, keepNext: true));
        if (b.Summary is not null) _body.Append(P(new[] { R(b.Summary, size: 8, color: TextSecondary) }, after: 60, keepNext: true));
        DrawReportTable(b.Table);
        if (!string.IsNullOrEmpty(b.Legend)) _body.Append(P(new[] { R(b.Legend!, size: 7, color: TextQuaternary) }, after: 60));
        Spacer();
    }

    private void DrawReportTable(ReportTable t)
    {
        if (t.Columns.Count == 0) return;
        if (!string.IsNullOrEmpty(t.Caption))
            _body.Append(P(new[] { R(t.Caption!, size: 7.5, bold: true, color: TextTertiary) }, before: 60, after: 20, keepNext: true));

        var totalFraction = t.Columns.Sum(c => c.WidthFraction);
        var widths = t.Columns.Select(c => (int)(ContentWidthTwips * (totalFraction > 0 ? c.WidthFraction / totalFraction : 1.0 / t.Columns.Count))).ToList();
        var rows = new List<List<string>>
        {
            t.Columns.Select(c => P(new[] { R(c.Header.ToUpperInvariant(), size: 6.5, color: TextQuaternary) }, after: 0)).ToList()
        };
        for (var r = 0; r < t.Rows.Count; r++)
        {
            var row = new List<string>();
            for (var c = 0; c < t.Columns.Count; c++)
            {
                var text = c < t.Rows[r].Count ? t.Rows[r][c] : string.Empty;
                var tone = t.CellTones is not null && r < t.CellTones.Count && c < t.CellTones[r].Count ? t.CellTones[r][c] : null;
                var isStatus = c == t.StatusColumnIndex;
                var color = tone is not null ? ToneHex(tone.Value) : isStatus ? StatusHex(text) : TextSecondary;
                row.Add(P(new[] { R(text, size: 7.5, bold: tone is not null || isStatus, color: color) }, after: 0));
            }
            rows.Add(row);
        }
        _body.Append(Table(widths, rows, headerRows: 1));
    }

    private void DrawCard(CardBlock b)
    {
        var head = new List<string> { R(b.Title, size: 10, bold: true, color: Black) };
        if (!string.IsNullOrEmpty(b.HeadlineValue))
            head.Add(R($"   {b.HeadlineValue} {b.HeadlineUnit}".TrimEnd(), size: 10, bold: true, color: ToneHex(b.Tone)));
        _body.Append(P(head, before: 120, after: 0, keepNext: true));
        if (!string.IsNullOrEmpty(b.Subtitle)) _body.Append(P(new[] { R(b.Subtitle, size: 8, color: TextTertiary) }, after: 60, keepNext: true));

        var pairs = b.Details.Concat(b.Stats).ToList();
        if (pairs.Count > 0)
        {
            var half = ContentWidthTwips / 2;
            _body.Append(Table(new List<int> { half, half },
                pairs.Select(p => new List<string> { P(new[] { R(p.Label, size: 8, color: TextTertiary) }, after: 0), P(new[] { R(p.Value, size: 8, bold: true, color: Black) }, after: 0) }).ToList(),
                headerRows: 0));
        }
        foreach (var line in b.MetaLines) _body.Append(P(new[] { R(line, size: 8, color: TextSecondary) }, after: 40));
        if (b.Rows.Count > 0)
        {
            var half = ContentWidthTwips / 2;
            _body.Append(Table(new List<int> { half, half },
                b.Rows.Select(r => new List<string> { P(new[] { R(r.Left, size: 8, bold: true, color: Black) }, after: 0), P(new[] { R(r.Right, size: 8, color: TextSecondary) }, after: 0, align: "right") }).ToList(),
                headerRows: 0));
        }
        if (b.TableColumns.Count > 0)
            DrawReportTable(new ReportTable { Columns = b.TableColumns, Rows = b.TableRows, StatusColumnIndex = b.TableStatusColumnIndex });
        foreach (var t in b.Tables) DrawReportTable(t);
        if (!string.IsNullOrEmpty(b.FooterLeft) || !string.IsNullOrEmpty(b.FooterRight))
            _body.Append(P(new[] { R($"{b.FooterLeft}   {b.FooterRight}".Trim(), size: 7, color: TextQuaternary) }, after: 60));
        Spacer();
    }

    private void DrawList(ListBlock b)
    {
        if (!string.IsNullOrEmpty(b.Label)) Label(b.Label);
        if (b.Rows.Count == 0)
        {
            _body.Append(P(new[] { R(b.EmptyText, size: 8, color: TextQuaternary) }, after: 120));
            return;
        }
        var half = ContentWidthTwips / 2;
        _body.Append(Table(new List<int> { half, half },
            b.Rows.Select(r => new List<string> { P(new[] { R(r.Left, size: 8, bold: true, color: Black) }, after: 0), P(new[] { R(r.Right, size: 8, color: TextSecondary) }, after: 0, align: "right") }).ToList(),
            headerRows: 0));
        Spacer();
    }

    private void DrawSignatures(SignatureBlock b)
    {
        if (b.Signatures.Count == 0) return;
        var rows = b.Signatures.Select(s =>
        {
            var who = new StringBuilder();
            who.Append(P(new[] { R(s.PrintedName, size: 10, bold: true, color: Black) }, after: 0));
            who.Append(P(new[] { R($"@{s.Username}", size: 7, color: TextQuaternary) }, after: 0));
            who.Append(P(new[] { R(s.Role, size: 8, color: TextTertiary) }, after: 0));
            who.Append(P(new[] { R(s.SignedAt, size: 8, color: TextQuaternary) }, after: 0));
            var what = new StringBuilder(P(new[] { R(s.Meaning, size: 7.5, color: TextQuaternary) }, after: 40));
            if (!string.IsNullOrWhiteSpace(s.Comment)) what.Append(P(new[] { R($"“{s.Comment}”", size: 7.5, color: TextSecondary) }, after: 0));
            return new List<string> { who.ToString(), what.ToString() };
        }).ToList();
        _body.Append(Table(new List<int> { ContentWidthTwips * 2 / 5, ContentWidthTwips * 3 / 5 }, rows, headerRows: 0, borderColor: "000000"));
        Spacer();
    }

    // Content width, never enlarged past 2 px per point (the scale the
    // charts are captured at), and capped so a tall image fits a page.
    private void DrawImage(ImageBlock b)
    {
        if (!PngHeader.TryReadSize(b.Png, out var pxW, out var pxH))
            throw new InvalidOperationException("The chart image is not a valid PNG.");
        _images.Add(b.Png);
        var n = _images.Count;

        var maxW = ContentWidthTwips * EmuPerTwip;
        var maxH = (PageHeightTwips - 1020 - 1247 - 900) * EmuPerTwip;
        var cx = Math.Min(maxW, (long)(pxW / 2.0 * EmuPerPoint));
        var cy = cx * pxH / pxW;
        if (cy > maxH) { cy = maxH; cx = cy * pxW / pxH; }

        var drawing =
            "<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">" +
            $"<wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:docPr id=\"{n}\" name=\"Chart {n}\"/>" +
            "<wp:cNvGraphicFramePr><a:graphicFrameLocks xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>" +
            "<a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">" +
            "<pic:pic xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">" +
            $"<pic:nvPicPr><pic:cNvPr id=\"{n}\" name=\"image{n}.png\"/><pic:cNvPicPr/></pic:nvPicPr>" +
            $"<pic:blipFill><a:blip r:embed=\"rIdImg{n}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>" +
            $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>" +
            "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        _body.Append(P(new[] { drawing }, align: "center", after: 40, keepNext: !string.IsNullOrWhiteSpace(b.Caption)));
        if (!string.IsNullOrWhiteSpace(b.Caption))
            _body.Append(P(new[] { R(b.Caption!, size: 7.5, italic: true, color: TextTertiary) }, align: "center", style: "Caption", after: 160));
    }

    private void Spacer() => _body.Append(P(Array.Empty<string>(), after: 120));

    // ---- WordprocessingML primitives ----------------------------------

    private static string X(string s) => SecurityElement.Escape(s) ?? string.Empty;

    private static string HalfPoints(double pt) => ((int)Math.Round(pt * 2)).ToString(CultureInfo.InvariantCulture);

    // A run; "\n" becomes a line break so multi-line cells survive.
    private static string R(string text, double size = 9, bool bold = false, bool italic = false, string color = Black, string? shade = null)
    {
        var props = new StringBuilder("<w:rPr>");
        if (bold) props.Append("<w:b/>");
        if (italic) props.Append("<w:i/>");
        props.Append($"<w:color w:val=\"{color}\"/><w:sz w:val=\"{HalfPoints(size)}\"/><w:szCs w:val=\"{HalfPoints(size)}\"/>");
        if (shade is not null) props.Append($"<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{shade}\"/>");
        props.Append("</w:rPr>");

        var parts = (text ?? string.Empty).Split('\n');
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append($"<w:r>{props}<w:br/></w:r>");
            sb.Append($"<w:r>{props}<w:t xml:space=\"preserve\">{X(parts[i])}</w:t></w:r>");
        }
        return sb.ToString();
    }

    private static string P(IEnumerable<string> runs, string? style = null, string? align = null, int before = 0, int after = 80,
        bool keepNext = false, bool bottomBorder = false)
    {
        var sb = new StringBuilder("<w:p><w:pPr>");
        if (style is not null) sb.Append($"<w:pStyle w:val=\"{style}\"/>");
        if (keepNext) sb.Append("<w:keepNext/>");
        if (bottomBorder) sb.Append($"<w:pBdr><w:bottom w:val=\"single\" w:sz=\"8\" w:space=\"4\" w:color=\"{(style is null ? "000000" : Border)}\"/></w:pBdr>");
        sb.Append($"<w:spacing w:before=\"{before}\" w:after=\"{after}\"/>");
        if (align is not null) sb.Append($"<w:jc w:val=\"{align}\"/>");
        sb.Append("</w:pPr>");
        foreach (var r in runs) sb.Append(r);
        sb.Append("</w:p>");
        return sb.ToString();
    }

    // Cells hold ready-made paragraphs. Header rows repeat on every page
    // and are shaded; rows never split across a page.
    private static string Table(List<int> widths, List<List<string>> rows, int headerRows, string borderColor = Border)
    {
        var sb = new StringBuilder("<w:tbl><w:tblPr>");
        // CT_TblPr order: tblW, tblBorders, tblLayout, tblCellMar.
        sb.Append($"<w:tblW w:w=\"{widths.Sum()}\" w:type=\"dxa\"/>");
        sb.Append("<w:tblBorders>");
        foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
            sb.Append($"<w:{side} w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"{borderColor}\"/>");
        sb.Append("</w:tblBorders><w:tblLayout w:type=\"fixed\"/><w:tblCellMar><w:top w:w=\"60\" w:type=\"dxa\"/><w:left w:w=\"100\" w:type=\"dxa\"/><w:bottom w:w=\"60\" w:type=\"dxa\"/><w:right w:w=\"100\" w:type=\"dxa\"/></w:tblCellMar>");
        sb.Append("</w:tblPr><w:tblGrid>");
        foreach (var w in widths) sb.Append($"<w:gridCol w:w=\"{w}\"/>");
        sb.Append("</w:tblGrid>");

        for (var r = 0; r < rows.Count; r++)
        {
            var header = r < headerRows;
            sb.Append("<w:tr><w:trPr><w:cantSplit/>");
            if (header) sb.Append("<w:tblHeader/>");
            sb.Append("</w:trPr>");
            for (var c = 0; c < widths.Count; c++)
            {
                var content = c < rows[r].Count && !string.IsNullOrEmpty(rows[r][c]) ? rows[r][c] : "<w:p/>";
                sb.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{widths[c]}\" w:type=\"dxa\"/>");
                if (header) sb.Append($"<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{SurfaceMuted}\"/>");
                sb.Append($"</w:tcPr>{content}</w:tc>");
            }
            sb.Append("</w:tr>");
        }
        sb.Append("</w:tbl>");
        return sb.ToString();
    }

    private static string ToneHex(ReportTone tone) => tone switch
    {
        ReportTone.Positive => "16A34A",
        ReportTone.Danger => "DC2626",
        ReportTone.Warning => "D97706",
        _ => TextTertiary
    };

    // Same palette as ReportPdfRenderer.LocationStatusColor.
    private static string StatusHex(string cell) =>
        Enum.TryParse<ResultStatus>(cell, out var status)
            ? status switch
            {
                ResultStatus.WithinLimits or ResultStatus.Absent => "16A34A",
                ResultStatus.LimitsNotConfigured or ResultStatus.AlertLimitExceeded => "D97706",
                ResultStatus.ActionLimitExceeded => "EA580C",
                ResultStatus.OutOfSpecification or ResultStatus.Detected => "DC2626",
                _ => TextTertiary
            }
            : TextTertiary;

    // ---- Package parts --------------------------------------------------

    private static void Add(ZipArchive zip, string path, string xml)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(new UTF8Encoding(false).GetBytes(xml));
    }

    private const string XmlDecl = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
    private const string WNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string RNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private string Document() =>
        XmlDecl +
        $"<w:document xmlns:w=\"{WNs}\" xmlns:r=\"{RNs}\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\">" +
        $"<w:body>{_body}" +
        "<w:sectPr><w:footerReference w:type=\"default\" r:id=\"rIdFooter\"/>" +
        $"<w:pgSz w:w=\"{PageWidthTwips}\" w:h=\"{PageHeightTwips}\"/>" +
        $"<w:pgMar w:top=\"1020\" w:right=\"{MarginXTwips}\" w:bottom=\"1247\" w:left=\"{MarginXTwips}\" w:header=\"567\" w:footer=\"567\" w:gutter=\"0\"/>" +
        "</w:sectPr></w:body></w:document>";

    private string Footer()
    {
        static string Field(string instr) =>
            "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>" +
            $"<w:r><w:instrText xml:space=\"preserve\"> {instr} </w:instrText></w:r>" +
            "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
        var generated = DateTime.UtcNow.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture);
        return XmlDecl +
            $"<w:ftr xmlns:w=\"{WNs}\" xmlns:r=\"{RNs}\">" +
            P(new[] { R("This is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled.", size: 7, bold: true, color: TextTertiary) },
                align: "center", after: 0, bottomBorder: false) +
            "<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\"/><w:jc w:val=\"center\"/></w:pPr>" +
            R($"Document ID: {_documentId}  |  Generated: {generated} UTC  |  Page ", size: 7, color: TextQuaternary) +
            Field("PAGE") + R(" of ", size: 7, color: TextQuaternary) + Field("NUMPAGES") +
            "</w:p></w:ftr>";
    }

    private static string Styles() =>
        XmlDecl +
        $"<w:styles xmlns:w=\"{WNs}\">" +
        "<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\" w:cs=\"Arial\" w:eastAsia=\"Arial\"/>" +
        "<w:sz w:val=\"18\"/><w:szCs w:val=\"18\"/><w:lang w:val=\"en-GB\"/></w:rPr></w:rPrDefault>" +
        "<w:pPrDefault><w:pPr><w:spacing w:after=\"80\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>" +
        "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/>" +
        "<w:qFormat/><w:pPr><w:keepNext/><w:outlineLvl w:val=\"0\"/></w:pPr></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Caption\"><w:name w:val=\"caption\"/><w:basedOn w:val=\"Normal\"/><w:qFormat/></w:style>" +
        "</w:styles>";

    private string ContentTypes() =>
        XmlDecl +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Default Extension=\"png\" ContentType=\"image/png\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>" +
        "<Override PartName=\"/word/footer1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/>" +
        "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
        "</Types>";

    private static string RootRels() =>
        XmlDecl +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
        "</Relationships>";

    private string DocumentRels()
    {
        var sb = new StringBuilder(XmlDecl + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        sb.Append("<Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        sb.Append("<Relationship Id=\"rIdFooter\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer\" Target=\"footer1.xml\"/>");
        for (var i = 1; i <= _images.Count; i++)
            sb.Append($"<Relationship Id=\"rIdImg{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/image{i}.png\"/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string CoreProperties(string title) =>
        XmlDecl +
        "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
        "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
        $"<dc:title>{X(title)}</dc:title><dc:creator>MicroLIMS</dc:creator>" +
        $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created>" +
        "</cp:coreProperties>";
}
