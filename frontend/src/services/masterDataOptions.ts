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
  getReleasedMedia: (mediaTypeId?: number) =>
    apiClient.get("/media/released", { params: mediaTypeId ? { mediaTypeId } : {} }).then((r) => r.data.data)
};

// "Sampled By" is free text per the confirmed spec, but the common
// names are offered as quick-pick suggestions.
export const SAMPLED_BY_SUGGESTIONS = ["Walid", "Mohamed", "Adel", "Ahmed Reda", "Shawky", "IPQA", "R&D"];
export const PRODUCTION_STAGES = ["B", "IP", "F.P", "S.F", "Coating", "Compressed Tab"];
