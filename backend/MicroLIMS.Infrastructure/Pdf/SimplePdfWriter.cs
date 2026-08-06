using System.Text;

namespace MicroLIMS.Infrastructure.Pdf;

// A dependency-free minimal PDF writer. It produces a genuinely valid
// PDF (opens in any reader) from a list of text lines - not a
// full-fidelity layout engine, but real PDF bytes rather than a plain
// text file with a .pdf extension. Swap for QuestPDF/iText later if
// richer layout (tables, logos) is required; ReportService is already
// isolated behind IPdfGenerator so that swap is a one-file change.
public static class SimplePdfWriter
{
    private const int PageWidth = 612;   // Letter size in points
    private const int PageHeight = 792;
    private const int LeftMargin = 50;
    private const int TopMargin = 740;
    private const int BottomMargin = 40;
    private const int LineHeight = 16;

    // Paginates automatically instead of truncating - a report with more
    // lines than fit on one page continues onto additional Page objects
    // rather than silently dropping content.
    public static byte[] WriteTextDocument(string title, IEnumerable<string> lines)
    {
        var pageContents = BuildPageContentStreams(title, lines);

        // Object numbering: 1=Catalog, 2=Pages, 3=Font, then one Page +
        // one Contents object per page, interleaved (4,5), (6,7), ...
        var objects = new List<string>();
        var pageObjectIds = new List<int>();
        var contentBytesByObjectIndex = new Dictionary<int, byte[]>();

        var catalogIndex = objects.Count; objects.Add(""); // placeholder, filled after we know Pages id
        var pagesIndex = objects.Count; objects.Add("");
        var fontIndex = objects.Count; objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        foreach (var content in pageContents)
        {
            var pageIndex = objects.Count;
            objects.Add(""); // filled below once we know the content object's id
            var contentIndex = objects.Count;
            objects.Add($"<< /Length {content.Length} >>");
            contentBytesByObjectIndex[contentIndex] = content;

            objects[pageIndex] =
                $"<< /Type /Page /Parent {pagesIndex + 1} 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] " +
                $"/Resources << /Font << /F1 {fontIndex + 1} 0 R >> >> /Contents {contentIndex + 1} 0 R >>";
            pageObjectIds.Add(pageIndex + 1);
        }

        objects[catalogIndex] = $"<< /Type /Catalog /Pages {pagesIndex + 1} 0 R >>";
        objects[pagesIndex] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>";

        using var ms = new MemoryStream();
        void Write(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        Write("%PDF-1.4\n");
        var offsets = new List<long> { 0 };

        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\n");
            if (contentBytesByObjectIndex.TryGetValue(i, out var contentBytes))
            {
                Write("stream\n");
                ms.Write(contentBytes);
                Write("\nendstream\n");
            }
            Write("endobj\n");
        }

        var xrefStart = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        for (int i = 1; i <= objects.Count; i++)
            Write($"{offsets[i]:D10} 00000 n \n");

        Write($"trailer\n<< /Size {objects.Count + 1} /Root {catalogIndex + 1} 0 R >>\nstartxref\n{xrefStart}\n%%EOF");

        return ms.ToArray();
    }

    private static List<byte[]> BuildPageContentStreams(string title, IEnumerable<string> lines)
    {
        var pages = new List<byte[]>();
        var content = new StringBuilder();
        var y = TopMargin;

        void StartPage(bool withTitle)
        {
            content.Clear();
            content.AppendLine("BT");
            if (withTitle)
            {
                content.AppendLine("/F1 14 Tf");
                content.AppendLine($"{LeftMargin} {TopMargin} Td");
                content.AppendLine($"({Escape(title)}) Tj");
                content.AppendLine("/F1 10 Tf");
                y = TopMargin - LineHeight * 2;
            }
            else
            {
                content.AppendLine("/F1 10 Tf");
                y = TopMargin;
            }
        }

        void EndPage()
        {
            content.AppendLine("ET");
            pages.Add(Encoding.ASCII.GetBytes(content.ToString()));
        }

        StartPage(withTitle: true);
        foreach (var line in lines)
        {
            if (y < BottomMargin)
            {
                EndPage();
                StartPage(withTitle: false);
            }

            content.AppendLine($"1 0 0 1 {LeftMargin} {y} Tm");
            content.AppendLine($"({Escape(line)}) Tj");
            y -= LineHeight;
        }
        EndPage();

        return pages;
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
