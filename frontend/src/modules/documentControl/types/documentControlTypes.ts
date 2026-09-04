/**
 * MicroLIMS Document Control Module - Release 1a Frontend Types
 * Matching backend DTOs in MicroLIMS.Application.DTOs.DocumentControl
 */

export type DocumentRecordStatus = "Active" | "Void";

export type DocumentRevisionStatus =
  | "Draft"
  | "InReview"
  | "InApproval"
  | "AwaitingApproval"
  | "FutureEffective"
  | "Approved"
  | "Effective"
  | "Superseded"
  | "Obsolete"
  | "Cancelled";

export type DocumentConfidentiality =
  | "Public"
  | "Internal"
  | "Restricted"
  | "Confidential";

export type RecordOrigin = "Native" | "Migrated";

export type FileRole = "ControlledPdf" | "SourceFile" | "Attachment";

export type AssignmentRole =
  | "Owner"
  | "Author"
  | "TechnicalReviewer"
  | "Approver";

export type RevisionType =
  | "Major"
  | "Minor"
  | "Editorial"
  | "Administrative";

export type ActorType = "User" | "System" | "ExternalSystem" | "MigrationScript";

export type AuditActionCategory =
  | "Document"
  | "Workflow"
  | "Signature"
  | "Security"
  | "Configuration"
  | "System"
  | "Review"
  | "Approval"
  | "Training"
  | "Other";

// ---- DTOs ----

export interface RevisionFileDto {
  id: number;
  documentRevisionId: number;
  fileRole: FileRole;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  contentSha256: string;
  isActive: boolean;
  supersededByFileId: number | null;
  uploadedAt: string;
  uploadedByUserId: number;
  uploadedByUserName: string;
}

export interface DocumentRevisionDto {
  id: number;
  documentMasterId: number;
  revisionNumber: string;
  revisionSequence: number;
  revisionStatus: DocumentRevisionStatus;
  effectiveDate: string | null;
  nextReviewDate: string | null;
  reviewCycleMonths: number;
  revisionType: RevisionType;
  reasonForRevision: string | null;
  changeReference: string | null;
  recordOrigin: RecordOrigin;
  createdAt: string;
  createdByUserId: number;
  createdByUserName: string;
  cancelledAt: string | null;
  cancelledByUserId: number | null;
  cancelledByUserName: string | null;
  cancelReason: string | null;
  files: RevisionFileDto[];
}

export interface DocumentMasterAssignmentDto {
  id: number;
  documentMasterId: number;
  userId: number;
  username: string;
  userFullName: string;
  assignmentRole: AssignmentRole;
  isActive: boolean;
  assignedAt: string;
  assignedByUserId: number;
  assignedByUserName: string;
}

export interface DocumentMasterDto {
  id: number;
  microLimsDocumentId: string;
  companyDocumentCode: string;
  title: string;
  documentTypeId: number;
  documentTypeName: string;
  documentTypeCode: string;
  departmentId: number;
  departmentName: string;
  sectionId: number;
  sectionName: string;
  documentOwnerUserId: number;
  documentOwnerUserName: string;
  confidentiality: DocumentConfidentiality;
  category: string | null;
  recordOrigin: RecordOrigin;
  recordStatus: DocumentRecordStatus;
  currentEffectiveRevisionId: number | null;
  currentRevisionNumber: string | null;
  createdAt: string;
  createdByUserId: number;
  createdByUserName: string;
  modifiedAt: string | null;
  modifiedByUserId: number | null;
  modifiedByUserName: string | null;
  voidedAt: string | null;
  voidedByUserId: number | null;
  voidedByUserName: string | null;
  voidReason: string | null;
  keywords: string[];
  assignments: DocumentMasterAssignmentDto[];
  revisions: DocumentRevisionDto[];
}

export interface DocumentMasterSummaryDto {
  id: number;
  microLimsDocumentId: string;
  companyDocumentCode: string;
  title: string;
  documentTypeId: number;
  documentTypeName: string;
  documentTypeCode: string;
  departmentId: number;
  departmentName: string;
  sectionId: number;
  sectionName: string;
  documentOwnerUserId: number;
  documentOwnerUserName: string;
  confidentiality: DocumentConfidentiality;
  category: string | null;
  recordOrigin: RecordOrigin;
  recordStatus: DocumentRecordStatus;
  currentEffectiveRevisionId: number | null;
  currentRevisionNumber: string | null;
  effectiveDate: string | null;
  nextReviewDate: string | null;
  hasControlledPdf: boolean;
  hasSourceFile: boolean;
  createdAt: string;
}

export interface DocumentLibraryFilterRequest {
  searchTerm?: string;
  documentTypeId?: number;
  departmentId?: number;
  sectionId?: number;
  ownerUserId?: number;
  revisionStatus?: DocumentRevisionStatus;
  recordStatus?: DocumentRecordStatus;
  recordOrigin?: RecordOrigin;
  includeCancelledAndVoided?: boolean;
  isOverdue?: boolean;
  sortBy?: string;
  sortDescending?: boolean;
  page?: number;
  pageSize?: number;
}

export interface DocumentLibraryResponse {
  items: DocumentMasterSummaryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface RegisterDocumentMasterRequest {
  companyDocumentCode: string;
  title: string;
  documentTypeId: number;
  departmentId: number;
  sectionId: number;
  documentOwnerUserId: number;
  confidentiality?: DocumentConfidentiality;
  category?: string;
  keywords?: string[];
  initialRevisionNumber?: string;
  reviewCycleMonths?: number;
  initialAssignments?: Array<{ userId: number; assignmentRole: AssignmentRole }>;
}

export interface UpdateDocumentMasterDraftRequest {
  companyDocumentCode: string;
  title: string;
  documentTypeId: number;
  departmentId: number;
  sectionId: number;
  documentOwnerUserId: number;
  confidentiality?: DocumentConfidentiality;
  category?: string;
  keywords?: string[];
}

export interface VoidDocumentMasterRequest {
  reason: string;
}

export interface CancelDraftRevisionRequest {
  reason: string;
}

export interface CreateAssignmentRequest {
  userId: number;
  assignmentRole: AssignmentRole;
}

// ---- Configuration DTOs ----

export interface DocumentTypeDto {
  id: number;
  code: string;
  name: string;
  defaultReviewCycleMonths: number;
  isActive: boolean;
}

export interface CreateDocumentTypeRequest {
  code: string;
  name: string;
  defaultReviewCycleMonths: number;
}

export interface UpdateDocumentTypeRequest {
  name: string;
  defaultReviewCycleMonths: number;
  isActive: boolean;
}

export interface DocumentSectionDto {
  id: number;
  departmentId: number;
  departmentName: string;
  name: string;
  isActive: boolean;
}

export interface DocumentDepartmentDto {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
  sections: DocumentSectionDto[];
}

export interface CreateDocumentDepartmentRequest {
  code: string;
  name: string;
}

export interface UpdateDocumentDepartmentRequest {
  name: string;
  isActive: boolean;
}

export interface CreateDocumentSectionRequest {
  departmentId: number;
  name: string;
}

export interface UpdateDocumentSectionRequest {
  name: string;
  isActive: boolean;
}

export interface DocumentNumberingConfigDto {
  id: number;
  prefix: string;
  numberFormat: string;
  isEnabled: boolean;
  modifiedAt: string;
  modifiedByUserId: number;
  modifiedByUserName: string;
  sampleNextId: string;
}

export interface UpdateDocumentNumberingConfigRequest {
  prefix: string;
  numberFormat: string;
  isEnabled: boolean;
}

export interface ConfigurationSettingDto {
  id: number;
  settingKey: string;
  settingValue: string;
  dataType: string;
  settingGroup: string;
  modifiedAt: string;
  modifiedByUserId: number | null;
  modifiedByUserName: string | null;
}

export interface UpdateConfigurationSettingRequest {
  settingValue: string;
}

// ---- Audit DTOs ----

export interface AuditEventChangeDto {
  fieldName: string;
  previousValue: string | null;
  newValue: string | null;
}

export interface DocumentAuditItemDto {
  id: number;
  eventUid: string;
  timestamp: string;
  actorType: ActorType;
  userId: number | null;
  userName: string | null;
  systemProcessName: string | null;
  action: string;
  actionCode: string;
  actionCategory: AuditActionCategory;
  recordType: string;
  entityId: string;
  reason: string | null;
  sourceContext: string | null;
  correlationId: string | null;
  documentMasterId: number | null;
  documentRevisionId: number | null;
  changes: AuditEventChangeDto[];
}

export interface DocumentAuditFilterRequest {
  searchTerm?: string;
  actionCategory?: AuditActionCategory;
  actionCode?: string;
  recordType?: string;
  userId?: number;
  documentMasterId?: number;
  documentRevisionId?: number;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

export interface DocumentAuditResponse {
  items: DocumentAuditItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ==========================================
// Release 1b: Technical Review Types
// ==========================================

export type ReviewTaskStatus =
  | "Pending"
  | "InProgress"
  | "Completed"
  | "ReturnedForCorrection"
  | "Cancelled";

export type ReviewDecision = "ReturnForCorrection" | "CompleteReview";

export type ReviewFindingStatus =
  | "Open"
  | "AuthorResponded"
  | "ReviewerVerified"
  | "Resolved";

export interface DocumentReviewFindingDto {
  id: number;
  documentReviewTaskId: number;
  createdByUserId: number;
  createdByUsername: string;
  createdByFullName: string;
  createdAt: string;
  pageNumber?: number | null;
  sectionNumber?: string | null;
  commentText: string;
  isMandatory: boolean;
  status: ReviewFindingStatus;
  authorResponse?: string | null;
  authorResponseAt?: string | null;
  authorResponseUsername?: string | null;
  reviewerVerificationNotes?: string | null;
  reviewerVerifiedAt?: string | null;
  reviewerVerifiedUsername?: string | null;
  resolvedAt?: string | null;
  resolvedByUsername?: string | null;
}

export interface DocumentReviewTaskDto {
  id: number;
  documentMasterId: number;
  microLimsDocumentId: string;
  companyDocumentCode: string;
  documentTitle: string;
  documentRevisionId: number;
  revisionNumber: string;
  revisionStatus: DocumentRevisionStatus;
  assignedReviewerUserId: number;
  assignedReviewerUsername: string;
  assignedReviewerFullName: string;
  assignedByUserId: number;
  assignedByUsername: string;
  assignedAt: string;
  dueDate?: string | null;
  status: ReviewTaskStatus;
  decision?: ReviewDecision | null;
  decisionAt?: string | null;
  decisionByUsername?: string | null;
  reviewNotes?: string | null;
  submissionNotes?: string | null;
  totalFindingsCount: number;
  openMandatoryFindingsCount: number;
  findings: DocumentReviewFindingDto[];
}

export interface SubmitForReviewRequest {
  reviewerUserId: number;
  submissionNotes?: string;
  dueDate?: string;
}

export interface AddReviewFindingRequest {
  pageNumber?: number | null;
  sectionNumber?: string | null;
  commentText: string;
  isMandatory: boolean;
}

export interface RespondToFindingRequest {
  response: string;
}

export interface VerifyFindingRequest {
  verificationNotes?: string;
}

export interface ReviewDecisionRequest {
  decision: ReviewDecision;
  reviewNotes?: string;
}

// ==========================================
// Release 1b: WP2 Revision Management Types
// ==========================================

export interface CreateRevisionRequest {
  revisionType: RevisionType;
  reasonForRevision: string;
  changeSummary?: string;
  changeReference?: string;
  customRevisionNumber?: string;
  originatingPeriodicReviewTaskId?: number;
}

export interface ProposeNextRevisionRequest {
  revisionType: RevisionType;
}

export interface ProposeNextRevisionResponse {
  proposedRevisionNumber: string;
  revisionType: RevisionType;
  currentEffectiveRevisionNumber: string;
}

export interface RevisionChangeItemDto {
  id: number;
  documentRevisionId: number;
  sectionNumber: string;
  sectionTitle: string;
  descriptionOfChange: string;
  changeRationale: string;
  changeCategory: string;
  status: string;
  originatingReviewFindingId?: number | null;
  createdByUserId: number;
  createdByUsername: string;
  createdByFullName: string;
  createdAt: string;
  modifiedAt?: string | null;
}

export interface AddChangeItemRequest {
  sectionNumber: string;
  sectionTitle: string;
  descriptionOfChange: string;
  changeRationale: string;
  changeCategory: string;
  status?: string;
  originatingReviewFindingId?: number | null;
}

export interface UpdateChangeItemRequest {
  sectionNumber: string;
  sectionTitle: string;
  descriptionOfChange: string;
  changeRationale: string;
  changeCategory: string;
  status: string;
}

export interface ConvertFindingRequest {
  sectionTitle: string;
  changeRationale: string;
  changeCategory: string;
}

export interface RevisionImpactAssessmentDto {
  id: number;
  documentRevisionId: number;
  procedureOrMethodImpact: boolean;
  procedureOrMethodDetails?: string | null;
  trainingImpact: boolean;
  trainingDetails?: string | null;
  formsOrTemplatesImpact: boolean;
  formsOrTemplatesDetails?: string | null;
  specificationsImpact: boolean;
  specificationsDetails?: string | null;
  equipmentImpact: boolean;
  equipmentDetails?: string | null;
  materialsOrMediaImpact: boolean;
  materialsOrMediaDetails?: string | null;
  validationImpact: boolean;
  validationDetails?: string | null;
  regulatoryCommitmentImpact: boolean;
  regulatoryCommitmentDetails?: string | null;
  relatedDocumentsImpact: boolean;
  relatedDocumentsDetails?: string | null;
  isComplete: boolean;
  completedByUserId: number;
  completedByUsername: string;
  completedByFullName: string;
  completedAt: string;
}

export interface SaveImpactAssessmentRequest {
  procedureOrMethodImpact: boolean;
  procedureOrMethodDetails?: string | null;
  trainingImpact: boolean;
  trainingDetails?: string | null;
  formsOrTemplatesImpact: boolean;
  formsOrTemplatesDetails?: string | null;
  specificationsImpact: boolean;
  specificationsDetails?: string | null;
  equipmentImpact: boolean;
  equipmentDetails?: string | null;
  materialsOrMediaImpact: boolean;
  materialsOrMediaDetails?: string | null;
  validationImpact: boolean;
  validationDetails?: string | null;
  regulatoryCommitmentImpact: boolean;
  regulatoryCommitmentDetails?: string | null;
  relatedDocumentsImpact: boolean;
  relatedDocumentsDetails?: string | null;
}

// ---- Release 1b WP3 Approval Workflow Types ----

export type DocumentApprovalTaskStatus =
  | "Pending"
  | "Approved"
  | "ReturnedForCorrection"
  | "Declined"
  | "Cancelled";

export type DocumentApprovalDecision =
  | "Approve"
  | "ReturnForCorrection"
  | "Decline";

export interface DocumentApprovalTaskDto {
  id: number;
  documentRevisionId: number;
  documentMasterId: number;
  companyDocumentCode: string;
  documentTitle: string;
  revisionNumber: string;
  revisionSequence: number;
  assignedApproverUserId: number;
  assignedApproverUsername: string;
  assignedApproverFullName: string;
  assignedByUserId: number;
  assignedByUsername: string;
  assignedByFullName: string;
  assignedAt: string;
  dueDate?: string | null;
  status: DocumentApprovalTaskStatus;
  decision?: DocumentApprovalDecision | null;
  decisionAt?: string | null;
  decisionByUserId?: number | null;
  decisionByUsername?: string | null;
  decisionByFullName?: string | null;
  decisionNotes?: string | null;
  submissionNotes?: string | null;
  targetEffectiveDate?: string | null;
}

export interface CreateApprovalTaskRequest {
  approverUserId: number;
  dueDate?: string | null;
  submissionNotes?: string | null;
  targetEffectiveDate?: string | null;
}

export interface ApprovalReadinessDto {
  isReady: boolean;
  validationErrors: string[];
  requiresElectronicSignature: boolean;
  targetStatusOnApproval: DocumentRevisionStatus;
  hasActiveControlledPdf: boolean;
  isTechnicalReviewCompleted: boolean;
  areMandatoryFindingsResolved: boolean;
  isImpactAssessmentCompleted: boolean;
  areChangeItemsAddressed: boolean;
  isSegregationOfDutiesSatisfied: boolean;
}

export interface ElectronicSignatureDto {
  id: number;
  userId: number;
  userFullNameSnapshot: string;
  usernameSnapshot: string;
  roleSnapshot: string;
  meaningOfSignature: string;
  entityType: string;
  entityId: number;
  signedAt: string;
  comment?: string | null;
  ipAddress?: string | null;
}

export interface ApprovalDossierDto {
  task: DocumentApprovalTaskDto;
  revision: DocumentRevisionDto;
  master: DocumentMasterDto;
  controlledPdf?: RevisionFileDto | null;
  changeItems: RevisionChangeItemDto[];
  impactAssessment?: RevisionImpactAssessmentDto | null;
  reviewHistory: DocumentReviewTaskDto[];
  readiness: ApprovalReadinessDto;
  activeSignature?: ElectronicSignatureDto | null;
}

export interface ExecuteApprovalDecisionRequest {
  decision: DocumentApprovalDecision;
  decisionNotes?: string | null;
  effectiveDate?: string | null;
  password?: string | null;
  signatureToken?: string | null;
}

export interface SignApprovalRequest {
  password: string;
  decisionNotes?: string | null;
  effectiveDate?: string | null;
}

// ---- Periodic Review Types (WP6) ----

export type PeriodicReviewTaskStatus = "Pending" | "InProgress" | "Completed" | "Cancelled";
export type PeriodicReviewOutcome = "RemainsValid" | "RevisionRequired" | "ObsolescenceRecommended";
export type PeriodicReviewFindingStatus = "Open" | "Resolved";

export interface PeriodicReviewFindingDto {
  id: number;
  periodicReviewTaskId: number;
  pageNumber?: number | null;
  sectionNumber?: string | null;
  noteText: string;
  status: PeriodicReviewFindingStatus;
  createdByUserId: number;
  createdByUsername: string;
  createdByFullName: string;
  createdAt: string;
}

export interface PeriodicReviewTaskDto {
  id: number;
  documentMasterId: number;
  microLimsDocumentId: string;
  companyDocumentCode: string;
  documentTitle: string;
  documentRevisionId: number;
  revisionNumber: string;
  reviewCycleMonths: number;
  scheduledDueDate: string;
  isOverdue: boolean;
  assignedReviewerUserId?: number | null;
  assignedReviewerUsername?: string | null;
  assignedReviewerFullName?: string | null;
  createdAt: string;
  status: PeriodicReviewTaskStatus;
  outcome?: PeriodicReviewOutcome | null;
  completedAt?: string | null;
  completedByUserId?: number | null;
  completedByUsername?: string | null;
  completedByFullName?: string | null;
  reviewSummary?: string | null;
  totalFindingsCount: number;
  findings: PeriodicReviewFindingDto[];
}

export interface CreatePeriodicReviewFindingRequest {
  pageNumber?: number | null;
  sectionNumber?: string | null;
  noteText: string;
}

export interface CompletePeriodicReviewRequest {
  outcome: PeriodicReviewOutcome;
  reviewSummary: string;
  proposedObsolescenceReason?: string | null;
}

export interface AssignPeriodicReviewerRequest {
  reviewerUserId: number;
}

export interface PeriodicReviewWorkspaceDto {
  task: PeriodicReviewTaskDto;
  master: DocumentMasterSummaryDto;
  revision: DocumentRevisionDto;
  controlledPdf?: RevisionFileDto | null;
  findings: PeriodicReviewFindingDto[];
  historicalReviews: PeriodicReviewTaskDto[];
}

export interface PeriodicReviewGenerationResultDto {
  evaluatedMastersCount: number;
  createdTasksCount: number;
  skippedCount: number;
  createdTaskIds: number[];
  errors: string[];
}
