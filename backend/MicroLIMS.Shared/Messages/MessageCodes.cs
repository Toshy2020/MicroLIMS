namespace MicroLIMS.Shared.Messages;

// Codes for the refusals the server reports, named Module.Reason. The frontend
// translates a code through frontend/src/locales/{en,ar}/serverMessages.json;
// the English sentence in the exception stays the fallback and the text every
// log, audit record and test sees.
//
// Every code is listed once in ParameterNames with the placeholders its
// sentence uses ({{name}} in the template files). MessageTemplateTests keeps
// the two template files and this list in step.
//
// From F4 Task 4 on, a new BusinessRuleException / NotFoundException in
// Application or API carries one of these codes (UncodedMessageRatchetTests).
public static class MessageCodes
{
    public const string AuthRoleDoesNotIncludeAction = "Auth.RoleDoesNotIncludeAction";
    public const string DocumentOnlyAssignedApprover = "Document.OnlyAssignedApprover";
    public const string DocumentApprovePermissionRequired = "Document.ApprovePermissionRequired";
    public const string MediaCannotApproveOwnLot = "Media.CannotApproveOwnLot";
    public const string SampleCannotApproveReviewed = "Sample.CannotApproveReviewed";
    public const string SampleCannotApproveTested = "Sample.CannotApproveTested";
    public const string SampleCannotReviewTested = "Sample.CannotReviewTested";
    public const string SampleDuplicateBatchAndControl = "Sample.DuplicateBatchAndControl";
    public const string SampleNoOpenTestsToAssign = "Sample.NoOpenTestsToAssign";
    public const string TestAlreadyTaken = "Test.AlreadyTaken";
    public const string TestNotInYourSection = "Test.NotInYourSection";
    public const string TestOnlyHolderCanRelease = "Test.OnlyHolderCanRelease";

    private static readonly string[] None = [];

    // Code -> the parameter names its sentence uses.
    public static IReadOnlyDictionary<string, string[]> ParameterNames { get; } = new Dictionary<string, string[]>
    {
        [AuthRoleDoesNotIncludeAction] = None,
        [DocumentOnlyAssignedApprover] = None,
        [DocumentApprovePermissionRequired] = None,
        [MediaCannotApproveOwnLot] = None,
        [SampleCannotApproveReviewed] = None,
        [SampleCannotApproveTested] = None,
        [SampleCannotReviewTested] = None,
        [SampleDuplicateBatchAndControl] = None,
        [SampleNoOpenTestsToAssign] = None,
        [TestAlreadyTaken] = ["name"],
        [TestNotInYourSection] = None,
        [TestOnlyHolderCanRelease] = None,
    };
}
