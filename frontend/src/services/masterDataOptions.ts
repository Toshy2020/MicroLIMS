import { apiClient } from "./apiClient";

// Shared lookup lists used across receiving, preparation, and master
// data screens. All hit /api/masterdata/*.
export const masterDataOptions = {
  getItems: (category?: string) =>
    apiClient.get("/items").then((r) => (category ? r.data.data.filter((i: any) => i.category === category) : r.data.data)),
  getWaterSamplingPoints: () => apiClient.get("/masterdata/water-sampling-points").then((r) => r.data.data),
  getDepartments: () => apiClient.get("/masterdata/departments").then((r) => r.data.data),
  getRooms: () => apiClient.get("/masterdata/rooms").then((r) => r.data.data),
  getMachines: () => apiClient.get("/masterdata/machines").then((r) => r.data.data),
  getCausesOfTesting: () => apiClient.get("/masterdata/causes-of-testing").then((r) => r.data.data),
  getDiluentTypes: () => apiClient.get("/masterdata/diluent-types").then((r) => r.data.data),
  getNeutralizers: () => apiClient.get("/masterdata/neutralizers").then((r) => r.data.data),
  getEquipment: (type?: string) =>
    apiClient.get("/masterdata/equipment", { params: type ? { type } : {} }).then((r) => r.data.data),
  getMediaTypes: () => apiClient.get("/masterdata/media-types").then((r) => r.data.data),
  updateMediaType: (id: number, payload: any) => apiClient.put(`/masterdata/media-types/${id}`, payload).then((r) => r.data.data),
  getReleasedMedia: (mediaTypeId?: number) =>
    apiClient.get("/media/released", { params: mediaTypeId ? { mediaTypeId } : {} }).then((r) => r.data.data),
  getMediaChallengeSpecs: (params?: { materialName?: string; evaluationType?: string }) =>
    apiClient.get("/masterdata/media-challenge-specs", { params: params ?? {} }).then((r) => r.data.data),
  createMediaChallengeSpec: (payload: { materialName: string; evaluationType: string; organismId: number; challengeRole?: string | null; expectedDescription?: string | null }) =>
    apiClient.post("/masterdata/media-challenge-specs", payload).then((r) => r.data.data),
  updateMediaChallengeSpec: (id: number, payload: { materialName: string; evaluationType: string; organismId: number; challengeRole?: string | null; expectedDescription?: string | null }) =>
    apiClient.put(`/masterdata/media-challenge-specs/${id}`, payload).then((r) => r.data.data),
  deleteMediaChallengeSpec: (id: number) => apiClient.delete(`/masterdata/media-challenge-specs/${id}`),
  getApprovedMedia: (testCode: string) =>
    apiClient.get("/test-definition-media-lookup/approved-media", { params: { testCode } }).then((r) => r.data.data),
  getOrganisms: () => apiClient.get("/masterdata/organisms").then((r) => r.data.data),
  createOrganism: (scientificName: string, atccNumber?: string | null, commonName?: string | null) =>
    apiClient.post("/masterdata/organisms", { scientificName, atccNumber: atccNumber || null, commonName: commonName || null }).then((r) => r.data.data),
  updateOrganism: (id: number, scientificName: string, atccNumber?: string | null, commonName?: string | null) =>
    apiClient.put(`/masterdata/organisms/${id}`, { scientificName, atccNumber: atccNumber || null, commonName: commonName || null }).then((r) => r.data.data),
  deleteOrganism: (id: number) => apiClient.delete(`/masterdata/organisms/${id}`),
  getTestDefinitions: () => apiClient.get("/masterdata/test-definitions").then((r) => r.data.data),
  createTestDefinition: (code: string, displayName: string) =>
    apiClient.post("/masterdata/test-definitions", { code, displayName }).then((r) => r.data.data),
  updateTestDefinition: (id: number, code: string, displayName: string) =>
    apiClient.put(`/masterdata/test-definitions/${id}`, { code, displayName }).then((r) => r.data.data),
  freezeTestDefinition: (id: number) =>
    apiClient.put(`/masterdata/test-definitions/${id}/freeze`).then((r) => r.data.data),
  unfreezeTestDefinition: (id: number) =>
    apiClient.put(`/masterdata/test-definitions/${id}/unfreeze`).then((r) => r.data.data),
  getTestDefinitionMedia: (testDefinitionId: number) =>
    apiClient.get("/masterdata/test-definition-media", { params: { testDefinitionId } }).then((r) => r.data.data),
  createTestDefinitionMedia: (testDefinitionId: number, mediaTypeId: number, stepName?: string) =>
    apiClient.post("/masterdata/test-definition-media", { testDefinitionId, mediaTypeId, stepName: stepName || null }).then((r) => r.data.data),
  updateTestDefinitionMedia: (id: number, mediaTypeId: number, stepName?: string) =>
    apiClient.put(`/masterdata/test-definition-media/${id}`, { mediaTypeId, stepName: stepName || null }).then((r) => r.data.data),
  deleteTestDefinitionMedia: (id: number) => apiClient.delete(`/masterdata/test-definition-media/${id}`),
  updateWorkflowType: (testDefinitionId: number, workflowType: string) =>
    apiClient.put(`/masterdata/test-definitions/${testDefinitionId}/workflow-type`, { workflowType }).then((r) => r.data.data),
  getTestWorkflowSteps: (testDefinitionId: number) =>
    apiClient.get(`/masterdata/test-definitions/${testDefinitionId}/steps`).then((r) => r.data.data),
  getMaterials: (type?: string) =>
    apiClient.get("/inventory/materials", { params: type ? { type } : {} }).then((r) => r.data.data),
  createTestWorkflowStep: (testDefinitionId: number, payload: {
    stepName: string; mediaTypeId: number; incubationMinHours: number; incubationMaxHours: number;
    temperatureMin: number; temperatureMax: number; isFinalStep: boolean; stepType: string;
    targetOrganismId: number | null;
    stepMedia: { materialId: number; tempMin: number; tempMax: number; isRequired: boolean; displayOrder: number }[];
  }) => apiClient.post(`/masterdata/test-definitions/${testDefinitionId}/steps`, payload).then((r) => r.data.data),
  updateTestWorkflowStep: (stepId: number, payload: {
    stepName: string; mediaTypeId: number; incubationMinHours: number; incubationMaxHours: number;
    temperatureMin: number; temperatureMax: number; isFinalStep: boolean; stepType: string;
    targetOrganismId: number | null;
    stepMedia: { materialId: number; tempMin: number; tempMax: number; isRequired: boolean; displayOrder: number }[];
  }) => apiClient.put(`/masterdata/test-definitions/steps/${stepId}`, payload).then((r) => r.data.data),
  moveTestWorkflowStep: (stepId: number, direction: "up" | "down") =>
    apiClient.put(`/masterdata/test-definitions/steps/${stepId}/move`, { direction }).then((r) => r.data.data),
  deleteTestWorkflowStep: (stepId: number) => apiClient.delete(`/masterdata/test-definitions/steps/${stepId}`)
};

// "Sampled By" is free text per the confirmed spec, but the common
// names are offered as quick-pick suggestions.
export const SAMPLED_BY_SUGGESTIONS = ["Walid", "Mohamed", "Adel", "Ahmed Reda", "Shawky", "IPQA", "R&D"];
export const PRODUCTION_STAGES = ["B", "IP", "F.P", "S.F", "Coating", "Compressed Tab"];

// MediaType is a fixed set of 4 rows, one per MediaClass - it no longer
// has a Name/Code, so this is the friendly label used everywhere a
// media type is shown in a dropdown or table.
export const MEDIA_CLASS_LABELS: Record<string, string> = {
  GeneralAgar: "General Agar",
  GeneralBroth: "General Broth",
  SelectiveAgar: "Selective Agar",
  SelectiveBroth: "Selective Broth"
};
export const mediaClassLabel = (mediaClass?: string) => (mediaClass && MEDIA_CLASS_LABELS[mediaClass]) || mediaClass || "";

// Media Evaluation - the three named tests, one per MediaClass grouping.
export const EVALUATION_TYPE_LABELS: Record<string, string> = {
  GrowthPromotion: "Growth Promotion",
  IndicationInhibition: "Indication / Inhibition",
  EnrichmentCharacteristics: "Enrichment Characteristics"
};
export const evaluationTypeLabel = (t?: string) => (t && EVALUATION_TYPE_LABELS[t]) || t || "";
