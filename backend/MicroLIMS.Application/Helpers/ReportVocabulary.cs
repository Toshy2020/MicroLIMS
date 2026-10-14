using System.Text.RegularExpressions;

namespace MicroLIMS.Application.Helpers;

// The wording every controlled record shares - signature meanings and
// enum labels. It lives here once so the exported PDF, the archived copy
// and the on-screen printable report cannot drift apart: the frontend
// reads these strings from the DTOs (SignatureDto.MeaningText,
// SampleSummaryDto.StatusDisplay) instead of keeping its own copies.
public static class ReportVocabulary
{
    // 11.50(b) wants the meaning attested to, not the signature's type.
    // "Record" rather than "sample" because the same signatures appear on
    // media lot and cryovial records too.
    private static readonly Dictionary<string, string> SignatureMeanings = new()
    {
        ["Reviewed"] = "I have reviewed the test data and confirm it is complete and accurate.",
        ["Approved"] = "I approve the release of this record for its intended use.",
        ["Rejected"] = "I reject this record; it does not conform to specification.",
        ["RetestRequested"] = "I am ordering a retest of the retained sample.",
        ["InvestigationOrdered"] = "I am ordering an investigation into these results.",
        ["SampleCorrected"] = "I confirm these corrections to the sample record are accurate and justified.",
        ["SampleVoided"] = "I void this sample record; it is struck from the register."
    };

    public static string SignatureMeaning(string? meaning) =>
        meaning is not null && SignatureMeanings.TryGetValue(meaning, out var text) ? text : Humanize(meaning);

    // Enum names arrive PascalCase; a controlled document should read as
    // English. Consecutive capitals are preserved so acronyms survive:
    // "TAMC" stays "TAMC", "OOSInvestigation" becomes "OOS investigation".
    public static string Humanize(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "-";
        var spaced = Regex.Replace(v, "([a-z0-9])([A-Z])", "$1 $2");
        spaced = Regex.Replace(spaced, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        return Regex.Replace(spaced, @"\s+(.)", m => " " + char.ToLowerInvariant(m.Groups[1].Value[0]));
    }
}
