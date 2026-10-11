using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MicroLIMS.Application.Services.InstrumentImport;
using SkiaSharp;

namespace MicroLIMS.Infrastructure.InstrumentImport;

// OCR of one report page with the Tesseract CLI (apt tesseract-ocr in the
// Docker image; UB-Mannheim build on Windows).
//
// A single pass over a phone photo misreads small numbers (Injection # "1"
// read as "4") and misses the boxed peak table, so reading is two-stage:
//  1. locate: the whole page (psm 11 - sparse text; psm 4's layout analysis
//     dropped a whole header row on one photo) and
//     the peak-table region - the band between the chromatogram's "Minutes"
//     axis title and the "Reported by" footer (psm 6) - give word boxes,
//     and the page pass's own reading of each row is the cross-check pass;
//  2. read: the words are grouped into rows, each row's pieces cropped
//     tight, enlarged at two scales, binarised (Otsu, ruled lines erased)
//     and read alone (psm 7); the two crops and the page pass vote.
// Whatever wins, every OCR value still needs the analyst's check
// (EmpowerReport.RequiresVerification).
public sealed class TesseractOcr(string tesseractPath, string workDirectory)
{
    private const int TargetPageWidth = 2400;
    private const int TargetRowHeight = 120;
    // Below this, in the file's own pixels, OCR of small digits is
    // unreliable (a 924 px wide photo read Injection # 1 as 41).
    private const int MinSourceTextHeight = 20;
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public async Task<ReportText> ReadPageAsync(byte[] imageBytes, CancellationToken ct)
    {
        using var decoded = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("The file is not a readable image.");
        var pageScale = Math.Clamp((float)TargetPageWidth / decoded.Width, 0.5f, 2.5f);
        using var page = decoded.Resize(
            new SKImageInfo((int)(decoded.Width * pageScale), (int)(decoded.Height * pageScale)),
            new SKSamplingOptions(SKCubicResampler.CatmullRom));

        var dir = Path.Combine(workDirectory, "ocr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Stage 1: locate.
            var pagePng = Path.Combine(dir, "page.png");
            Save(Grey(page), pagePng);
            var pageWords = Words(await RunAsync(pagePng, 11, ct));

            var minutes = pageWords.LastOrDefault(w => w.Text.StartsWith("Minutes", StringComparison.OrdinalIgnoreCase));
            var reported = pageWords.FirstOrDefault(w => w.Text.StartsWith("Reported", StringComparison.OrdinalIgnoreCase)
                && (minutes is null || w.Top > minutes.Top));
            var tableTop = minutes is null ? -1 : minutes.Top + minutes.Height + 5;
            var tableBottom = reported?.Top - 10 ?? page.Height;

            var tableWords = new List<OcrWord>();
            if (minutes is not null && tableBottom - tableTop > 20)
            {
                const float s = 2f;
                var tablePng = Crop(page, new SKRectI(0, tableTop, page.Width, tableBottom), s, Path.Combine(dir, "table.png"));
                tableWords = Words(await RunAsync(tablePng, 6, ct))
                    .Select(w => w with
                    {
                        Left = (int)(w.Left / s),
                        Top = (int)(w.Top / s) + tableTop,
                        Width = (int)(w.Width / s),
                        Height = (int)(w.Height / s),
                        Block = w.Block + 10_000
                    })
                    .ToList();
            }

            // Stage 2: read each row on its own.
            bool InTable(double y) => tableTop >= 0 && y >= tableTop && y < tableBottom;
            var located = tableWords.Count == 0
                ? pageWords
                : pageWords.Where(w => !InTable(w.Top + w.Height / 2.0)).Concat(tableWords).ToList();
            var rows = Rows(located, page.Width, page.Height, InTable, tableWords);
            // Each segment is cropped at two scales; with the page pass that
            // gives three independent readings to vote on.
            var crops = rows.SelectMany(r => r.Segments).ToList();
            var cropFiles = new List<string>();
            foreach (var big in new[] { 1f, 1.6f })
                cropFiles.AddRange(crops.Select((seg, i) => Crop(page, seg.Rect,
                    Math.Clamp(TargetRowHeight * big / seg.Rect.Height, 1f, 6f), Path.Combine(dir, $"seg{i:D3}x{big}.png"))));
            var listFile = Path.Combine(dir, "segments.txt");
            await File.WriteAllLinesAsync(listFile, cropFiles, ct);
            var segTexts = cropFiles.Count == 0 ? [] : TextPerImage(Words(await RunAsync(listFile, 7, ct)), cropFiles.Count);

            // A synthetic header row ahead of the table rows, in case the
            // real one was misread - the parser starts the table there.
            var lines = new List<ReportLine>();
            var headerAdded = false;
            var next = 0;
            foreach (var row in rows)
            {
                if (row.InTable && !headerAdded)
                {
                    lines.Add(new ReportLine("RT Area Height", null));
                    headerAdded = true;
                }
                var parts = row.Segments
                    .Select((seg, i) => Vote(segTexts[next + i], segTexts[crops.Count + next + i], new ReportLine(seg.PageText, seg.PageConf)))
                    .ToList();
                next += row.Segments.Count;
                lines.Add(new ReportLine(string.Join("   ", parts.Select(p => p.Text)), parts.Min(p => p.Confidence)));
            }

            var problems = new List<string>();
            if (pageWords.Count > 0)
            {
                var sourceHeight = pageWords.Select(w => w.Height).Order().ElementAt(pageWords.Count / 2) / pageScale;
                if (sourceHeight < MinSourceTextHeight)
                    problems.Add($"Image resolution too low: text is about {sourceHeight:F0} px high, " +
                        $"{MinSourceTextHeight}+ is needed. Values may be misread - use the PDF or a closer photo.");
            }

            var crossCheck = rows
                .Select(r => new ReportLine(string.Join("   ", r.Segments.Select(sg => sg.PageText)), r.Segments.Min(sg => sg.PageConf)))
                .ToList();
            return new ReportText(ReportTextSource.Ocr, lines, crossCheck, problems);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    private sealed record OcrWord(int Image, int Block, int Par, int LineNo, int Left, int Top, int Width, int Height, double Conf, string Text);

    private sealed record Segment(SKRectI Rect, double Mid, string PageText, double PageConf);

    private sealed record Row(IReadOnlyList<Segment> Segments, bool InTable);

    // Rows: words at the same height (vertical centres within 0.6 of a
    // median word height). Segments: a row split at wide gaps (the report's
    // two columns, a label and a far value), each cropped around its own
    // centre line - a crop that takes in part of the row above or below
    // reads as nothing, and a tilted photo puts the right column lower than
    // the left. Tall glyphs (box borders read as "|") are ignored. Outside
    // the table only rows with a word of 3+ letters are kept (drops axis
    // ticks).
    private static List<Row> Rows(
        List<OcrWord> words, int pageWidth, int pageHeight, Func<double, bool> inTable, List<OcrWord> tableWords)
    {
        if (words.Count == 0) return [];
        var median = words.Select(w => w.Height).Order().ElementAt(words.Count / 2);
        static double Mid(OcrWord w) => w.Top + w.Height / 2.0;

        var groups = new List<List<OcrWord>>();
        foreach (var w in words.Where(w => w.Height <= median * 2 && w.Height >= median / 3).OrderBy(Mid))
        {
            var g = groups.LastOrDefault();
            if (g is not null && Math.Abs(g.Average(Mid) - Mid(w)) < median * 0.6) g.Add(w);
            else groups.Add([w]);
        }

        var rows = new List<Row>();
        foreach (var g in groups)
        {
            var table = inTable(g.Average(Mid));
            if (!table && !g.Any(w => Regex.IsMatch(w.Text, "[A-Za-z]{3,}"))) continue;

            // A table row is read whole, across the table's full width: its
            // cells are close together and a cell the locate pass missed
            // would otherwise be left out of every crop.
            if (table && tableWords.Count > 0)
            {
                var th = g.Select(w => w.Height).Order().ElementAt(g.Count / 2);
                var tmid = g.Average(Mid);
                var rowText = ToLine(g);
                rows.Add(new Row([new Segment(new SKRectI(
                    Math.Max(0, tableWords.Min(w => w.Left) - 2 * th),
                    Math.Max(0, (int)(tmid - th * 0.85)),
                    Math.Min(pageWidth, tableWords.Max(w => w.Left + w.Width) + 2 * th),
                    Math.Min(pageHeight, (int)(tmid + th * 0.85))), tmid, rowText.Text, rowText.Confidence ?? 0)], true));
                continue;
            }

            var pieces = new List<List<OcrWord>>();
            foreach (var w in g.OrderBy(w => w.Left))
            {
                var piece = pieces.LastOrDefault();
                if (piece is not null && w.Left - piece.Max(x => x.Left + x.Width) < median * 4) piece.Add(w);
                else pieces.Add([w]);
            }

            rows.Add(new Row(pieces.Select(piece =>
            {
                var h = piece.Select(w => w.Height).Order().ElementAt(piece.Count / 2);
                var mid = piece.Average(Mid);
                var text = ToLine(piece);
                return new Segment(new SKRectI(
                    Math.Max(0, piece.Min(w => w.Left) - h),
                    Math.Max(0, (int)(mid - h * 0.85)),
                    Math.Min(pageWidth, piece.Max(w => w.Left + w.Width) + h),
                    Math.Min(pageHeight, (int)(mid + h * 0.85))), mid, text.Text, text.Confidence ?? 0);
            }).ToList(), table));
        }
        return rows;
    }

    // Two of three readings agreeing (ignoring spacing/punctuation) win;
    // otherwise the most confident. The page pass is also the cross-check
    // pass, so when it is outvoted the parser still flags the field.
    private static ReportLine Vote(params ReportLine[] readings)
    {
        static string Key(ReportLine l) => Regex.Replace(l.Text, "[^0-9A-Za-z]", "").ToUpperInvariant();
        var usable = readings.Where(r => Key(r).Length > 0).ToList();
        if (usable.Count == 0) return readings[^1];
        var agreed = usable.GroupBy(Key).Where(g => g.Count() >= 2).MaxBy(g => g.Count());
        return (agreed ?? (IEnumerable<ReportLine>)usable).MaxBy(r => r.Confidence ?? 0)!;
    }

    private static List<OcrWord> Words(string tsv)
    {
        var words = new List<OcrWord>();
        foreach (var row in tsv.Split('\n').Skip(1))
        {
            var c = row.TrimEnd('\r').Split('\t');
            if (c.Length < 12 || c[0] != "5" || string.IsNullOrWhiteSpace(c[11])) continue;
            int I(int i) => int.Parse(c[i], CultureInfo.InvariantCulture);
            words.Add(new OcrWord(I(1), I(2), I(3), I(4), I(6), I(7), I(8), I(9),
                double.Parse(c[10], CultureInfo.InvariantCulture), c[11]));
        }
        return words;
    }

    private static List<ReportLine> TextPerImage(List<OcrWord> words, int imageCount) =>
        Enumerable.Range(1, imageCount)
            .Select(i => words.Where(w => w.Image == i).ToList())
            .Select(ws => ws.Count == 0 ? new ReportLine("", 0) : ToLine(ws))
            .ToList();

    private static ReportLine ToLine(IEnumerable<OcrWord> ws)
    {
        var list = ws.OrderBy(w => w.Left).ToList();
        return new ReportLine(string.Join(' ', list.Select(w => w.Text)), Math.Round(list.Average(w => w.Conf), 1));
    }

    private static string Crop(SKBitmap page, SKRectI rect, float scale, string path)
    {
        using var subset = new SKBitmap();
        page.ExtractSubset(subset, rect);
        using var big = subset.Resize(
            new SKImageInfo((int)(rect.Width * scale), (int)(rect.Height * scale)),
            new SKSamplingOptions(SKCubicResampler.CatmullRom));
        Save(Binarise(big), path);
        return path;
    }

    private static SKBitmap Grey(SKBitmap src)
    {
        using var rgba = src.Copy(SKColorType.Rgba8888);
        var px = rgba.GetPixelSpan();
        var grey = new SKBitmap(rgba.Width, rgba.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        var dst = grey.GetPixelSpan();
        for (var i = 0; i < dst.Length; i++)
            dst[i] = (byte)((px[i * 4] * 30 + px[i * 4 + 1] * 59 + px[i * 4 + 2] * 11) / 100);
        return grey;
    }

    // Greyscale + Otsu threshold. Thresholding per crop copes with uneven
    // lighting across a photo better than one global threshold.
    private static SKBitmap Binarise(SKBitmap src)
    {
        using var rgba = src.Copy(SKColorType.Rgba8888);
        var px = rgba.GetPixelSpan();
        var gray = new byte[rgba.Width * rgba.Height];
        var hist = new long[256];
        for (var i = 0; i < gray.Length; i++)
        {
            var g = (byte)((px[i * 4] * 30 + px[i * 4 + 1] * 59 + px[i * 4 + 2] * 11) / 100);
            gray[i] = g;
            hist[g]++;
        }

        double sum = 0;
        for (var t = 0; t < 256; t++) sum += t * (double)hist[t];
        double sumB = 0, wB = 0, best = -1;
        var threshold = 128;
        for (var t = 0; t < 256; t++)
        {
            wB += hist[t];
            var wF = gray.Length - wB;
            if (wB == 0) continue;
            if (wF == 0) break;
            sumB += t * (double)hist[t];
            var diff = sumB / wB - (sum - sumB) / wF;
            var between = wB * wF * diff * diff;
            if (between > best) { best = between; threshold = t; }
        }

        var (w, h) = (rgba.Width, rgba.Height);
        var bin = new SKBitmap(w, h, SKColorType.Gray8, SKAlphaType.Opaque);
        var dst = bin.GetPixelSpan();
        for (var i = 0; i < gray.Length; i++) dst[i] = gray[i] <= threshold ? (byte)0 : (byte)255;

        // Erase table/box borders: a column dark for most of the crop's
        // height, or a row dark for most of its width, is a ruled line, not
        // a glyph. Left in, they fuse with digits ("1 | 3.300" -> "13.300").
        for (var x = 0; x < w; x++)
        {
            var dark = 0;
            for (var y = 0; y < h; y++) if (dst[y * w + x] == 0) dark++;
            if (dark > h * 0.8) for (var y = 0; y < h; y++) dst[y * w + x] = 255;
        }
        for (var y = 0; y < h; y++)
        {
            var dark = 0;
            for (var x = 0; x < w; x++) if (dst[y * w + x] == 0) dark++;
            if (dark > w * 0.5) for (var x = 0; x < w; x++) dst[y * w + x] = 255;
        }
        return bin;
    }

    private static void Save(SKBitmap bmp, string path)
    {
        using (bmp)
        using (var file = File.Create(path))
            bmp.Encode(file, SKEncodedImageFormat.Png, 100);
    }

    private async Task<string> RunAsync(string input, int psm, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(tesseractPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[] { input, "-", "--psm", psm.ToString(CultureInfo.InvariantCulture), "-l", "eng", "tsv" })
            psi.ArgumentList.Add(a);
        psi.Environment["OMP_THREAD_LIMIT"] = "1";

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Tesseract did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        var stdout = proc.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = proc.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await proc.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            proc.Kill(entireProcessTree: true);
            throw;
        }
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Tesseract failed ({proc.ExitCode}): {await stderr}");
        return await stdout;
    }
}
