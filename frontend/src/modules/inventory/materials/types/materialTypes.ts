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
  atccNumber?: string | null;
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

// ---- Material Document types ----

export type MaterialDocumentType =
  | "COA"
  | "SupplierCertificate"
  | "Specification"
  | "SDS"
  | "Other";

export type MaterialDocumentStatus = "Current" | "Superseded" | "Voided";

export const MATERIAL_DOCUMENT_TYPE_LABELS: Record<MaterialDocumentType, string> = {
  COA: "Certificate of Analysis (COA)",
  SupplierCertificate: "Supplier Certificate",
  Specification: "Specification",
  SDS: "Safety Data Sheet (SDS)",
  Other: "Other"
};

export const COA_REQUIRED_TYPES: MaterialType[] = [
  "DehydratedMedia",
  "LyophilizedMicroorganism",
  "Supplement"
];

export interface MaterialDocument {
  id: number;
  materialId: number;
  documentType: MaterialDocumentType;
  originalFileName: string;
  fileExtension: string;
  contentType: string;
  fileSizeBytes: number;
  contentSha256: string;
  uploadedByUserId: number;
  uploadedByName: string;
  uploadedAt: string;
  status: MaterialDocumentStatus;
  supersededByDocumentId: number | null;
  supersededAt: string | null;
  supersededByUserId: number | null;
  supersessionReason: string | null;
  voidedAt: string | null;
  voidedByUserId: number | null;
  voidReason: string | null;
}

export interface CoeEligibilityResult {
  isEligible: boolean;
  coaRequired: boolean;
  hasCurrentCoa: boolean;
}
