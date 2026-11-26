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

// ---- Step requests and results (DeviationDtos.cs, CapaDtos.cs, ChangeControlDtos.cs, ComplaintDtos.cs) ----

// QualityRecordDto: the base every type DTO carries as its "record" member.
export interface QualityRecordDto {
  id: number;
  number: string;
  type: QualityRecordType;
  title: string;
  description: string;
  sectionId: number | null;
  raisedByUserId: number | null;
  raisedAtUtc: string;
  ownerUserId: number | null;
  version: number;
  attachments: QualityAttachmentDto[];
}

// What a create call answers with: CapaDto, ChangeControlDto and ComplaintDto all expose the base record, its
// Id and its Version (the other members are read again from the record page).
export interface QualityCreatedDto {
  record: QualityRecordDto;
  id: number;
  version: number;
}

export interface AssessDeviationRequest {
  accept: boolean;
  classificationId: number;
  ownerUserId: number;
  dueDate: string | null;
  password: string;
}

export interface SaveDeviationInvestigationRequest {
  impactAssessment: string;
  rootCauseCategoryId: number;
  rootCauseDescription: string;
  proposedActions: string;
}

export interface ApproveDeviationRequest {
  capaRequired: boolean;
  comment: string | null;
  password: string;
}

// DeviationReasonRequest, CapaReasonRequest, ComplaintReasonRequest and ChangeControlRejectRequest.
export interface QualityReasonRequest {
  reason: string;
  password: string;
}

// DeviationPasswordRequest, CapaPasswordRequest, ChangeControlPasswordRequest and ComplaintPasswordRequest.
export interface QualityPasswordRequest {
  password: string;
}

// ReturnDeviationRequest and ReturnCapaPlanRequest.
export interface QualityCommentRequest {
  comment: string;
}

export interface CreateCapaRequest {
  title: string;
  description: string;
  sectionId: number | null;
  sourceRecordId: number | null;
  links: QualityLinkRequest[] | null;
  ownerUserId: number;
}

export interface CapaActionInput {
  kind: QualityActionKind;
  description: string;
  responsibleUserId: number;
  dueDate: string;
}

export interface SaveCapaPlanRequest {
  actions: CapaActionInput[];
  effectivenessCriteria: string;
  effectivenessMethod: string;
  effectivenessPlannedDate: string;
}

export interface SignCapaEffectivenessRequest {
  result: CapaEffectiveness;
  findings: string;
  newCapaId: number | null;
  password: string;
}

export interface ChangeControlAffectedInput {
  targetType: QualityLinkTargetType;
  targetId: number | null;
  newRecordCode: string | null;
}

export interface ProposeChangeControlRequest {
  title: string;
  description: string;
  reason: string;
  sectionId: number | null;
  affected: ChangeControlAffectedInput[] | null;
  ownerUserId: number | null;
}

export interface ChangeControlTaskInput {
  description: string;
  responsibleUserId: number;
  dueDate: string;
}

export type QualityChecklistAnswerValue = "Yes" | "No" | "NotApplicable";

export interface ChangeControlChecklistAnswerInput {
  itemKey: string;
  answer: QualityChecklistAnswerValue;
  comment: string | null;
}

export interface SaveChangeControlAssessmentRequest {
  impactSummary: string;
  tasks: ChangeControlTaskInput[];
  answers: ChangeControlChecklistAnswerInput[] | null;
}

export interface CloseChangeControlRequest {
  postChangeReview: string;
  unchangedJustification: string | null;
  password: string;
}

export interface ReceiveComplaintRequest {
  title: string;
  source: string;
  receivedOn: string;
  externalReference: string | null;
  itemId: number | null;
  lotText: string | null;
  complaintText: string;
  sectionId: number | null;
}

export interface RequestComplaintInvestigationRequest {
  classificationId: number;
  ownerUserId: number;
  dueDate: string | null;
  whatToTest: string;
}

export interface SaveComplaintInvestigationRequest {
  findings: string;
  sampleIds: number[] | null;
}

export interface ConcludeComplaintRequest {
  outcome: ComplaintOutcome;
  summary: string;
  password: string;
}

// QualityChecklistTemplateDto / QualityChecklistItemDto / QualityAnswerDto (QualityConfigDtos.cs).
export interface QualityChecklistItemDto {
  key: string;
  order: number;
  text: string;
  isActive: boolean;
}

export interface QualityChecklistTemplateDto {
  id: number;
  type: QualityRecordType;
  sectionId: number | null;
  version: number;
  items: QualityChecklistItemDto[];
}

export interface QualityAnswerDto {
  id: number;
  itemKey: string;
  itemText: string;
  answer: QualityChecklistAnswerValue;
  comment: string | null;
  answeredByUserId: number;
  answeredAtUtc: string;
  recordVersion: number;
}

// UserDirectoryDto (UserService.cs): GET users/directory, open to every signed-in user.
export interface QualityUserOption {
  id: number;
  fullName: string;
  username: string;
  jobTitle: string | null;
  roleName: string;
  isActive: boolean;
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
  },

  async listUserDirectory(permission?: string, signal?: AbortSignal): Promise<QualityUserOption[]> {
    return (await apiClient.get("/users/directory", { params: permission ? { permission } : undefined, signal })).data.data;
  },

  async getChecklistTemplate(type: QualityRecordType, sectionId: number | null, signal?: AbortSignal): Promise<QualityChecklistTemplateDto | null> {
    return (await apiClient.get(`/quality/config/templates/${type}`, { params: sectionId === null ? undefined : { sectionId }, signal })).data.data;
  },

  async listChecklistAnswers(recordId: number, page: number, pageSize: number, signal?: AbortSignal): Promise<QualityAnswerDto[]> {
    return (await apiClient.get(`/quality/${recordId}/checklist-answers`, { params: { page, pageSize }, signal })).data.data;
  },

  // ---- Deviation (api/quality/deviations) ----
  async assessDeviation(id: number, request: AssessDeviationRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/deviations/${id}/assess`, request, ifMatch(version));
  },

  async rejectDeviation(id: number, request: QualityReasonRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/deviations/${id}/reject`, request, ifMatch(version));
  },

  async saveDeviationInvestigation(id: number, request: SaveDeviationInvestigationRequest, version: number): Promise<void> {
    await apiClient.put(`/quality/deviations/${id}/investigation`, request, ifMatch(version));
  },

  async signDeviationLabConclusion(id: number, request: QualityPasswordRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/deviations/${id}/lab-conclusion`, request, ifMatch(version));
  },

  async approveDeviation(id: number, request: ApproveDeviationRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/deviations/${id}/approve`, request, ifMatch(version));
  },

  async returnDeviation(id: number, request: QualityCommentRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/deviations/${id}/return`, request, ifMatch(version));
  },

  async closeDeviation(id: number, request: QualityPasswordRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/deviations/${id}/close`, request, ifMatch(version));
  },

  async cancelDeviation(id: number, request: QualityReasonRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/deviations/${id}/cancel`, request, ifMatch(version));
  },

  // ---- CAPA (api/quality/capas) ----
  // Creating needs no record version: the record does not exist yet.
  async createCapa(request: CreateCapaRequest): Promise<QualityCreatedDto> {
    return (await apiClient.post("/quality/capas", request)).data.data;
  },

  async saveCapaPlan(id: number, request: SaveCapaPlanRequest, version: number): Promise<void> {
    await apiClient.put(`/quality/capas/${id}/plan`, request, ifMatch(version));
  },

  async approveCapaPlan(id: number, request: QualityPasswordRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/capas/${id}/approve-plan`, request, ifMatch(version));
  },

  async returnCapaPlan(id: number, request: QualityCommentRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/capas/${id}/return-plan`, request, ifMatch(version));
  },

  async verifyCapaCompletion(id: number, request: QualityPasswordRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/capas/${id}/verify-completion`, request, ifMatch(version));
  },

  async signCapaEffectiveness(id: number, request: SignCapaEffectivenessRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/capas/${id}/effectiveness`, request, ifMatch(version));
  },

  async cancelCapa(id: number, request: QualityReasonRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/capas/${id}/cancel`, request, ifMatch(version));
  },

  // ---- Change control (api/quality/change-controls) ----
  async proposeChangeControl(request: ProposeChangeControlRequest): Promise<QualityCreatedDto> {
    return (await apiClient.post("/quality/change-controls", request)).data.data;
  },

  async addChangeControlAffected(id: number, input: ChangeControlAffectedInput, version: number): Promise<void> {
    await apiClient.post(`/quality/change-controls/${id}/affected`, input, ifMatch(version));
  },

  async saveChangeControlAssessment(id: number, request: SaveChangeControlAssessmentRequest, version: number): Promise<void> {
    await apiClient.put(`/quality/change-controls/${id}/assessment`, request, ifMatch(version));
  },

  async signChangeControlAssessment(id: number, request: QualityPasswordRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/change-controls/${id}/sign-assessment`, request, ifMatch(version));
  },

  async approveChangeControl(id: number, request: QualityPasswordRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/change-controls/${id}/approve`, request, ifMatch(version));
  },

  async rejectChangeControl(id: number, request: QualityReasonRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/change-controls/${id}/reject`, request, ifMatch(version));
  },

  async closeChangeControl(id: number, request: CloseChangeControlRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/change-controls/${id}/close`, request, ifMatch(version));
  },

  async cancelChangeControl(id: number, request: QualityReasonRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/change-controls/${id}/cancel`, request, ifMatch(version));
  },

  // ---- Complaint (api/quality/complaints) ----
  async receiveComplaint(request: ReceiveComplaintRequest): Promise<QualityCreatedDto> {
    return (await apiClient.post("/quality/complaints", request)).data.data;
  },

  async requestComplaintInvestigation(id: number, request: RequestComplaintInvestigationRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/complaints/${id}/request-investigation`, request, ifMatch(version));
  },

  async saveComplaintInvestigation(id: number, request: SaveComplaintInvestigationRequest, version: number): Promise<void> {
    await apiClient.put(`/quality/complaints/${id}/investigation`, request, ifMatch(version));
  },

  async signComplaintLabReport(id: number, request: QualityPasswordRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/complaints/${id}/lab-report`, request, ifMatch(version));
  },

  async concludeComplaint(id: number, request: ConcludeComplaintRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/complaints/${id}/conclude`, request, ifMatch(version));
  },

  async closeComplaint(id: number, request: QualityPasswordRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/complaints/${id}/close`, request, ifMatch(version));
  },

  async cancelComplaint(id: number, request: QualityReasonRequest, version: number): Promise<void> {
    await apiClient.post(`/quality/complaints/${id}/cancel`, request, ifMatch(version));
  }
};
