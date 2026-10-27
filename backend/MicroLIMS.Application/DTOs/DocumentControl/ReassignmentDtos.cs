namespace MicroLIMS.Application.DTOs.DocumentControl;

// Kinds of responsibility that can be waiting for a new holder. The queue is computed from the
// records themselves (an open item whose holder is a deactivated user), never copied.
public static class ReassignmentItemTypes
{
    public const string Custodian = "Custodian";
    public const string Assignment = "Assignment";
    public const string ReviewTask = "ReviewTask";
    public const string ApprovalTask = "ApprovalTask";
    public const string PeriodicReview = "PeriodicReview";

    public static readonly IReadOnlyList<string> All = new[] { Custodian, Assignment, ReviewTask, ApprovalTask, PeriodicReview };
}

public record ReassignmentItemDto(
    string ItemType,
    int ItemId,
    int DepartmentId,
    string DepartmentName,
    int DocumentMasterId,
    string DocumentCode,
    string DocumentTitle,
    string? RevisionNumber,
    // The role the new holder takes over ("Custodian", "Author", "Technical reviewer", "Approver", "Periodic reviewer").
    string RoleNeeded,
    // The permission the new holder's role must hold for that role.
    string RequiredPermission,
    // Always "Inactive user"; the name below is for the people who hand the work over.
    string HolderLabel,
    string? HolderName,
    DateTime? InactiveSince,
    bool IsOverdue);

public record ReassignmentQueueDto(
    IReadOnlyList<ReassignmentItemDto> Items,
    int Total,
    int Overdue,
    // DocumentControl.ReassignmentEscalationDays: items older than this (from the holder's deactivation) are overdue.
    int EscalationDays);

public record ReassignmentCountDto(int Total, int Overdue);

public record ReassignmentCandidateDto(int UserId, string FullName, string? JobTitle, string RoleName);

public record ReassignRequest(string ItemType, int ItemId, int NewUserId, string? Reason);

public record ReassignmentResultDto(string ItemType, int ItemId, int DocumentMasterId, int PreviousUserId, int NewUserId, string NewUserName);
