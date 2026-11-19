namespace MicroLIMS.Shared.Messages;

// AuditReview.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string AuditReviewAlreadySigned = "AuditReview.AlreadySigned";
    public const string AuditReviewClosingCommentRequired = "AuditReview.ClosingCommentRequired";
    public const string AuditReviewClosingCommentTooLong = "AuditReview.ClosingCommentTooLong";
    public const string AuditReviewConclusionRequired = "AuditReview.ConclusionRequired";
    public const string AuditReviewConclusionTooLong = "AuditReview.ConclusionTooLong";
    public const string AuditReviewDuplicateFinding = "AuditReview.DuplicateFinding";
    public const string AuditReviewEntryOutsideScope = "AuditReview.EntryOutsideScope";
    public const string AuditReviewFindingAlreadyClosed = "AuditReview.FindingAlreadyClosed";
    public const string AuditReviewFindingNotFound = "AuditReview.FindingNotFound";
    public const string AuditReviewFindingNoteRequired = "AuditReview.FindingNoteRequired";
    public const string AuditReviewFindingNoteTooLong = "AuditReview.FindingNoteTooLong";
    public const string AuditReviewInvalidScopeKind = "AuditReview.InvalidScopeKind";
    public const string AuditReviewNotFound = "AuditReview.NotFound";
    public const string AuditReviewPeriodEndInFuture = "AuditReview.PeriodEndInFuture";
    public const string AuditReviewPeriodStartNotBeforeEnd = "AuditReview.PeriodStartNotBeforeEnd";
    public const string AuditReviewScopeEntityNameRequired = "AuditReview.ScopeEntityNameRequired";
    public const string AuditReviewScopeEntityNameTooLong = "AuditReview.ScopeEntityNameTooLong";
    public const string AuditReviewScopeRecordIdRequired = "AuditReview.ScopeRecordIdRequired";
    public const string AuditReviewScopeRecordIdTooLong = "AuditReview.ScopeRecordIdTooLong";
    public const string AuditReviewSignedCannotAddFindings = "AuditReview.SignedCannotAddFindings";
    public const string AuditReviewTitleRequired = "AuditReview.TitleRequired";
    public const string AuditReviewTitleTooLong = "AuditReview.TitleTooLong";

    private static Dictionary<string, string[]> AuditReviewParameters() => new()
    {
        [AuditReviewAlreadySigned] = [],
        [AuditReviewClosingCommentRequired] = [],
        [AuditReviewClosingCommentTooLong] = ["max"],
        [AuditReviewConclusionRequired] = [],
        [AuditReviewConclusionTooLong] = ["max"],
        [AuditReviewDuplicateFinding] = ["auditLogId"],
        [AuditReviewEntryOutsideScope] = ["auditLogId", "reviewId"],
        [AuditReviewFindingAlreadyClosed] = [],
        [AuditReviewFindingNotFound] = ["findingId"],
        [AuditReviewFindingNoteRequired] = [],
        [AuditReviewFindingNoteTooLong] = ["max"],
        [AuditReviewInvalidScopeKind] = [],
        [AuditReviewNotFound] = ["reviewId"],
        [AuditReviewPeriodEndInFuture] = [],
        [AuditReviewPeriodStartNotBeforeEnd] = [],
        [AuditReviewScopeEntityNameRequired] = [],
        [AuditReviewScopeEntityNameTooLong] = ["max"],
        [AuditReviewScopeRecordIdRequired] = [],
        [AuditReviewScopeRecordIdTooLong] = ["max"],
        [AuditReviewSignedCannotAddFindings] = [],
        [AuditReviewTitleRequired] = [],
        [AuditReviewTitleTooLong] = ["max"],
    };
}
