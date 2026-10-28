import { apiClient, ifMatch } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";

export type CultureCollection = "ATCC" | "NCTC" | "NCIMB" | "WDCM" | "DSMZ" | "Other";

export const CULTURE_COLLECTIONS: CultureCollection[] = ["ATCC", "NCTC", "NCIMB", "WDCM", "DSMZ", "Other"];

export const REFERENCE_ORGANISM_CODE_PREFIX = "ORG-";

export interface ReferenceOrganism {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  code: string;
  organismId: number;
  organismScientificName?: string | null;
  cultureCollection: CultureCollection;
  strainNumber?: string | null;
  // "{species} {collection} {strain number}", computed by the server.
  name: string;
  // Owner lab: the only lab that can edit the strain; every lab can see and use it.
  ownerSectionId: number;
  ownerSectionCode?: string | null;
  ownerSectionName?: string | null;
  isActive: boolean;
  // Decided by the server from the user's configurable sections.
  canEdit: boolean;
  // A stock lot or cryovial references it: identity and code are frozen.
  hasStock: boolean;
  createdAt?: string;
  lastModifiedAt?: string;
}

export interface SaveReferenceOrganismRequest {
  code: string;
  organismId: number;
  cultureCollection: CultureCollection;
  strainNumber?: string | null;
  ownerSectionId?: number | null;
}

export const ReferenceOrganismService = {
  getAll: async (activeOnly?: boolean, organismId?: number): Promise<ReferenceOrganism[]> => {
    const params: Record<string, boolean | number> = {};
    if (activeOnly !== undefined) params.activeOnly = activeOnly;
    if (organismId !== undefined) params.organismId = organismId;
    const res = await apiClient.get<ApiResponse<ReferenceOrganism[]>>("/masterdata/reference-organisms", { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  create: async (req: SaveReferenceOrganismRequest): Promise<ReferenceOrganism> => {
    const res = await apiClient.post<ApiResponse<ReferenceOrganism>>("/masterdata/reference-organisms", req);
    return res.data?.data;
  },

  update: async (id: number, req: SaveReferenceOrganismRequest, version?: number): Promise<ReferenceOrganism> => {
    const res = await apiClient.put<ApiResponse<ReferenceOrganism>>(`/masterdata/reference-organisms/${id}`, req, ifMatch(version));
    return res.data?.data;
  },

  changeCode: async (id: number, code: string, reason: string, password: string): Promise<ReferenceOrganism> => {
    const res = await apiClient.put<ApiResponse<ReferenceOrganism>>(`/masterdata/reference-organisms/${id}/code`, { code, reason, password });
    return res.data?.data;
  },

  setActive: async (id: number, value: boolean): Promise<ReferenceOrganism> => {
    const res = await apiClient.put<ApiResponse<ReferenceOrganism>>(`/masterdata/reference-organisms/${id}/active`, null, {
      params: { value }
    });
    return res.data?.data;
  }
};
