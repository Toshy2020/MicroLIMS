using System.Globalization;
using System.Text;

namespace MicroLIMS.Infrastructure.Pdf;

public enum PdfFont { Regular, Bold }
public enum PdfAlign { Left, Center, Right }

public readonly record struct PdfColor(double R, double G, double B)
{
    public static PdfColor FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return new PdfColor(
            Convert.ToInt32(hex[..2], 16) / 255.0,
            Convert.ToInt32(hex.Substring(2, 2), 16) / 255.0,
            Convert.ToInt32(hex.Substring(4, 2), 16) / 255.0);
    }
    public string Fill => $"{F(R)} {F(G)} {F(B)} rg";
    public string Stroke => $"{F(R)} {F(G)} {F(B)} RG";
    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

// Low-level drawing surface over a real PDF content stream: filled and
// stroked rectangles, and text in the base-14 Helvetica faces with
// accurate width measurement.
//
// Deliberately dependency-free. This is a validated GMP system, so every
// third-party binary added here becomes something that has to be
// qualified and re-qualified on each update - the cost of owning this
// layout code is lower than the cost of carrying a PDF engine or a
// headless browser through that process.
//
// Coordinates are given top-left origin, y growing downward (the way the
// layout code thinks); the canvas flips them into PDF's bottom-left space.
public class PdfCanvas
{
    public const double PageWidth = 595.276;   // A4 portrait, points
    public const double PageHeight = 841.890;
    public const double MarginX = 45.4;        // 16mm
    public const double MarginTop = 51.0;      // 18mm
    public const double MarginBottom = 62.4;   // 22mm
    public static double ContentWidth => PageWidth - 2 * MarginX;

    private readonly List<StringBuilder> _pages = new();
    private StringBuilder _current = null!;

    public PdfCanvas() => NewPage();

    public int PageCount => _pages.Count;

    public void NewPage()
    {
        _current = new StringBuilder();
        _pages.Add(_current);
    }

    // Lets the renderer go back and stamp each page once the total page
    // count is known - a controlled document should say "Page 1 of 3",
    // which cannot be written while the pages are still being filled.
    public void SelectPage(int index) => _current = _pages[index];

    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static double ToPdfY(double top) => PageHeight - top;

    public void FillRect(double x, double top, double w, double h, PdfColor fill)
    {
        _current.AppendLine("q");
        _current.AppendLine(fill.Fill);
        _current.AppendLine($"{N(x)} {N(ToPdfY(top + h))} {N(w)} {N(h)} re f");
        _current.AppendLine("Q");
    }

    public void StrokeRect(double x, double top, double w, double h, PdfColor stroke, double lineWidth = 0.75)
    {
        _current.AppendLine("q");
        _current.AppendLine(stroke.Stroke);
        _current.AppendLine($"{N(lineWidth)} w");
        _current.AppendLine($"{N(x)} {N(ToPdfY(top + h))} {N(w)} {N(h)} re S");
        _current.AppendLine("Q");
    }

    public void FillAndStrokeRect(double x, double top, double w, double h, PdfColor fill, PdfColor stroke, double lineWidth = 0.75)
    {
        FillRect(x, top, w, h, fill);
        StrokeRect(x, top, w, h, stroke, lineWidth);
    }

    public void Line(double x1, double top1, double x2, double top2, PdfColor stroke, double lineWidth = 0.75)
    {
        _current.AppendLine("q");
        _current.AppendLine(stroke.Stroke);
        _current.AppendLine($"{N(lineWidth)} w");
        _current.AppendLine($"{N(x1)} {N(ToPdfY(top1))} m {N(x2)} {N(ToPdfY(top2))} l S");
        _current.AppendLine("Q");
    }

    // `top` is the top of the line box; the text baseline is placed at
    // top + size, which approximates Helvetica's ascent closely enough
    // for this layout.
    public void Text(string text, double x, double top, double size, PdfFont font, PdfColor color, PdfAlign align = PdfAlign.Left, double? boxWidth = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        var width = MeasureText(text, size, font);
        var drawX = align switch
        {
            PdfAlign.Center => x + ((boxWidth ?? 0) - width) / 2,
            PdfAlign.Right => x + (boxWidth ?? 0) - width,
            _ => x
        };

        _current.AppendLine("q");
        _current.AppendLine(color.Fill);
        _current.AppendLine("BT");
        _current.AppendLine($"/{(font == PdfFont.Bold ? "F2" : "F1")} {N(size)} Tf");
        _current.AppendLine($"{N(drawX)} {N(ToPdfY(top + size))} Td");
        _current.AppendLine($"({EscapeWinAnsi(text)}) Tj");
        _current.AppendLine("ET");
        _current.AppendLine("Q");
    }

    // Draws text wrapped to boxWidth and returns the height consumed, so
    // callers can lay out around variable-length content (observations,
    // comments) without guessing.
    public double TextWrapped(string text, double x, double top, double boxWidth, double size, PdfFont font, PdfColor color, double lineHeight)
    {
        var lines = WrapText(text, boxWidth, size, font);
        for (var i = 0; i < lines.Count; i++)
            Text(lines[i], x, top + i * lineHeight, size, font, color);
        return lines.Count * lineHeight;
    }

    public List<string> WrapText(string text, double boxWidth, double size, PdfFont font)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (MeasureText(candidate, size, font) > boxWidth && line.Length > 0)
            {
                result.Add(line.ToString());
                line.Clear().Append(word);
            }
            else
            {
                line.Clear().Append(candidate);
            }
        }
        if (line.Length > 0) result.Add(line.ToString());
        return result;
    }

    // Truncates with an ellipsis so a long value can never overrun its
    // column and collide with the next one.
    public string Ellipsize(string text, double boxWidth, double size, PdfFont font)
    {
        if (string.IsNullOrEmpty(text) || MeasureText(text, size, font) <= boxWidth) return text;
        var trimmed = text;
        while (trimmed.Length > 1 && MeasureText(trimmed + "...", size, font) > boxWidth)
            trimmed = trimmed[..^1];
        return trimmed + "...";
    }

    public double MeasureText(string text, double size, PdfFont font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var widths = font == PdfFont.Bold ? HelveticaBoldWidths : HelveticaWidths;
        double total = 0;
        foreach (var ch in text)
        {
            var b = ToWinAnsiByte(ch);
            total += b >= 32 && b <= 126 ? widths[b - 32] : WideCharWidth(b);
        }
        return total * size / 1000.0;
    }

    private static double WideCharWidth(int b) => b switch
    {
        183 => 278,  // middle dot
        176 => 400,  // degree
        151 => 1000, // em dash
        150 => 556,  // en dash
        _ => 556
    };

    // The report text uses typographic characters (·, —, °, curly quotes)
    // that are not ASCII. The fonts are declared WinAnsiEncoding, so map
    // those to their WinAnsi byte and fall back to a plain ASCII
    // substitute for anything outside it rather than emitting garbage.
    private static int ToWinAnsiByte(char ch) => ch switch
    {
        '·' => 183,
        '°' => 176,
        '—' => 151,
        '–' => 150,
        '“' => 147,
        '”' => 148,
        '‘' => 145,
        '’' => 146,
        '…' => 133,
        _ => ch <= 255 ? ch : '?'
    };

    private static string EscapeWinAnsi(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            var b = ToWinAnsiByte(ch);
            if (b == '(' || b == ')' || b == '\\') sb.Append('\\').Append((char)b);
            else if (b < 32 || b > 126) sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
            else sb.Append((char)b);
        }
        return sb.ToString();
    }

    public byte[] Build(string title)
    {
        var objects = new List<string>();
        var streams = new Dictionary<int, byte[]>();

        var catalogIdx = objects.Count; objects.Add("");
        var pagesIdx = objects.Count; objects.Add("");
        var fontRegularIdx = objects.Count; objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var fontBoldIdx = objects.Count; objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        var pageIds = new List<int>();
        foreach (var page in _pages)
        {
            var bytes = Encoding.ASCII.GetBytes(page.ToString());
            var pageIdx = objects.Count; objects.Add("");
            var contentIdx = objects.Count; objects.Add($"<< /Length {bytes.Length} >>");
            streams[contentIdx] = bytes;

            objects[pageIdx] =
                $"<< /Type /Page /Parent {pagesIdx + 1} 0 R /MediaBox [0 0 {N(PageWidth)} {N(PageHeight)}] " +
                $"/Resources << /Font << /F1 {fontRegularIdx + 1} 0 R /F2 {fontBoldIdx + 1} 0 R >> >> " +
                $"/Contents {contentIdx + 1} 0 R >>";
            pageIds.Add(pageIdx + 1);
        }

        objects[catalogIdx] = $"<< /Type /Catalog /Pages {pagesIdx + 1} 0 R >>";
        objects[pagesIdx] = $"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(i => $"{i} 0 R"))}] /Count {pageIds.Count} >>";

        var infoIdx = objects.Count;
        objects.Add($"<< /Title ({EscapeWinAnsi(title)}) /Producer (MicroLIMS) /CreationDate (D:{DateTime.UtcNow:yyyyMMddHHmmss}Z) >>");

        using var ms = new MemoryStream();
        void Write(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        Write("%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\n");
            if (streams.TryGetValue(i, out var content))
            {
                Write("stream\n");
                ms.Write(content);
                Write("\nendstream\n");
            }
            Write("endobj\n");
        }

        var xrefStart = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (var i = 1; i <= objects.Count; i++) Write($"{offsets[i]:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root {catalogIdx + 1} 0 R /Info {infoIdx + 1} 0 R >>\nstartxref\n{xrefStart}\n%%EOF");

        return ms.ToArray();
    }

    // Adobe base-14 Helvetica advance widths (units per 1000 em) for
    // ASCII 32-126. Needed for centring, right-alignment and wrapping -
    // without real metrics every alignment would drift.
    private static readonly int[] HelveticaWidths =
    {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,
        556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
        1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
        333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,
        556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
    };

    private static readonly int[] HelveticaBoldWidths =
    {
        278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,
        556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
        975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
        333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,
        611,611,389,556,333,611,556,778,556,556,500,389,280,389,584
    };
}
