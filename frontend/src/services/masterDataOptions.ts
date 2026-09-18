import { apiClient } from "./apiClient";

export type MediaProductOption = {
  id: number;
  name: string;
  code: string;
  configurationCount: number;
  batchCount: number;
  incubationConditionCount: number;
};

type IncubationConditionValues = {
  incubationMinHours: number;
  incubationMaxHours: number;
  temperatureMin: number;
  temperatureMax: number;
};

// A time + temperature pair a media product can be incubated at. The
// product's evaluation configuration and Test Master step media each pick
// one; the server locks a condition once either count is above zero.
export type MediaIncubationConditionOption = IncubationConditionValues & {
  id: number;
  mediaProductId: number;
  configurationCount: number;
  stepMediaCount: number;
};

type MediaConfigurationPayload = {
  mediaProductId: number;
  evaluationType: string;
  mediaIncubationConditionId: number;
  recoveryPercentMin?: number | null;
  recoveryPercentMax?: number | null;
  challenges?: {
    organismId: number;
    challengeRole?: string | null;
    expectedDescription?: string | null;
    initialInoculum?: string | null;
  }[];
};

// Step media send only the chosen condition - the server copies its
// temperature and hours onto the step medium when it saves.
type TestWorkflowStepPayload = {
  stepName: string; incubationMinHours: number; incubationMaxHours: number;
  temperatureMin: number; temperatureMax: number; isFinalStep: boolean; stepType: string;
  targetOrganismId: number | null; phenotypicTestType: string | null; phenotypicTestTypes?: string[];
  stepMedia: { materialId: number; mediaIncubationConditionId: number | null; isRequired: boolean; displayOrder: number }[];
  requiresIncubationTransfer: boolean;
  incubationStages: { stageNumber: number; tempMin: number; tempMax: number; incubationMinHours: number; incubationMaxHours: number }[];
};

export interface EquationTypeDto {
  code: string;
  name: string;
  formulaText: string;
  requiredInputs: string[];
}

export interface CreateTestDefinitionPayload {
  code: string;
  displayName: string;
  sectionId?: number | null;
  workflowType?: string;
  equationType?: string;
  requiresSystemSuitability?: boolean;
  methodAbbreviation?: string | null;
  sstMaxRsdPercent?: number | null;
  sstMinResolution?: number | null;
  sstMaxTailingFactor?: number | null;
  sstMinTheoreticalPlates?: number | null;
}

export interface UpdateTestDefinitionPayload {
  code?: string;
  displayName?: string;
  sectionId?: number | null;
  workflowType?: string;
  equationType?: string;
  requiresSystemSuitability?: boolean;
  methodAbbreviation?: string | null;
  sstMaxRsdPercent?: number | null;
  sstMinResolution?: number | null;
  sstMaxTailingFactor?: number | null;
  sstMinTheoreticalPlates?: number | null;
}

// Shared lookup lists used across receiving, preparation, and master
// data screens. All hit /api/masterdata/*.
export const masterDataOptions = {
  getItems: (category?: string) =>
    apiClient.get("/items").then((r) => (category ? r.data.data.filter((i: { category?: string }) => i.category === category) : r.data.data)),
  getWaterSamplingPoints: () => apiClient.get("/masterdata/water-sampling-points").then((r) => r.data.data),
  getDepartments: () => apiClient.get("/masterdata/departments").then((r) => r.data.data),
  getWaterDepartments: () => apiClient.get("/masterdata/water-departments").then((r) => r.data.data),
  getRooms: () => apiClient.get("/masterdata/rooms").then((r) => r.data.data),
  getMachines: () => apiClient.get("/masterdata/machines").then((r) => r.data.data),
  getCausesOfTesting: () => apiClient.get("/masterdata/causes-of-testing").then((r) => r.data.data),
  createCauseOfTesting: (name: string) =>
    apiClient.post("/masterdata/causes-of-testing", JSON.stringify(name), { headers: { "Content-Type": "application/json" } }).then((r) => r.data.data),
  updateCauseOfTesting: (id: number, name: string) =>
    apiClient.put(`/masterdata/causes-of-testing/${id}`, JSON.stringify(name), { headers: { "Content-Type": "application/json" } }).then((r) => r.data.data),
  deleteCauseOfTesting: (id: number) => apiClient.delete(`/masterdata/causes-of-testing/${id}`),
  getSamplers: () => apiClient.get("/masterdata/samplers").then((r) => r.data.data),
  createSampler: (name: string) =>
    apiClient.post("/masterdata/samplers", JSON.stringify(name), { headers: { "Content-Type": "application/json" } }).then((r) => r.data.data),
  updateSampler: (id: number, name: string) =>
    apiClient.put(`/masterdata/samplers/${id}`, JSON.stringify(name), { headers: { "Content-Type": "application/json" } }).then((r) => r.data.data),
  deleteSampler: (id: number) => apiClient.delete(`/masterdata/samplers/${id}`),
  getProductionStages: () => apiClient.get("/masterdata/production-stages").then((r) => r.data.data),
  createProductionStage: (name: string) =>
    apiClient.post("/masterdata/production-stages", JSON.stringify(name), { headers: { "Content-Type": "application/json" } }).then((r) => r.data.data),
  updateProductionStage: (id: number, name: string) =>
    apiClient.put(`/masterdata/production-stages/${id}`, JSON.stringify(name), { headers: { "Content-Type": "application/json" } }).then((r) => r.data.data),
  deleteProductionStage: (id: number) => apiClient.delete(`/masterdata/production-stages/${id}`),
  getDiluentTypes: () => apiClient.get("/masterdata/diluent-types").then((r) => r.data.data),
  getNeutralizers: () => apiClient.get("/masterdata/neutralizers").then((r) => r.data.data),
  getEquipment: (type?: string) =>
    apiClient.get("/masterdata/equipment", { params: type ? { type } : {} }).then((r) => r.data.data),
  getReleasedMedia: (materialId?: number, opts?: { includeExpired?: boolean; excludeId?: number }) =>
    apiClient.get("/media/released", { params: { ...(materialId ? { materialId } : {}), ...opts } }).then((r) => r.data.data),
  getMediaProducts: () =>
    apiClient.get("/masterdata/media-products").then((r) => r.data.data),
  createMediaProduct: (name: string, code: string) =>
    apiClient.post("/masterdata/media-products", { name, code }).then((r) => r.data.data),
  renameMediaProduct: (id: number, name: string) =>
    apiClient.put(`/masterdata/media-products/${id}`, { name }).then((r) => r.data.data),
  changeMediaProductCode: (id: number, code: string, reason: string, password: string) =>
    apiClient.put(`/masterdata/media-products/${id}/code`, { code, reason, password }).then((r) => r.data.data),
  deleteMediaProduct: (id: number) => apiClient.delete(`/masterdata/media-products/${id}`),
  getMediaIncubationConditions: (mediaProductId?: number): Promise<MediaIncubationConditionOption[]> =>
    apiClient.get("/masterdata/media-incubation-conditions", { params: mediaProductId ? { mediaProductId } : {} }).then((r) => r.data.data),
  createMediaIncubationCondition: (payload: IncubationConditionValues & { mediaProductId: number }) =>
    apiClient.post("/masterdata/media-incubation-conditions", payload).then((r) => r.data.data),
  updateMediaIncubationCondition: (id: number, payload: IncubationConditionValues) =>
    apiClient.put(`/masterdata/media-incubation-conditions/${id}`, payload).then((r) => r.data.data),
  deleteMediaIncubationCondition: (id: number) => apiClient.delete(`/masterdata/media-incubation-conditions/${id}`),
  getMediaConfigurations: () =>
    apiClient.get("/masterdata/media-configurations").then((r) => r.data.data),
  createMediaConfiguration: (payload: MediaConfigurationPayload) =>
    apiClient.post("/masterdata/media-configurations", payload).then((r) => r.data.data),
  updateMediaConfiguration: (id: number, payload: MediaConfigurationPayload) =>
    apiClient.put(`/masterdata/media-configurations/${id}`, payload).then((r) => r.data.data),
  deleteMediaConfiguration: (id: number) => apiClient.delete(`/masterdata/media-configurations/${id}`),
  getOrganisms: () => apiClient.get("/masterdata/organisms").then((r) => r.data.data),
  createOrganism: (scientificName: string, atccNumber?: string | null, commonName?: string | null, description?: string | null) =>
    apiClient.post("/masterdata/organisms", { scientificName, atccNumber: atccNumber || null, commonName: commonName || null, description: description || null }).then((r) => r.data.data),
  updateOrganism: (id: number, scientificName: string, atccNumber?: string | null, commonName?: string | null, description?: string | null) =>
    apiClient.put(`/masterdata/organisms/${id}`, { scientificName, atccNumber: atccNumber || null, commonName: commonName || null, description: description || null }).then((r) => r.data.data),
  deleteOrganism: (id: number) => apiClient.delete(`/masterdata/organisms/${id}`),
  getEquationTypes: (): Promise<EquationTypeDto[]> =>
    apiClient.get("/masterdata/equation-types").then((r) => r.data.data),
  getTestDefinitions: () => apiClient.get("/masterdata/test-definitions").then((r) => r.data.data),
  createTestDefinition: (codeOrPayload: string | CreateTestDefinitionPayload, displayName?: string, sectionId?: number | null) => {
    const payload = typeof codeOrPayload === "string"
      ? { code: codeOrPayload, displayName: displayName ?? codeOrPayload, ...(sectionId != null ? { sectionId } : {}) }
      : codeOrPayload;
    return apiClient.post("/masterdata/test-definitions", payload).then((r) => r.data.data);
  },
  updateTestDefinition: (id: number, codeOrPayload: string | UpdateTestDefinitionPayload, displayName?: string, sectionId?: number | null) => {
    const payload = typeof codeOrPayload === "string"
      ? { code: codeOrPayload, displayName: displayName ?? codeOrPayload, ...(sectionId != null ? { sectionId } : {}) }
      : codeOrPayload;
    return apiClient.put(`/masterdata/test-definitions/${id}`, payload).then((r) => r.data.data);
  },
  freezeTestDefinition: (id: number) =>
    apiClient.put(`/masterdata/test-definitions/${id}/freeze`).then((r) => r.data.data),
  unfreezeTestDefinition: (id: number) =>
    apiClient.put(`/masterdata/test-definitions/${id}/unfreeze`).then((r) => r.data.data),
  updateWorkflowType: (testDefinitionId: number, workflowType: string) =>
    apiClient.put(`/masterdata/test-definitions/${testDefinitionId}/workflow-type`, { workflowType }).then((r) => r.data.data),
  getTestWorkflowSteps: (testDefinitionId: number) =>
    apiClient.get(`/masterdata/test-definitions/${testDefinitionId}/steps`).then((r) => r.data.data),
  getMaterials: (type?: string) =>
    apiClient.get("/inventory/materials", { params: type ? { type } : {} }).then((r) => r.data.data),
  createTestWorkflowStep: (testDefinitionId: number, payload: TestWorkflowStepPayload) =>
    apiClient.post(`/masterdata/test-definitions/${testDefinitionId}/steps`, payload).then((r) => r.data.data),
  updateTestWorkflowStep: (stepId: number, payload: TestWorkflowStepPayload) =>
    apiClient.put(`/masterdata/test-definitions/steps/${stepId}`, payload).then((r) => r.data.data),
  moveTestWorkflowStep: (stepId: number, direction: "up" | "down") =>
    apiClient.put(`/masterdata/test-definitions/steps/${stepId}/move`, { direction }).then((r) => r.data.data),
  deleteTestWorkflowStep: (stepId: number) => apiClient.delete(`/masterdata/test-definitions/steps/${stepId}`)
};

// "18–24 h at 35–37 °C" - how an incubation condition is shown everywhere.
export const incubationConditionLabel = (c: IncubationConditionValues) =>
  `${c.incubationMinHours}–${c.incubationMaxHours} h at ${c.temperatureMin}–${c.temperatureMax} °C`;

// MediaType is a fixed set of 4 rows, one per MediaClass - it no longer
// has a Name/Code, so this is the friendly label used everywhere a
// media type is shown in a dropdown or table.
const MEDIA_CLASS_LABELS: Record<string, string> = {
  GeneralAgar: "General Agar",
  GeneralBroth: "General Broth",
  SelectiveAgar: "Selective Agar",
  SelectiveBroth: "Selective Broth"
};
export const mediaClassLabel = (mediaClass?: string) => (mediaClass && MEDIA_CLASS_LABELS[mediaClass]) || mediaClass || "";

// Media Evaluation - the three named tests, one per MediaClass grouping.
const EVALUATION_TYPE_LABELS: Record<string, string> = {
  GrowthPromotion: "Growth Promotion",
  IndicationInhibition: "Indication / Inhibition",
  EnrichmentCharacteristics: "Enrichment Characteristics"
};
export const evaluationTypeLabel = (t?: string) => (t && EVALUATION_TYPE_LABELS[t]) || t || "";
