import { apiClient, ifMatch } from "./apiClient";
import type { PagedResult } from "./oosInvestigationService";

// Mirrors backend/MicroLIMS.Application/DTOs/Quality*Dtos.cs, CapaDtos.cs and ChangeControlDtos.cs.
// Enums travel as their names (ApiJsonOptions adds JsonStringEnumConverter); DateOnly as "yyyy-MM-dd";
// DateTime as an ISO string in UTC. Every mutation sends the record's Version as If-Match.

export type QualityRecordType = "Deviation" | "Capa" | "ChangeControl" | "Complaint";

export type QualityDeviationStatus =
  | "Raised" | "Investigation" | "LabConcluded" | "Approved" | "Closed" | "Rejected" | "Cancelled";
export type CapaStatus = "Plan" | "PlanApproved" | "CompletionVerified" | "Closed" | "Cancelled";
export type CapaEffectiveness = "Effective" | "NotEffective";
export type ChangeControlStatus =
  | "Proposed" | "Assessed" | "ApprovedForImplementation" | "Closed" | "Rejected" | "Cancelled";
export type ComplaintStatus =
  | "Received" | "Investigation" | "LabReported" | "Concluded" | "Closed" | "Cancelled";
export type ComplaintOutcome = "Justified" | "NotJustified" | "Inconclusive";

export type QualityActionKind = "Corrective" | "Preventive" | "ChangeTask";

export type QualityLinkTargetType =
  | "Sample" | "TestOrder" | "OosInvestigation" | "MonitoringDeviation" | "Equipment" | "MediaLot"
  | "StockLot" | "Item" | "Specification" | "TestDefinition" | "DocumentMaster" | "DocumentRevision"
  | "AuditTrailReviewFinding" | "QualityRecord";

export type QualityStepKind =
  | "DeviationAssessed" | "DeviationRejected" | "DeviationLabConclusion" | "DeviationApproved"
  | "DeviationClosed" | "CapaPlanApproved" | "CapaCompletionVerified" | "CapaEffectivenessChecked"
  | "ChangeImpactAssessed" | "ChangeApproved" | "ChangeRejected" | "ChangeClosed"
  | "ComplaintLabReport" | "ComplaintConcluded" | "ComplaintClosed" | "QualityCancelled"
  | "QualityExtensionDecided";

export type QualityExtensionDecision = "Approved" | "Refused";

export const QUALITY_RECORD_TYPES: readonly QualityRecordType[] = ["Deviation", "Capa", "ChangeControl", "Complaint"];

// QualityRecordFilter (QualityQueryDtos.cs). from/to are sent as ISO date-times.
export interface QualityRecordFilter {
  type?: QualityRecordType;
  status?: string;
  classificationId?: number;
  sectionId?: number;
  ownerUserId?: number;
  overdue?: boolean;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface QualityRecordListItemDto {
  id: number;
  number: string;
  type: QualityRecordType;
  title: string;
  status: string;
  sectionId: number | null;
  sectionName: string | null;
  classificationId: number | null;
  classificationName: string | null;
  raisedByUserId: number | null;
  raisedByName: string | null;
  raisedAtUtc: string;
  ownerUserId: number | null;
  ownerName: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  isCancelled: boolean;
  version: number;
}

export interface QualityPathStepDto {
  stepKey: string;
  label: string;
  isCompleted: boolean;
  isCurrent: boolean;
  completedAtUtc: string | null;
  completedByName: string | null;
}

export interface QualitySignedTimelineEntryDto {
  stepId: number;
  kind: QualityStepKind;
  signerFullName: string;
  username: string;
  role: string;
  signedAtUtc: string;
  signedAtUtcText: string;
  meaning: string;
  meaningText: string;
  comment: string | null;
}

export interface QualityActionDetailDto {
  id: number;
  kind: QualityActionKind;
  description: string;
  responsibleUserId: number;
  responsibleName: string;
  dueDate: string;
  doneAtUtc: string | null;
  doneByUserId: number | null;
  doneByName: string | null;
  evidence: string | null;
  evidenceLinkId: number | null;
  version: number;
  isOverdue: boolean;
}

export interface QualityAttachmentDto {
  id: number;
  fileName: string;
  contentType: string;
  size: number;
  uploadedByUserId: number;
  uploadedByName: string;
  uploadedAtUtc: string;
  voidedAtUtc: string | null;
  voidedByUserId: number | null;
  voidedByName: string | null;
  voidReason: string | null;
  isVoided: boolean;
}

export interface QualityDeviationDetailDto {
  status: QualityDeviationStatus;
  occurredAtUtc: string;
  location: string | null;
  immediateAction: string;
  impactAssessment: string | null;
  rootCauseCategoryId: number | null;
  rootCauseCategoryName: string | null;
  rootCauseDescription: string | null;
  proposedActions: string | null;
  capaRequired: boolean | null;
  rejectionReason: string | null;
}

export interface QualityCapaDetailDto {
  status: CapaStatus;
  sourceRecordId: number | null;
  sourceRecordNumber: string | null;
  effectivenessCriteria: string | null;
  effectivenessMethod: string | null;
  effectivenessPlannedDate: string | null;
  effectivenessResult: CapaEffectiveness | null;
  effectivenessFindings: string | null;
  successorCapaId: number | null;
  successorCapaNumber: string | null;
}

export interface QualityChangeControlDetailDto {
  status: ChangeControlStatus;
  reason: string;
  impactSummary: string | null;
  rejectionReason: string | null;
  postChangeReview: string | null;
  unchangedJustification: string | null;
}

export interface QualityComplaintDetailDto {
  status: ComplaintStatus;
  source: string;
  receivedOn: string;
  externalReference: string | null;
  itemId: number | null;
  itemName: string | null;
  lotText: string | null;
  complaintText: string;
  whatToTest: string | null;
  findings: string | null;
  outcome: ComplaintOutcome | null;
  conclusionSummary: string | null;
}

export interface QualityRecordDetailDto {
  id: number;
  number: string;
  type: QualityRecordType;
  title: string;
  description: string;
  status: string;
  sectionId: number | null;
  sectionName: string | null;
  classificationId: number | null;
  classificationName: string | null;
  raisedByUserId: number | null;
  raisedByName: string | null;
  raisedAtUtc: string;
  ownerUserId: number | null;
  ownerName: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  isCancelled: boolean;
  cancelReason: string | null;
  version: number;
  deviation: QualityDeviationDetailDto | null;
  capa: QualityCapaDetailDto | null;
  changeControl: QualityChangeControlDetailDto | null;
  complaint: QualityComplaintDetailDto | null;
  pathSteps: QualityPathStepDto[];
  timeline: QualitySignedTimelineEntryDto[];
  actions: QualityActionDetailDto[];
  attachments: QualityAttachmentDto[];
  linksCount: number;
}

// QualityLinkDto: a restricted link carries only its number (label and route are null).
export interface QualityLinkDto {
  id: number;
  number: string | null;
  label: string | null;
  route: string | null;
  isRestricted: boolean;
}

export interface QualityLinkRequest {
  targetType: QualityLinkTargetType;
  targetId: number;
}

export interface QualityCommentDto {
  id: number;
  text: string;
  authorUserId: number;
  authorName: string;
  createdAtUtc: string;
}

export interface QualityClassificationDto {
  id: number;
  name: string;
  order: number;
  isActive: boolean;
  version: number;
}

export interface RequestQualityExtensionRequest {
  actionId: number | null;
  requestedDueDate: string;
  reason: string;
}

export interface DecideQualityExtensionRequest {
  approve: boolean;
  comment: string | null;
  password: string;
}

export interface QualityExtensionDto {
  id: number;
  recordId: number;
  actionId: number | null;
  requestedDueDate: string;
  reason: string;
  requestedByUserId: number;
  requestedByName: string;
  requestedAtUtc: string;
  decision: QualityExtensionDecision | null;
  decidedByUserId: number | null;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionComment: string | null;
  signatureId: number | null;
  version: number;
  recordVersion: number;
}

// CompleteCapaActionRequest and CompleteChangeControlTaskRequest have the same shape.
export interface CompleteQualityActionRequest {
  evidence: string;
  evidenceLinkId: number | null;
}

export const qualityService = {
  async listRecords(filter: QualityRecordFilter, signal?: AbortSignal): Promise<PagedResult<QualityRecordListItemDto>> {
    return (await apiClient.get("/quality/records", { params: filter, signal })).data.data;
  },

  async getRecordDetail(id: number, signal?: AbortSignal): Promise<QualityRecordDetailDto> {
    return (await apiClient.get(`/quality/records/${id}`, { signal })).data.data;
  },

  async downloadPdf(id: number): Promise<Blob> {
    return (await apiClient.get(`/quality/records/${id}/pdf`, { responseType: "blob" })).data;
  },

  async listClassifications(page: number, pageSize: number, signal?: AbortSignal): Promise<QualityClassificationDto[]> {
    return (await apiClient.get("/quality/config/classifications", { params: { page, pageSize }, signal })).data.data;
  },

  async listLinks(recordId: number, offset: number, pageSize: number, signal?: AbortSignal): Promise<PagedResult<QualityLinkDto>> {
    return (await apiClient.get(`/quality/${recordId}/links`, { params: { offset, pageSize }, signal })).data.data;
  },

  async addLink(recordId: number, request: QualityLinkRequest, version: number): Promise<QualityLinkDto> {
    return (await apiClient.post(`/quality/${recordId}/links`, request, ifMatch(version))).data.data;
  },

  async removeLink(recordId: number, linkId: number, version: number): Promise<void> {
    await apiClient.delete(`/quality/${recordId}/links/${linkId}`, ifMatch(version));
  },

  async listComments(recordId: number, offset: number, pageSize: number, signal?: AbortSignal): Promise<QualityCommentDto[]> {
    return (await apiClient.get(`/quality/${recordId}/comments`, { params: { offset, pageSize }, signal })).data.data;
  },

  async addComment(recordId: number, text: string, version: number): Promise<QualityCommentDto> {
    return (await apiClient.post(`/quality/${recordId}/comments`, { text }, ifMatch(version))).data.data;
  },

  async uploadAttachment(recordId: number, file: File, version: number): Promise<QualityAttachmentDto> {
    const form = new FormData();
    form.append("file", file);
    return (await apiClient.post(`/quality/${recordId}/attachments`, form, ifMatch(version))).data.data;
  },

  async downloadAttachment(recordId: number, attachmentId: number): Promise<Blob> {
    return (await apiClient.get(`/quality/${recordId}/attachments/${attachmentId}`, { responseType: "blob" })).data;
  },

  async voidAttachment(recordId: number, attachmentId: number, reason: string, version: number): Promise<QualityAttachmentDto> {
    return (await apiClient.post(`/quality/${recordId}/attachments/${attachmentId}/void`, { reason }, ifMatch(version))).data.data;
  },

  async listExtensions(recordId: number, page: number, pageSize: number, signal?: AbortSignal): Promise<QualityExtensionDto[]> {
    return (await apiClient.get(`/quality/${recordId}/extensions`, { params: { page, pageSize }, signal })).data.data;
  },

  async requestExtension(recordId: number, request: RequestQualityExtensionRequest, version: number): Promise<QualityExtensionDto> {
    return (await apiClient.post(`/quality/${recordId}/extensions`, request, ifMatch(version))).data.data;
  },

  async decideExtension(requestId: number, request: DecideQualityExtensionRequest, version: number): Promise<QualityExtensionDto> {
    return (await apiClient.post(`/quality/extensions/${requestId}/decision`, request, ifMatch(version))).data.data;
  },

  // The CAPA and the change-control controllers each own their action route; both answer with the whole
  // record, which the caller discards and reloads.
  async completeCapaAction(actionId: number, request: CompleteQualityActionRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/capas/actions/${actionId}/complete`, request, ifMatch(version));
  },

  async completeChangeControlTask(actionId: number, request: CompleteQualityActionRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/change-controls/tasks/${actionId}/complete`, request, ifMatch(version));
  }
};
