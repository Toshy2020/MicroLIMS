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
    public const string DocumentAlreadyVoided = "Document.AlreadyVoided";
    public const string DocumentApprovePermissionRequired = "Document.ApprovePermissionRequired";
    public const string DocumentExternalReportHasRecordedResult = "Document.ExternalReportHasRecordedResult";
    public const string DocumentExternalReportMustUploadFromTest = "Document.ExternalReportMustUploadFromTest";
    public const string DocumentFileReadFailed = "Document.FileReadFailed";
    public const string DocumentIntegrityCheckFailed = "Document.IntegrityCheckFailed";
    public const string DocumentNotFound = "Document.NotFound";
    public const string DocumentOnlyAssignedApprover = "Document.OnlyAssignedApprover";
    public const string DocumentVoidReasonRequired = "Document.VoidReasonRequired";
    public const string MediaCannotApproveOwnLot = "Media.CannotApproveOwnLot";
    public const string ReceivingAllTestsNotApplicable = "Receiving.AllTestsNotApplicable";
    public const string ReceivingChooseAtLeastOneLab = "Receiving.ChooseAtLeastOneLab";
    public const string ReceivingGradeAppliesToNoTests = "Receiving.GradeAppliesToNoTests";
    public const string ReceivingGradeHasNoSpecification = "Receiving.GradeHasNoSpecification";
    public const string ReceivingGradeRequired = "Receiving.GradeRequired";
    public const string ReceivingItemFrozen = "Receiving.ItemFrozen";
    public const string ReceivingItemHasNoAssignedTests = "Receiving.ItemHasNoAssignedTests";
    public const string ReceivingItemHasNoTestsForSection = "Receiving.ItemHasNoTestsForSection";
    public const string ReceivingItemNotConfigured = "Receiving.ItemNotConfigured";
    public const string ReceivingProductionStageRequired = "Receiving.ProductionStageRequired";
    public const string ReceivingProductionStageUnknown = "Receiving.ProductionStageUnknown";
    public const string ReceivingReferenceNumberClash = "Receiving.ReferenceNumberClash";
    public const string ReceivingSelectedLabsDoNotExist = "Receiving.SelectedLabsDoNotExist";
    public const string ReceivingUnsupportedCategory = "Receiving.UnsupportedCategory";
    public const string SampleAddLabAlreadyTesting = "Sample.AddLabAlreadyTesting";
    public const string SampleAddLabAwaitingTestSelection = "Sample.AddLabAwaitingTestSelection";
    public const string SampleAddLabCategoryNotAllowed = "Sample.AddLabCategoryNotAllowed";
    public const string SampleAddLabClosedSample = "Sample.AddLabClosedSample";
    public const string SampleAddLabReasonRequired = "Sample.AddLabReasonRequired";
    public const string SampleAnalystNotFound = "Sample.AnalystNotFound";
    public const string SampleBulkAssignLimitExceeded = "Sample.BulkAssignLimitExceeded";
    public const string SampleCannotApproveReviewed = "Sample.CannotApproveReviewed";
    public const string SampleCannotApproveTested = "Sample.CannotApproveTested";
    public const string SampleCannotChangeItemOrCauseWhileAwaitingTests = "Sample.CannotChangeItemOrCauseWhileAwaitingTests";
    public const string SampleCannotReassignWithResult = "Sample.CannotReassignWithResult";
    public const string SampleCannotReviewTested = "Sample.CannotReviewTested";
    public const string SampleCauseNotFound = "Sample.CauseNotFound";
    public const string SampleChooseAtLeastOneTest = "Sample.ChooseAtLeastOneTest";
    public const string SampleChooseAtLeastOneTestToAssign = "Sample.ChooseAtLeastOneTestToAssign";
    public const string SampleChooseAtLeastOneToAssign = "Sample.ChooseAtLeastOneToAssign";
    public const string SampleClosedNoTestSelection = "Sample.ClosedNoTestSelection";
    public const string SampleCorrectionItemFrozen = "Sample.CorrectionItemFrozen";
    public const string SampleCorrectionItemHasNoTests = "Sample.CorrectionItemHasNoTests";
    public const string SampleCorrectionLockedAfterReview = "Sample.CorrectionLockedAfterReview";
    public const string SampleCorrectionReasonRequired = "Sample.CorrectionReasonRequired";
    public const string SampleDepartmentNotFound = "Sample.DepartmentNotFound";
    public const string SampleDuplicateBatchAndControl = "Sample.DuplicateBatchAndControl";
    public const string SampleDuplicateInBulkAssign = "Sample.DuplicateInBulkAssign";
    public const string SampleDuplicateTestInAssignment = "Sample.DuplicateTestInAssignment";
    public const string SampleExpiryBeforeMfg = "Sample.ExpiryBeforeMfg";
    public const string SampleFieldRequired = "Sample.FieldRequired";
    public const string SampleGradeCorrectionRequiresPendingTests = "Sample.GradeCorrectionRequiresPendingTests";
    public const string SampleGradeLeavesLabWithNoTests = "Sample.GradeLeavesLabWithNoTests";
    public const string SampleItemNotFound = "Sample.ItemNotFound";
    public const string SampleMachineNotFound = "Sample.MachineNotFound";
    public const string SampleNewItemMustMatchCategory = "Sample.NewItemMustMatchCategory";
    public const string SampleNoCorrectionChangesMade = "Sample.NoCorrectionChangesMade";
    public const string SampleNoOpenTestsToAssign = "Sample.NoOpenTestsToAssign";
    public const string SampleNotFound = "Sample.NotFound";
    public const string SampleRetestCannotChangeItemOrLocation = "Sample.RetestCannotChangeItemOrLocation";
    public const string SampleRetestCauseSystemAssigned = "Sample.RetestCauseSystemAssigned";
    public const string SampleStorageConditionInvalid = "Sample.StorageConditionInvalid";
    public const string SampleStorageConditionRequired = "Sample.StorageConditionRequired";
    public const string SampleStorageTimeNegative = "Sample.StorageTimeNegative";
    public const string SampleStorageTimeRequired = "Sample.StorageTimeRequired";
    public const string SampleTestingStartedCannotChangeItemOrLocation = "Sample.TestingStartedCannotChangeItemOrLocation";
    public const string SampleTestNotAvailable = "Sample.TestNotAvailable";
    public const string SampleTestsAlreadySelected = "Sample.TestsAlreadySelected";
    public const string SampleVoidReasonRequired = "Sample.VoidReasonRequired";
    public const string SampleVoidedDocumentsLocked = "Sample.VoidedDocumentsLocked";
    public const string SampleWaterDepartmentNotFound = "Sample.WaterDepartmentNotFound";
    public const string TestAlreadyTaken = "Test.AlreadyTaken";
    public const string TestClosedCannotChangeAnalyst = "Test.ClosedCannotChangeAnalyst";
    public const string TestNotFoundOnSample = "Test.NotFoundOnSample";
    public const string TestNotInYourSection = "Test.NotInYourSection";
    public const string TestNotOutsourced = "Test.NotOutsourced";
    public const string TestOnlyHolderCanRelease = "Test.OnlyHolderCanRelease";
    public const string TestOrderNotFound = "Test.OrderNotFound";

    private static readonly string[] None = [];

    // Code -> the parameter names its sentence uses.
    public static IReadOnlyDictionary<string, string[]> ParameterNames { get; } = new Dictionary<string, string[]>
    {
        [AuthRoleDoesNotIncludeAction] = None,
        [DocumentAlreadyVoided] = None,
        [DocumentApprovePermissionRequired] = None,
        [DocumentExternalReportHasRecordedResult] = None,
        [DocumentExternalReportMustUploadFromTest] = None,
        [DocumentFileReadFailed] = None,
        [DocumentIntegrityCheckFailed] = None,
        [DocumentNotFound] = ["documentId"],
        [DocumentOnlyAssignedApprover] = None,
        [DocumentVoidReasonRequired] = None,
        [MediaCannotApproveOwnLot] = None,
        [ReceivingAllTestsNotApplicable] = ["item"],
        [ReceivingChooseAtLeastOneLab] = None,
        [ReceivingGradeAppliesToNoTests] = ["grade", "item"],
        [ReceivingGradeHasNoSpecification] = ["grade"],
        [ReceivingGradeRequired] = None,
        [ReceivingItemFrozen] = ["item"],
        [ReceivingItemHasNoAssignedTests] = ["item"],
        [ReceivingItemHasNoTestsForSection] = ["item", "section"],
        [ReceivingItemNotConfigured] = ["itemId"],
        [ReceivingProductionStageRequired] = None,
        [ReceivingProductionStageUnknown] = ["stage"],
        [ReceivingReferenceNumberClash] = ["referenceNumber"],
        [ReceivingSelectedLabsDoNotExist] = None,
        [ReceivingUnsupportedCategory] = ["category"],
        [SampleAddLabAlreadyTesting] = None,
        [SampleAddLabAwaitingTestSelection] = None,
        [SampleAddLabCategoryNotAllowed] = None,
        [SampleAddLabClosedSample] = None,
        [SampleAddLabReasonRequired] = None,
        [SampleAnalystNotFound] = ["userId"],
        [SampleBulkAssignLimitExceeded] = ["max"],
        [SampleCannotApproveReviewed] = None,
        [SampleCannotApproveTested] = None,
        [SampleCannotChangeItemOrCauseWhileAwaitingTests] = None,
        [SampleCannotReassignWithResult] = None,
        [SampleCannotReviewTested] = None,
        [SampleCauseNotFound] = None,
        [SampleChooseAtLeastOneTest] = None,
        [SampleChooseAtLeastOneTestToAssign] = None,
        [SampleChooseAtLeastOneToAssign] = None,
        [SampleClosedNoTestSelection] = None,
        [SampleCorrectionItemFrozen] = ["item"],
        [SampleCorrectionItemHasNoTests] = ["item"],
        [SampleCorrectionLockedAfterReview] = None,
        [SampleCorrectionReasonRequired] = None,
        [SampleDepartmentNotFound] = None,
        [SampleDuplicateBatchAndControl] = None,
        [SampleDuplicateInBulkAssign] = None,
        [SampleDuplicateTestInAssignment] = None,
        [SampleExpiryBeforeMfg] = None,
        [SampleFieldRequired] = ["field"],
        [SampleGradeCorrectionRequiresPendingTests] = None,
        [SampleGradeLeavesLabWithNoTests] = ["labName"],
        [SampleItemNotFound] = None,
        [SampleMachineNotFound] = None,
        [SampleNewItemMustMatchCategory] = None,
        [SampleNoCorrectionChangesMade] = None,
        [SampleNoOpenTestsToAssign] = None,
        [SampleNotFound] = ["sampleId"],
        [SampleRetestCannotChangeItemOrLocation] = None,
        [SampleRetestCauseSystemAssigned] = None,
        [SampleStorageConditionInvalid] = None,
        [SampleStorageConditionRequired] = None,
        [SampleStorageTimeNegative] = None,
        [SampleStorageTimeRequired] = None,
        [SampleTestingStartedCannotChangeItemOrLocation] = None,
        [SampleTestNotAvailable] = ["testCode"],
        [SampleTestsAlreadySelected] = None,
        [SampleVoidReasonRequired] = None,
        [SampleVoidedDocumentsLocked] = None,
        [SampleWaterDepartmentNotFound] = None,
        [TestAlreadyTaken] = ["name"],
        [TestClosedCannotChangeAnalyst] = ["testCode"],
        [TestNotFoundOnSample] = ["referenceNumber", "testOrderId"],
        [TestNotInYourSection] = None,
        [TestNotOutsourced] = ["testOrderId"],
        [TestOnlyHolderCanRelease] = None,
        [TestOrderNotFound] = ["testOrderId"],
    };
}
