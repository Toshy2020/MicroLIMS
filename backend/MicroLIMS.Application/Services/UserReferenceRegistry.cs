using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services;

public enum UserReferenceDisposition
{
    // A row referencing this user here means the user has history and
    // must not be hard-deleted.
    Blocks,
    // A row referencing this user here does NOT block a hard delete -
    // it is auth housekeeping that cleans itself up via an existing
    // Cascade FK when the User row is removed.
    Excluded
}

public record UserReferenceEntry(Type EntityType, string PropertyName, UserReferenceDisposition Disposition, string Reason);

// Exhaustive, human-reviewed list of every column across the domain model
// that references User.Id (whether or not the database enforces it as a
// real FK - see the recon that produced this list: only 14 of these 58
// columns have an actual DB FK constraint; the other 44 are plain int
// columns with no DB-level enforcement at all).
//
// UserDeletionService.UserHasAnyHistoryAsync walks every Blocks entry
// looking for a reference to the user being deleted. UserReferenceModelScanTests
// asserts every int/int? "*UserId"-suffixed property found by reflecting
// over the EF model appears in this list, so a newly added column that
// nobody reviewed fails the build instead of silently going unchecked.
//
// Do not add or remove an entry without updating this list AND deciding
// its disposition - that is the entire point of keeping it explicit
// instead of reflection-driven at runtime.
public static class UserReferenceRegistry
{
    public static readonly IReadOnlyList<UserReferenceEntry> All = new[]
    {
        // ---- Group A: DB enforces a real Restrict FK to User ----
        new UserReferenceEntry(typeof(AdminPasswordRecovery), nameof(AdminPasswordRecovery.UserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(AdminPasswordRecovery), nameof(AdminPasswordRecovery.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(ElectronicSignature), nameof(ElectronicSignature.UserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(EquipmentDocument), nameof(EquipmentDocument.UploadedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(EquipmentStatusHistory), nameof(EquipmentStatusHistory.ChangedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(ItemDocument), nameof(ItemDocument.UploadedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(ItemDocumentAccessLog), nameof(ItemDocumentAccessLog.UserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(LocationPathogenObservation), nameof(LocationPathogenObservation.ObservedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(MaterialDocument), nameof(MaterialDocument.UploadedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(OosInvestigationDocument), nameof(OosInvestigationDocument.UploadedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(Sample), nameof(Sample.ReceivedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict (added alongside this feature)"),
        new UserReferenceEntry(typeof(Sample), nameof(Sample.ReviewedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(Sample), nameof(Sample.ApprovedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(TestReturnEvent), nameof(TestReturnEvent.ReviewerUserId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(TestReturnEvent), nameof(TestReturnEvent.AssignedAnalystId), UserReferenceDisposition.Blocks, "DB FK Restrict"),
        new UserReferenceEntry(typeof(DiscussionPost), nameof(DiscussionPost.AuthorUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - post author"),
        new UserReferenceEntry(typeof(DiscussionPost), nameof(DiscussionPost.LastEditedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - post editor"),
        new UserReferenceEntry(typeof(DiscussionPostVersion), nameof(DiscussionPostVersion.ChangedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - post version editor"),
        new UserReferenceEntry(typeof(DiscussionComment), nameof(DiscussionComment.AuthorUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - comment author"),
        new UserReferenceEntry(typeof(DiscussionAttachment), nameof(DiscussionAttachment.UploadedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - attachment uploader"),
        new UserReferenceEntry(typeof(Conversation), nameof(Conversation.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - conversation creator"),
        new UserReferenceEntry(typeof(ConversationParticipant), nameof(ConversationParticipant.UserId), UserReferenceDisposition.Blocks, "DB FK Restrict - conversation participant"),
        new UserReferenceEntry(typeof(DirectMessage), nameof(DirectMessage.SenderUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - message sender"),

        // Document Control (Release 1a)
        new UserReferenceEntry(typeof(DocumentMaster), nameof(DocumentMaster.DocumentOwnerUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - document owner"),
        new UserReferenceEntry(typeof(DocumentMaster), nameof(DocumentMaster.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - document creator"),
        new UserReferenceEntry(typeof(DocumentMaster), nameof(DocumentMaster.ModifiedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - document editor"),
        new UserReferenceEntry(typeof(DocumentMaster), nameof(DocumentMaster.VoidedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - document voider"),
        new UserReferenceEntry(typeof(DocumentMasterAssignment), nameof(DocumentMasterAssignment.UserId), UserReferenceDisposition.Blocks, "DB FK Restrict - assigned user"),
        new UserReferenceEntry(typeof(DocumentMasterAssignment), nameof(DocumentMasterAssignment.AssignedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - assigner user"),
        new UserReferenceEntry(typeof(DocumentRevision), nameof(DocumentRevision.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - revision creator"),
        new UserReferenceEntry(typeof(DocumentRevision), nameof(DocumentRevision.CancelledByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - revision canceller"),
        new UserReferenceEntry(typeof(RevisionFile), nameof(RevisionFile.UploadedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - file uploader"),
        new UserReferenceEntry(typeof(DocumentNumberingConfiguration), nameof(DocumentNumberingConfiguration.ModifiedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - numbering modifier"),
        new UserReferenceEntry(typeof(ConfigurationSetting), nameof(ConfigurationSetting.ModifiedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - setting modifier"),

        // Document Control (Release 1b - WP1 Technical Review)
        new UserReferenceEntry(typeof(DocumentReviewTask), nameof(DocumentReviewTask.AssignedReviewerUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - assigned reviewer"),
        new UserReferenceEntry(typeof(DocumentReviewTask), nameof(DocumentReviewTask.AssignedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - review task assigner"),
        new UserReferenceEntry(typeof(DocumentReviewTask), nameof(DocumentReviewTask.DecisionByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - review decision maker"),
        new UserReferenceEntry(typeof(DocumentReviewFinding), nameof(DocumentReviewFinding.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - finding creator"),
        new UserReferenceEntry(typeof(DocumentReviewFinding), nameof(DocumentReviewFinding.AuthorResponseByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - finding author respondent"),
        new UserReferenceEntry(typeof(DocumentReviewFinding), nameof(DocumentReviewFinding.ReviewerVerifiedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - finding reviewer verifier"),
        new UserReferenceEntry(typeof(DocumentReviewFinding), nameof(DocumentReviewFinding.ResolvedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - finding resolver"),

        // Document Control (Release 1b - WP2 Revision Management)
        new UserReferenceEntry(typeof(RevisionChangeItem), nameof(RevisionChangeItem.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - change item creator"),
        new UserReferenceEntry(typeof(RevisionImpactAssessment), nameof(RevisionImpactAssessment.CompletedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - impact assessor"),

        // Document Control (Release 1b - WP3 Approval Workflow)
        new UserReferenceEntry(typeof(DocumentApprovalTask), nameof(DocumentApprovalTask.AssignedApproverUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - assigned approver"),
        new UserReferenceEntry(typeof(DocumentApprovalTask), nameof(DocumentApprovalTask.AssignedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - approval task assigner"),
        new UserReferenceEntry(typeof(DocumentApprovalTask), nameof(DocumentApprovalTask.DecisionByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - approval decision maker"),

        // Document Control (Release 1b - WP6 Periodic Review)
        new UserReferenceEntry(typeof(PeriodicReviewTask), nameof(PeriodicReviewTask.AssignedReviewerUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - assigned periodic reviewer"),
        new UserReferenceEntry(typeof(PeriodicReviewTask), nameof(PeriodicReviewTask.CompletedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - periodic review completer"),
        new UserReferenceEntry(typeof(PeriodicReviewFinding), nameof(PeriodicReviewFinding.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - periodic review finding creator"),

        // Document Control (Release 1c - WP1/WP3/WP4 Training Matrix, Acknowledgements & Escalations)
        new UserReferenceEntry(typeof(DocumentTrainingAssignment), nameof(DocumentTrainingAssignment.AssignedUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - assigned trainee user"),
        new UserReferenceEntry(typeof(DocumentTrainingAssignment), nameof(DocumentTrainingAssignment.AcknowledgedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - acknowledgement submitter"),
        new UserReferenceEntry(typeof(DocumentTrainingAssignment), nameof(DocumentTrainingAssignment.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - training assignment creator"),
        new UserReferenceEntry(typeof(DocumentTrainingAssignment), nameof(DocumentTrainingAssignment.ModifiedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - training assignment modifier"),
        new UserReferenceEntry(typeof(DocumentTrainingConfiguration), nameof(DocumentTrainingConfiguration.ModifiedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - training configuration modifier"),
        new UserReferenceEntry(typeof(DocumentRoleCurriculum), nameof(DocumentRoleCurriculum.CreatedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - role curriculum creator"),
        new UserReferenceEntry(typeof(DocumentAcknowledgementRecord), nameof(DocumentAcknowledgementRecord.AcknowledgedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - legal acknowledgement evidence user"),
        new UserReferenceEntry(typeof(DocumentEscalationRecord), nameof(DocumentEscalationRecord.AssignedUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - escalated assignment user"),
        new UserReferenceEntry(typeof(DocumentEscalationRecord), nameof(DocumentEscalationRecord.ResolvedByUserId), UserReferenceDisposition.Blocks, "DB FK Restrict - escalation resolver user"),

        // ---- Excluded: DB enforces Cascade - auth housekeeping only, cleans up automatically ----
        new UserReferenceEntry(typeof(PasswordHistory), nameof(PasswordHistory.UserId), UserReferenceDisposition.Excluded, "Cascade FK - password history is per-user housekeeping, deleted with the user"),
        new UserReferenceEntry(typeof(PasswordResetToken), nameof(PasswordResetToken.UserId), UserReferenceDisposition.Excluded, "Cascade FK - reset tokens are per-user housekeeping, deleted with the user"),
        new UserReferenceEntry(typeof(RefreshToken), nameof(RefreshToken.UserId), UserReferenceDisposition.Excluded, "Cascade FK - refresh tokens are per-user housekeeping, deleted with the user"),

        // Error monitoring is operational/technical data, not GxP evidence -
        // a user who once triggered a 500 must not thereby become undeletable.
        // Both columns are SetNull FKs, so the database drops the attribution
        // by itself and the error entry survives without it.
        new UserReferenceEntry(typeof(ErrorLog), nameof(ErrorLog.UserId), UserReferenceDisposition.Excluded, "SetNull FK - operational error log, not a GxP record; attribution clears with the user"),
        new UserReferenceEntry(typeof(Incident), nameof(Incident.ResolvedByUserId), UserReferenceDisposition.Excluded, "SetNull FK - operational incident triage, not a GxP record; attribution clears with the user"),
        new UserReferenceEntry(typeof(SecurityAuditEvent), nameof(SecurityAuditEvent.ActorUserId), UserReferenceDisposition.Excluded, "SetNull FK - security evidence, not a GxP record; TargetUsername keeps the event readable after the user row goes"),
        new UserReferenceEntry(typeof(SecurityAuditEvent), nameof(SecurityAuditEvent.TargetUserId), UserReferenceDisposition.Excluded, "SetNull FK - security evidence, not a GxP record; TargetUsername keeps the event readable after the user row goes"),

        // ---- Group B: no DB FK constraint at all - blocked at application level only ----
        new UserReferenceEntry(typeof(AuditLog), nameof(AuditLog.UserId), UserReferenceDisposition.Blocks, "No DB FK - traceability record"),
        new UserReferenceEntry(typeof(LoginHistory), nameof(LoginHistory.UserId), UserReferenceDisposition.Blocks, "No DB FK - login history record"),
        new UserReferenceEntry(typeof(DataExportLog), nameof(DataExportLog.UserId), UserReferenceDisposition.Blocks, "No DB FK - export history record"),
        new UserReferenceEntry(typeof(NotificationLog), nameof(NotificationLog.UserId), UserReferenceDisposition.Blocks, "No DB FK - notification record"),
        new UserReferenceEntry(typeof(ArchivedRecord), nameof(ArchivedRecord.GeneratedByUserId), UserReferenceDisposition.Blocks, "No DB FK - archive provenance"),
        new UserReferenceEntry(typeof(AutoclaveProgram), nameof(AutoclaveProgram.CreatedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(AutoclaveProgram), nameof(AutoclaveProgram.LastModifiedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(AutoclaveProgramHistory), nameof(AutoclaveProgramHistory.ChangedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(ConfirmatoryPlateObservation), nameof(ConfirmatoryPlateObservation.RecordedByUserId), UserReferenceDisposition.Blocks, "No DB FK - test result provenance"),
        new UserReferenceEntry(typeof(CountTestReading), nameof(CountTestReading.EnteredByUserId), UserReferenceDisposition.Blocks, "No DB FK - test result provenance"),
        new UserReferenceEntry(typeof(IncubatorSetPointHistory), nameof(IncubatorSetPointHistory.ChangedByUserId), UserReferenceDisposition.Blocks, "No DB FK - equipment history provenance"),
        new UserReferenceEntry(typeof(PathogenObservation), nameof(PathogenObservation.ObservedByUserId), UserReferenceDisposition.Blocks, "No DB FK - test result provenance"),
        new UserReferenceEntry(typeof(Cryovial), nameof(Cryovial.PreparedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(Cryovial), nameof(Cryovial.ApprovedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(ThawEvent), nameof(ThawEvent.ThawedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(EquipmentDocument), nameof(EquipmentDocument.SupersededByUserId), UserReferenceDisposition.Blocks, "No DB FK - document lifecycle provenance"),
        new UserReferenceEntry(typeof(EquipmentDocument), nameof(EquipmentDocument.VoidedByUserId), UserReferenceDisposition.Blocks, "No DB FK - document lifecycle provenance"),
        new UserReferenceEntry(typeof(EquipmentInventory), nameof(EquipmentInventory.CreatedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(EquipmentInventory), nameof(EquipmentInventory.LastModifiedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(Incubation), nameof(Incubation.StartedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(Incubation), nameof(Incubation.CompletedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(Incubation), nameof(Incubation.MinimumDurationOverriddenByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(ItemDocument), nameof(ItemDocument.SupersededByUserId), UserReferenceDisposition.Blocks, "No DB FK - document lifecycle provenance"),
        new UserReferenceEntry(typeof(ItemDocument), nameof(ItemDocument.VoidedByUserId), UserReferenceDisposition.Blocks, "No DB FK - document lifecycle provenance"),
        new UserReferenceEntry(typeof(Material), nameof(Material.CreatedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(Material), nameof(Material.LastModifiedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(MaterialDocument), nameof(MaterialDocument.SupersededByUserId), UserReferenceDisposition.Blocks, "No DB FK - document lifecycle provenance"),
        new UserReferenceEntry(typeof(MaterialDocument), nameof(MaterialDocument.VoidedByUserId), UserReferenceDisposition.Blocks, "No DB FK - document lifecycle provenance"),
        new UserReferenceEntry(typeof(OosInvestigationDocument), nameof(OosInvestigationDocument.SupersededByUserId), UserReferenceDisposition.Blocks, "No DB FK - document lifecycle provenance"),
        new UserReferenceEntry(typeof(OosInvestigationDocument), nameof(OosInvestigationDocument.VoidedByUserId), UserReferenceDisposition.Blocks, "No DB FK - document lifecycle provenance"),
        new UserReferenceEntry(typeof(Media), nameof(Media.PreparedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(Media), nameof(Media.ApprovedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(MediaEvaluation), nameof(MediaEvaluation.CompletedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(MediaEvaluationChallenge), nameof(MediaEvaluationChallenge.ReadByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(MediaUsage), nameof(MediaUsage.UsedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(Report), nameof(Report.GeneratedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(Result), nameof(Result.EnteredByUserId), UserReferenceDisposition.Blocks, "No DB FK - test result provenance"),
        new UserReferenceEntry(typeof(ResultRecord), nameof(ResultRecord.ResultEnteredByUserId), UserReferenceDisposition.Blocks, "No DB FK - test result provenance"),
        new UserReferenceEntry(typeof(ResultRecord), nameof(ResultRecord.ApprovedByUserId), UserReferenceDisposition.Blocks, "No DB FK - test result provenance"),
        new UserReferenceEntry(typeof(ReviewWorkflowEvent), nameof(ReviewWorkflowEvent.PerformedByUserId), UserReferenceDisposition.Blocks, "No DB FK - workflow provenance"),
        new UserReferenceEntry(typeof(SamplePreparation), nameof(SamplePreparation.PreparedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(ItemPreparationConfiguration), nameof(ItemPreparationConfiguration.CreatedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(ItemPreparationConfiguration), nameof(ItemPreparationConfiguration.ApprovedByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(SampleLocation), nameof(SampleLocation.EnteredByUserId), UserReferenceDisposition.Blocks, "No DB FK - provenance"),
        new UserReferenceEntry(typeof(TestOrder), nameof(TestOrder.AssignedAnalystId), UserReferenceDisposition.Blocks, "No DB FK - non-'UserId'-named FK to User, found only by manual review"),
        new UserReferenceEntry(typeof(WorkflowHistory), nameof(WorkflowHistory.PerformedByUserId), UserReferenceDisposition.Blocks, "No DB FK - workflow provenance"),
        new UserReferenceEntry(typeof(WorkflowStepResult), nameof(WorkflowStepResult.AnalystDecisionByUserId), UserReferenceDisposition.Blocks, "No DB FK - workflow provenance"),
        new UserReferenceEntry(typeof(WorkflowStepResult), nameof(WorkflowStepResult.ReturnedByUserId), UserReferenceDisposition.Blocks, "No DB FK - workflow provenance"),
        new UserReferenceEntry(typeof(WorkflowStepResult), nameof(WorkflowStepResult.SubmittedByUserId), UserReferenceDisposition.Blocks, "No DB FK - workflow provenance"),

        // ---- Group B gap entities confirmed during this feature's recon step 0 ----
        new UserReferenceEntry(typeof(MaterialDocumentAccessLog), nameof(MaterialDocumentAccessLog.UserId), UserReferenceDisposition.Blocks, "No DB FK - append-only access log, confirmed same pattern as ItemDocumentAccessLog"),
        new UserReferenceEntry(typeof(EquipmentDocumentAccessLog), nameof(EquipmentDocumentAccessLog.UserId), UserReferenceDisposition.Blocks, "No DB FK - append-only access log, confirmed same pattern as ItemDocumentAccessLog"),
        new UserReferenceEntry(typeof(WorkloadWeight), nameof(WorkloadWeight.ChangedByUserId), UserReferenceDisposition.Blocks, "No DB FK - workload weight configuration provenance"),
        new UserReferenceEntry(typeof(WorkloadWeightHistory), nameof(WorkloadWeightHistory.ChangedByUserId), UserReferenceDisposition.Blocks, "No DB FK - workload weight history provenance"),
    };
}
