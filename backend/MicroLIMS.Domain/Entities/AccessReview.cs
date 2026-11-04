using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Periodic access review (EU GMP Annex 11 s.12, 21 CFR 11.10(d)(g)). The report
// is generated once and stored as SnapshotJson; it is IMMUTABLE from the moment
// it exists. A database trigger lets the row change only once, Draft -> Signed
// (reviewer, time, signature and conclusion), never touches the snapshot columns,
// and blocks DELETE and TRUNCATE always. Findings are editable only while the
// review is Draft.
public class AccessReview
{
    public int Id { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int GeneratedByUserId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    // Lower-case hex SHA-256 of the UTF-8 bytes of SnapshotJson.
    public string SnapshotSha256 { get; set; } = string.Empty;
    public AccessReviewStatus Status { get; set; } = AccessReviewStatus.Draft;
    public int? ReviewerUserId { get; set; }
    public DateTime? SignedAt { get; set; }
    public int? ElectronicSignatureId { get; set; }
    public ElectronicSignature? ElectronicSignature { get; set; }
    public string? Conclusion { get; set; }

    public List<AccessReviewFinding> Findings { get; set; } = new();
}

// One reviewer observation about one account in the report. Editable only while
// the review is Draft (database trigger); never deleted.
public class AccessReviewFinding
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public AccessReview? Review { get; set; }
    // The account the finding is about (a user in the snapshot).
    public int UserId { get; set; }
    // SodConflict, Dormant, NeverLoggedIn, RoleReview, PermissionReview, Other.
    public string Kind { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public AccessReviewFindingAction Action { get; set; } = AccessReviewFindingAction.NoChange;
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
