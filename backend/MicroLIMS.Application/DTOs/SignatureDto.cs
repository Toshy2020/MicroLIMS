using MicroLIMS.Application.Helpers;

namespace MicroLIMS.Application.DTOs;

// What an auditor asks to see: printed name, meaning, date/time, comment -
// never the raw UserId/hash, per 11.50(b).
//
// Username is included alongside PrintedName because full names collide in
// practice (this lab has four separate accounts all displaying as "Mohamed
// Mahmoud"). On a printed record showing a reviewer and an approver, two
// identical names read as one person signing both roles - exactly what
// segregation of duties forbids - so the signature block needs the
// username to show they are distinct people.
public record SignatureDto(string PrintedName, string Username, string Role, string Meaning, DateTime SignedAt, string? Comment)
{
    // The sentence the signer attested to, worded once in ReportVocabulary
    // so every rendering of this signature (PDF, archive, print page)
    // states the same meaning.
    public string MeaningText => ReportVocabulary.SignatureMeaning(Meaning);
}
