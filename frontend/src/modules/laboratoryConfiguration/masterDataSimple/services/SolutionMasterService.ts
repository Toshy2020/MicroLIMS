import { apiClient, ifMatch } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";

export type SolutionType = "MobilePhase" | "Diluent" | "Titrant" | "Reagent" | "Neutralizer";
export type SolutionComponentKind = "Material" | "PreparedSolution" | "Text";

// Diluent, Reagent and Neutralizer: the types that can carry the sterilization / release
// flags and prepared-solution components. Mirrors the backend grouping (display only; the
// backend enforces it).
export const GENERAL_SOLUTION_TYPES: SolutionType[] = ["Diluent", "Reagent", "Neutralizer"];
export const isGeneralSolutionType = (t: SolutionType): boolean => GENERAL_SOLUTION_TYPES.includes(t);
export type ShelfLifeUnit = "Hours" | "Days";
export type SolutionComponentUnit = "Gram" | "Milligram" | "Milliliter" | "Liter" | "PercentVolume" | "Parts";
export type TitrantStrengthUnit = "Normal" | "Molar" | "MgWaterPerMl";
export type StandardizationMode = "PrimaryStandard" | "AgainstVolumetricSolution";

export interface SolutionComponentResponse {
  id?: number;
  order: number;
  kind?: SolutionComponentKind;
  materialMasterEntryId?: number | null;
  entryCode: string;
  entryName: string;
  quantity?: number | null;
  unit?: SolutionComponentUnit | null;
  sourceSolutionMasterId?: number | null;
  sourceSolutionName?: string | null;
  text?: string | null;
}

export interface SolutionComponentInput {
  kind: SolutionComponentKind;
  materialMasterEntryId?: number | null;
  sourceSolutionMasterId?: number | null;
  text?: string | null;
  quantity?: number | null;
  unit?: SolutionComponentUnit | null;
}

export interface SolutionMaster {
  id: number;
  version?: number;
  sectionId: number;
  sectionName?: string;
  name: string;
  type: SolutionType;
  shelfLifeValue: number;
  shelfLifeUnit: ShelfLifeUnit;
  storageCondition: string;
  finalVolumeMl: number;
  phTarget?: number | null;
  phTolerance?: number | null;
  phAdjustingEntryId?: number | null;
  phAdjustingEntryCode?: string | null;
  instructions: string;
  isActive: boolean;
  sterilizationRequired?: boolean;
  releaseRequired?: boolean;
  components: SolutionComponentResponse[];

  // Titrant only
  nominalStrength?: number | null;
  strengthUnit?: TitrantStrengthUnit | null;
  standardizationMode?: StandardizationMode | null;
  standardEntryId?: number | null;
  standardEntryCode?: string | null;
  equivalenceMgPerMl?: number | null;
  referenceSolutionId?: number | null;
  referenceSolutionName?: string | null;
  blankRequired: boolean;
  replicateCount?: number | null;
  factorMin?: number | null;
  factorMax?: number | null;
  maxRsdPercent?: number | null;
  validityDays?: number | null;

  createdAt?: string;
  lastModifiedAt?: string;
}

export interface SaveSolutionMasterRequest {
  name: string;
  type: SolutionType;
  shelfLifeValue: number;
  shelfLifeUnit: ShelfLifeUnit;
  storageCondition: string;
  finalVolumeMl: number;
  instructions: string;
  components: SolutionComponentInput[];
  phTarget?: number | null;
  phTolerance?: number | null;
  phAdjustingEntryId?: number | null;
  sectionId?: number | null;
  sterilizationRequired?: boolean;
  releaseRequired?: boolean;

  // Titrant fields (only sent when type is Titrant; otherwise null/false)
  nominalStrength?: number | null;
  strengthUnit?: TitrantStrengthUnit | null;
  standardizationMode?: StandardizationMode | null;
  standardEntryId?: number | null;
  equivalenceMgPerMl?: number | null;
  referenceSolutionId?: number | null;
  blankRequired?: boolean;
  replicateCount?: number | null;
  factorMin?: number | null;
  factorMax?: number | null;
  maxRsdPercent?: number | null;
  validityDays?: number | null;

  // Audit reason (required on update)
  reason?: string | null;
}

export const SolutionMasterService = {
  getAll: async (type?: SolutionType, activeOnly?: boolean): Promise<SolutionMaster[]> => {
    const params: Record<string, boolean | string> = {};
    if (type) params.type = type;
    if (activeOnly !== undefined) params.activeOnly = activeOnly;
    const res = await apiClient.get<ApiResponse<SolutionMaster[]>>("/masterdata/solution-masters", { params });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  },

  getById: async (id: number): Promise<SolutionMaster> => {
    const res = await apiClient.get<ApiResponse<SolutionMaster>>(`/masterdata/solution-masters/${id}`);
    return res.data?.data;
  },

  create: async (req: SaveSolutionMasterRequest): Promise<SolutionMaster> => {
    const res = await apiClient.post<ApiResponse<SolutionMaster>>("/masterdata/solution-masters", req);
    return res.data?.data;
  },

  update: async (id: number, req: SaveSolutionMasterRequest, version?: number): Promise<SolutionMaster> => {
    const res = await apiClient.put<ApiResponse<SolutionMaster>>(
      `/masterdata/solution-masters/${id}`,
      req,
      ifMatch(version)
    );
    return res.data?.data;
  },

  setActive: async (id: number, value: boolean, reason: string, version?: number): Promise<SolutionMaster> => {
    const res = await apiClient.put<ApiResponse<SolutionMaster>>(
      `/masterdata/solution-masters/${id}/active`,
      { reason },
      {
        params: { value },
        ...ifMatch(version)
      }
    );
    return res.data?.data;
  }
};
