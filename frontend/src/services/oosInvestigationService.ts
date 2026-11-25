import { apiClient, ifMatch } from "./apiClient";

export type OosInvestigationKind = "Oos" | "Excursion";

export type OosInvestigationStatus =
  | "PhaseOne"
  | "AwaitingRetest"
  | "PhaseTwo"
  | "Concluded";

export type OosPhaseOneAssessment =
  | "LaboratoryErrorConfirmed"
  | "NoLaboratoryErrorFound";

export type OosConclusion = "Confirmed" | "Invalidated" | "ExcursionClosed";

export type OosEntryKind =
  | "AnalystStatement"
  | "ChecklistAnswer"
  | "OutsideReview"
  | "Hypothesis"
  | "HypothesisOutcome"
  | "Note";

export type OosChecklistAnswer = "Yes" | "No" | "NotApplicable";

export type OosRetestSource = "RetainedSample" | "NewSample";

export type OosDeviationRequestStatus = "Open" | "Addressed";

export type OosOutcomeClass =
  | "Conforms"
  | "Fails"
  | "AlertLimitExceeded"
  | "ActionLimitExceeded";

export interface OosInvestigationFilter {
  status?: OosInvestigationStatus;
  kind?: OosInvestigationKind;
  sectionId?: number;
  overdue?: boolean;
  openOnly?: boolean;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface OosInvestigationListItemDto {
  id: number;
  number: string;
  kind: OosInvestigationKind;
  status: OosInvestigationStatus;
  sampleId: number;
  sampleReferenceNumber: string;
  sampleDisplayName: string;
  batchNumber: string | null;
  sectionId: number;
  sectionName: string;
  openedAtUtc: string;
  openedByName: string;
  phaseOneDueDate: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  conclusion: OosConclusion | null;
  concludedAtUtc: string | null;
  rootCauseCategoryName: string | null;
  testCodes: string[];
  version: number;
}

export interface OosChecklistTemplateItemDto {
  phase: number;
  key: string;
  order: number;
  text: string;
}

export interface OosInvestigationResultDto {
  id: number;
  testOrderId: number;
  testCode: string;
  testDisplayName: string;
  outcomeAtOpening: OosOutcomeClass;
  resultSummaryAtOpening: string;
  addedAtUtc: string;
  isInvalidated: boolean;
  invalidatedAtUtc: string | null;
}

export interface OosInvestigationEntryDto {
  id: number;
  phase: number;
  kind: OosEntryKind;
  checklistItemKey: string | null;
  answer: OosChecklistAnswer | null;
  text: string;
  parentEntryId: number | null;
  recordedByUserId: number;
  recordedByName: string;
  recordedAtUtc: string;
}

export interface OosRetestPlanSampleDto {
  sampleId: number;
  referenceNumber: string;
  status: string;
}

export interface OosRetestPlanDetailDto {
  id: number;
  revision: number;
  source: OosRetestSource;
  retestCount: number;
  testOrderIds: number[];
  analystUserIds: number[];
  judgingRule: string;
  amendmentReason: string | null;
  signedAtUtc: string | null;
  signedByUserId: number | null;
  signedByName: string | null;
  samplesCreatedAtUtc: string | null;
  samples: OosRetestPlanSampleDto[];
}

export interface OosInvestigationReopeningDto {
  id: number;
  reason: string;
  previousConclusion: OosConclusion;
  previousRootCauseCategoryName: string | null;
  previousRootCauseDescription: string | null;
  previousConclusionSummary: string | null;
  previousConcludedByName: string | null;
  previousConcludedAtUtc: string | null;
  reopenedByUserId: number;
  reopenedByName: string;
  reopenedAtUtc: string;
}

export interface OosDeviationRequestDetailDto {
  id: number;
  reason: string;
  requestedByUserId: number;
  requestedByName: string;
  requestedAtUtc: string;
  status: OosDeviationRequestStatus;
  addressedComment: string | null;
  addressedByUserId: number | null;
  addressedByName: string | null;
  addressedAtUtc: string | null;
  version: number;
}

export interface OosSignedTimelineEntryDto {
  signerFullName: string;
  username: string;
  role: string;
  signedAtUtc: string;
  signedAtUtcText: string;
  meaning: string;
  meaningText: string;
  comment: string | null;
}

export interface OosInvestigationDetailDto {
  id: number;
  number: string;
  kind: OosInvestigationKind;
  status: OosInvestigationStatus;
  sampleId: number;
  sampleReferenceNumber: string;
  sampleCategory: string;
  sampleDisplayName: string;
  batchNumber: string | null;
  sectionId: number;
  sectionName: string;
  openedAtUtc: string;
  openedByUserId: number;
  openedByName: string;
  phaseOneDueDate: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  phaseOneAssessment: OosPhaseOneAssessment | null;
  phaseOneCauseCategoryId: number | null;
  phaseOneCauseCategoryName: string | null;
  phaseOneCauseDescription: string | null;
  phaseOneSignedAtUtc: string | null;
  phaseOneSignedByUserId: number | null;
  phaseOneSignedByName: string | null;
  conclusion: OosConclusion | null;
  rootCauseCategoryId: number | null;
  rootCauseCategoryName: string | null;
  rootCauseDescription: string | null;
  conclusionSummary: string | null;
  concludedAtUtc: string | null;
  concludedByUserId: number | null;
  concludedByName: string | null;
  version: number;
  checklistItems: OosChecklistTemplateItemDto[];
  results: OosInvestigationResultDto[];
  entries: OosInvestigationEntryDto[];
  retestPlans: OosRetestPlanDetailDto[];
  reopenings: OosInvestigationReopeningDto[];
  deviationRequests: OosDeviationRequestDetailDto[];
  timeline: OosSignedTimelineEntryDto[];
}

export interface OosEquipmentFact {
  equipmentId: number;
  usedAt: string | null;
  runCalibrationStatus: string | null;
  runCalibrationConfirmedAt: string | null;
  runAssignedAt: string | null;
}

export interface OosMediaFact {
  mediaId: number;
  lotNumber: string;
  isReleasedForUse: boolean;
  expiryDate: string;
}

export interface OosSolutionFact {
  solutionPreparationId: number;
  code: string | null;
  approvalStatus: string | null;
  releasedAt: string | null;
  expiresAt: string | null;
}

export interface OosStandardFact {
  materialId: number;
  purityPercent: number | null;
  moisturePercent: number | null;
}

export interface OosIncubationFact {
  stepNumber: number;
  stageNumber: number;
  startedAt: string;
  completedAt: string | null;
  incubationStartUtc: string | null;
  incubationEndUtc: string | null;
  currentMinimumHours: number | null;
  currentMaximumHours: number | null;
}

export interface OosOrderFactsDto {
  testOrderId: number;
  resultAuditCount: number;
  equipment: OosEquipmentFact[];
  media: OosMediaFact[];
  solutions: OosSolutionFact[];
  standards: OosStandardFact[];
  incubations: OosIncubationFact[];
}

export interface OosChecklistFactsDto {
  version: number;
  orders: OosOrderFactsDto[];
}

export interface SaveRetestPlanRequest {
  source: OosRetestSource;
  retestCount: number;
  testOrderIds: number[];
  analystUserIds: number[];
  judgingRule: string;
  amendmentReason?: string | null;
}

export interface OosRetestPlanDto {
  id: number;
  revision: number;
  investigationVersion: number;
  source: OosRetestSource;
  retestCount: number;
  testOrderIds: number[];
  analystUserIds: number[];
  judgingRule: string;
  amendmentReason: string | null;
}

export interface SignPhaseOneRequest {
  assessment: OosPhaseOneAssessment;
  causeCategoryId: number | null;
  causeDescription: string | null;
  password: string;
}

export interface ConcludeRequest {
  conclusion: OosConclusion;
  rootCauseCategoryId: number | null;
  rootCauseDescription: string | null;
  summary: string;
  password: string;
}

export interface CloseExcursionRequest {
  causeCategoryId: number;
  causeDescription: string;
  actionTaken: string;
  password: string;
}

export interface OosDeviationRequestDto {
  id: number;
  version: number;
}

export interface OosCategoryDto {
  id: number;
  name: string;
  isActive: boolean;
  isNoCauseFound: boolean;
  version: number;
}

export interface OosTemplateItem {
  key: string | null;
  phase: number;
  order: number;
  text: string;
  isActive: boolean;
}

export interface OosTemplateDto {
  version: number;
  phaseOneDueWorkingDays: number | null;
  totalDueWorkingDays: number | null;
  alertOpensExcursionReview: boolean;
  items: OosTemplateItem[];
}

export interface SaveOosTemplateRequest {
  phaseOneDueWorkingDays: number | null;
  totalDueWorkingDays: number | null;
  alertOpensExcursionReview: boolean;
  items: OosTemplateItem[];
}

export interface SaveOosCategoryRequest {
  name: string;
  isActive: boolean;
  reason: string;
}

export interface OosManualOpeningDto {
  id: number;
  number: string;
  version: number;
}

export const oosInvestigationService = {
  async list(
    filter: OosInvestigationFilter = {},
    signal?: AbortSignal
  ): Promise<PagedResult<OosInvestigationListItemDto>> {
    return (
      await apiClient.get("/oos-investigations", { params: filter, signal })
    ).data.data;
  },

  async getById(id: number, signal?: AbortSignal): Promise<OosInvestigationDetailDto> {
    return (await apiClient.get(`/oos-investigations/${id}`, { signal })).data.data;
  },

  async getFacts(id: number, signal?: AbortSignal): Promise<OosChecklistFactsDto> {
    return (await apiClient.get(`/oos-investigations/${id}/facts`, { signal })).data.data;
  },

  async downloadPdf(id: number): Promise<Blob> {
    const res = await apiClient.get(`/oos-investigations/${id}/pdf`, {
      responseType: "blob"
    });
    return res.data;
  },

  async addAnalystStatement(id: number, text: string, version: number): Promise<void> {
    await apiClient.post(
      `/oos-investigations/${id}/phase-one/statements`,
      { text },
      ifMatch(version)
    );
  },

  async answerChecklist(
    id: number,
    phase: number,
    itemKey: string,
    answer: OosChecklistAnswer,
    comment: string | null,
    version: number
  ): Promise<void> {
    await apiClient.post(
      `/oos-investigations/${id}/checklist/${phase}/${encodeURIComponent(itemKey)}`,
      { answer, comment },
      ifMatch(version)
    );
  },

  async signPhaseOne(
    id: number,
    request: SignPhaseOneRequest,
    version: number
  ): Promise<void> {
    await apiClient.post(
      `/oos-investigations/${id}/phase-one/sign`,
      request,
      ifMatch(version)
    );
  },

  async saveRetestPlan(
    id: number,
    request: SaveRetestPlanRequest,
    version: number
  ): Promise<OosRetestPlanDto> {
    return (
      await apiClient.put(
        `/oos-investigations/${id}/retest-plan`,
        request,
        ifMatch(version)
      )
    ).data.data;
  },

  async signRetestPlan(
    planId: number,
    password: string,
    version: number
  ): Promise<void> {
    await apiClient.post(
      `/oos-investigations/retest-plans/${planId}/sign`,
      { password },
      ifMatch(version)
    );
  },

  async addEntry(
    id: number,
    kind: OosEntryKind,
    text: string,
    parentEntryId: number | null,
    version: number
  ): Promise<void> {
    await apiClient.post(
      `/oos-investigations/${id}/entries`,
      { kind, text, parentEntryId },
      ifMatch(version)
    );
  },

  async conclude(
    id: number,
    request: ConcludeRequest,
    version: number
  ): Promise<void> {
    await apiClient.post(
      `/oos-investigations/${id}/conclude`,
      request,
      ifMatch(version)
    );
  },

  async reopen(
    id: number,
    reason: string,
    password: string,
    version: number
  ): Promise<void> {
    await apiClient.post(
      `/oos-investigations/${id}/reopen`,
      { reason, password },
      ifMatch(version)
    );
  },

  async requestDeviation(
    id: number,
    reason: string,
    version: number
  ): Promise<OosDeviationRequestDto> {
    return (
      await apiClient.post(
        `/oos-investigations/${id}/deviation-requests`,
        { reason },
        ifMatch(version)
      )
    ).data.data;
  },

  async addressDeviationRequest(
    requestId: number,
    comment: string,
    version: number
  ): Promise<void> {
    await apiClient.post(
      `/oos-investigations/deviation-requests/${requestId}/address`,
      { comment },
      ifMatch(version)
    );
  },

  async closeExcursion(
    id: number,
    request: CloseExcursionRequest,
    version: number
  ): Promise<void> {
    await apiClient.post(
      `/oos-investigations/${id}/excursion/close`,
      request,
      ifMatch(version)
    );
  },

  async listCategories(
    page = 1,
    pageSize = 100,
    signal?: AbortSignal
  ): Promise<PagedResult<OosCategoryDto>> {
    return (
      await apiClient.get("/oos-templates/categories", {
        params: { page, pageSize },
        signal
      })
    ).data.data;
  },

  async getTemplate(
    sectionId: number,
    kind: OosInvestigationKind,
    signal?: AbortSignal
  ): Promise<OosTemplateDto> {
    return (
      await apiClient.get(`/oos-templates/${sectionId}/${kind}`, { signal })
    ).data.data;
  },

  async saveTemplate(
    sectionId: number,
    kind: OosInvestigationKind,
    request: SaveOosTemplateRequest,
    version: number
  ): Promise<OosTemplateDto> {
    return (
      await apiClient.put(
        `/oos-templates/${sectionId}/${kind}`,
        request,
        ifMatch(version)
      )
    ).data.data;
  },

  async addCategory(request: SaveOosCategoryRequest): Promise<OosCategoryDto> {
    return (await apiClient.post("/oos-templates/categories", request)).data.data;
  },

  async saveCategory(
    categoryId: number,
    request: SaveOosCategoryRequest,
    version: number
  ): Promise<OosCategoryDto> {
    return (
      await apiClient.put(
        `/oos-templates/categories/${categoryId}`,
        request,
        ifMatch(version)
      )
    ).data.data;
  },

  async getForSample(
    sampleId: number,
    signal?: AbortSignal
  ): Promise<OosInvestigationListItemDto[]> {
    return (
      await apiClient.get(`/oos-investigations/for-sample/${sampleId}`, {
        signal
      })
    ).data.data;
  },

  async openManually(
    sampleId: number,
    sectionId: number,
    version: number
  ): Promise<OosManualOpeningDto> {
    return (
      await apiClient.post(
        `/oos-investigations/for-sample/${sampleId}/sections/${sectionId}/open`,
        {},
        ifMatch(version)
      )
    ).data.data;
  },

  async openManualExcursion(
    testOrderId: number,
    reason: string
  ): Promise<OosManualOpeningDto> {
    return (
      await apiClient.post("/oos-investigations/excursions/open", {
        testOrderId,
        reason
      })
    ).data.data;
  }
};
