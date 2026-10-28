import { apiClient } from "../../../../services/apiClient";
import type { ApiResponse } from "./EquipmentConfigurationService";

export type TestingMaterialGroup = "ReagentsIndicators" | "Standards" | "CultureMedia" | "ReferenceOrganisms" | "OtherMaterials";

export type TestingMaterialSourceType = "MaterialMasterEntry" | "MediaProduct" | "ReferenceOrganism" | "WorkingStandard";

// One row of the Master Testing Materials catalog (GET masterdata/testing-materials).
export interface TestingMaterialCatalogItem {
  group: TestingMaterialGroup;
  // Working standards: the stock lot id; every other source: the master record id.
  id: number;
  sourceType: TestingMaterialSourceType;
  code: string;
  name: string;
  subCategory?: string | null;
  // Null for media products, which no lab owns.
  ownerSectionCode?: string | null;
  // Working standards: the lot is in stock.
  isActive: boolean;
  hasStock: boolean;
  canEdit: boolean;
  // Managed in its own module (working standards), not on the master.
  readOnly: boolean;
}

export interface TestingMaterialCatalogQuery {
  group?: TestingMaterialGroup;
  search?: string;
  activeOnly?: boolean;
  // Narrows Other materials to the categories offered to this lab.
  sectionId?: number;
}

export const TestingMaterialCatalogService = {
  getAll: async (query: TestingMaterialCatalogQuery = {}): Promise<TestingMaterialCatalogItem[]> => {
    const res = await apiClient.get<ApiResponse<TestingMaterialCatalogItem[]>>("/masterdata/testing-materials", { params: query });
    const data = res.data?.data;
    return Array.isArray(data) ? data : [];
  }
};
