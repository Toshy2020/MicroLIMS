namespace MicroLIMS.Application.DTOs;

// The report stored (as JSON) in AccessReview.SnapshotJson. Property names are
// the JSON names (camelCase); once stored it never changes.
public record AccessReviewSnapshot(
    int ReportVersion,
    DateTime GeneratedAt,
    string PeriodLabel,
    int DormantDays,
    int StatusHistoryMonths,
    AccessReviewSummary Summary,
    IReadOnlyList<AccessReviewUserEntry> Users);

public record AccessReviewSummary(
    int TotalUsers, int ActiveUsers, int InactiveUsers,
    int DormantUsers, int NeverLoggedInUsers, int SodConflictUsers);

public record AccessReviewUserEntry(
    int UserId,
    string Username,
    string FullName,
    bool IsActive,
    DateTime? DeactivatedAt,
    int RoleId,
    string RoleName,
    string RoleType,
    IReadOnlyList<AccessReviewPermission> Permissions,
    IReadOnlyList<AccessReviewMembership> Sections,
    DateTime? LastSuccessfulLogin,
    bool NeverLoggedIn,
    bool Dormant,
    int? DaysSinceLastActivity,
    IReadOnlyList<AccessReviewStatusChange> StatusHistory,
    IReadOnlyList<string> SodConflicts);

public record AccessReviewPermission(string Code, bool Hidden);

public record AccessReviewMembership(
    int DepartmentId, string DepartmentName, int? SectionId, string? SectionName, string? PhyschemArea);

public record AccessReviewStatusChange(
    DateTime Timestamp, string Action, int? ByUserId, string? Reason, string? Detail);

public record AccessReviewSummaryDto(
    int Id,
    DateTime GeneratedAt,
    int GeneratedByUserId,
    string? GeneratedByName,
    string PeriodLabel,
    string Status,
    int? ReviewerUserId,
    string? ReviewerName,
    DateTime? SignedAt,
    int FindingCount,
    AccessReviewSummary Summary);

public record AccessReviewFindingDto(
    int Id,
    int ReviewId,
    int UserId,
    string? Username,
    string Kind,
    string Detail,
    string Action,
    string? Note,
    int RecordedByUserId,
    DateTime RecordedAt);

public record AccessReviewDetailDto(
    int Id,
    DateTime GeneratedAt,
    int GeneratedByUserId,
    string? GeneratedByName,
    string PeriodLabel,
    string Status,
    int? ReviewerUserId,
    string? ReviewerName,
    DateTime? SignedAt,
    int? ElectronicSignatureId,
    string? Conclusion,
    string SnapshotSha256,
    // False if the stored snapshot no longer hashes to SnapshotSha256 (tampering).
    bool SnapshotIntact,
    AccessReviewSnapshot Snapshot,
    IReadOnlyList<AccessReviewFindingDto> Findings);

public record GenerateAccessReviewRequest(string? PeriodLabel, string? Reason);

public record SaveAccessReviewFindingRequest(int UserId, string? Kind, string? Detail, string? Action, string? Note);

public record SignAccessReviewRequest(string? Password, string? Conclusion);

public record AccessReviewExportResult(string FileName, string ContentType, byte[] Content);
