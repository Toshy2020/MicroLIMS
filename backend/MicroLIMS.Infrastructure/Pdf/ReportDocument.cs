namespace MicroLIMS.Infrastructure.Pdf;

public enum ReportTone { Positive, Danger, Warning, Neutral }

// A renderer-agnostic description of a controlled record. Sample, media
// lot and cryovial summaries all map into this, so one renderer produces
// all three and they cannot drift apart visually.
public class ReportDocument
{
    public string Kind { get; set; } = string.Empty;      // "SAMPLE SUMMARY REPORT"
    public string DocumentId { get; set; } = string.Empty; // "FP0826003"
    public string Subtitle { get; set; } = string.Empty;
    // Substring of Subtitle (typically the item/machine/point name) to
    // render bold instead of the rest of the line's regular weight.
    public string? SubtitleEmphasis { get; set; }
    public string BadgeText { get; set; } = string.Empty;
    public ReportTone BadgeTone { get; set; } = ReportTone.Neutral;
    public string HeaderNote { get; set; } = string.Empty;
    public List<ReportBlock> Blocks { get; set; } = new();
}

public abstract class ReportBlock { }

// Two side-by-side key/value cards - the identity + dates pairing.
public class TwoColumnBlock : ReportBlock
{
    public string LeftLabel { get; set; } = string.Empty;
    public List<(string Key, string Value)> Left { get; set; } = new();
    public string RightLabel { get; set; } = string.Empty;
    public List<(string Key, string Value)> Right { get; set; } = new();
}

// A single full-width card of evenly spaced label/value/sub items.
public class StripBlock : ReportBlock
{
    public string Label { get; set; } = string.Empty;
    public List<(string Label, string Value, string? Sub)> Items { get; set; } = new();
}

public class HeadingBlock : ReportBlock
{
    public string Text { get; set; } = string.Empty;
}

// A genuine multi-column table inside a CardBlock - used for the EM/After
// Cleaning per-location result breakdown, where a flat label/value Row
// can't show one location per line with several fields each.
public record TableColumn(string Header, double WidthFraction);

// The bordered record card: header with a right-aligned headline value,
// an optional detail strip, optional stat boxes, free rows, an optional
// table, and a footer.
public class CardBlock : ReportBlock
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string HeadlineValue { get; set; } = string.Empty;
    public string HeadlineUnit { get; set; } = string.Empty;
    public ReportTone Tone { get; set; } = ReportTone.Positive;
    public List<(string Label, string Value)> Details { get; set; } = new();
    public List<(string Label, string Value)> Stats { get; set; } = new();
    public List<string> MetaLines { get; set; } = new();
    public List<(string Left, string Right)> Rows { get; set; } = new();
    public List<TableColumn> TableColumns { get; set; } = new();
    public List<List<string>> TableRows { get; set; } = new();
    // Index into TableColumns whose cells should render as a colored
    // status word (WithinLimits/Detected/etc.) instead of plain text.
    public int? TableStatusColumnIndex { get; set; }
    public string FooterLeft { get; set; } = string.Empty;
    public string FooterRight { get; set; } = string.Empty;
}

// A bordered list - lifecycle timeline, thaw history, step history.
public class ListBlock : ReportBlock
{
    public string Label { get; set; } = string.Empty;
    public List<(string Left, string Right)> Rows { get; set; } = new();
    public string EmptyText { get; set; } = "None recorded.";
}

public class SignatureBlock : ReportBlock
{
    public List<SignatureEntry> Signatures { get; set; } = new();
}

public class SignatureEntry
{
    public string PrintedName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string SignedAt { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? Comment { get; set; }
}
