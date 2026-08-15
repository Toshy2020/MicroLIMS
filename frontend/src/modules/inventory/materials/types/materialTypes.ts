export type MaterialType =
  | "DehydratedMedia"
  | "LyophilizedMicroorganism"
  | "Supplement"
  | "AntibioticDisc"
  | "IdentificationKit"
  | "IdentificationReagent"
  | "Chemical"
  | "Indicator"
  | "ReferenceBuffer"
  | "DisposableTool"
  | "Other";

export type MaterialUnit =
  | "Gram"
  | "Kilogram"
  | "Milliliter"
  | "Liter"
  | "Disc"
  | "Vial"
  | "Kit"
  | "Piece"
  | "Bottle"
  | "Pack";

export type StockStatus = "InStock" | "Depleted" | "Expired";

export interface OrganismSummary {
  id: number;
  scientificName: string;
  strainNumber?: string;
}

export interface MaterialItem {
  id: number;
  materialType: MaterialType;
  materialName: string;
  manufacturerName: string;
  batchNumber: string;
  receivingDate: string;
  expiryDate: string | null;
  code: string | null;
  location: string;
  organismId: number | null;
  organism?: OrganismSummary | null;
  atccNumber: string | null;
  quantityReceived: number;
  quantityRemaining: number;
  unit: MaterialUnit;
  minimumStockLevel: number | null;
  status: StockStatus;
}

export interface MaterialFormState {
  materialType: MaterialType;
  materialName: string;
  manufacturerName: string;
  batchNumber: string;
  receivingDate: string;
  expiryDate: string;
  code: string;
  location: string;
  quantityReceived: number | string;
  unit: MaterialUnit;
  minimumStockLevel: number | string;
  atccNumber: string;
  organismId: number | null;
}

export type MaterialKpiFilter = "all" | "in_stock" | "low_stock" | "out_of_stock" | "expiring_soon";

export interface MaterialFilterState {
  search: string;
  materialType: string;
  manufacturer: string;
  location: string;
  status: string;
  expiryRange: string;
}
