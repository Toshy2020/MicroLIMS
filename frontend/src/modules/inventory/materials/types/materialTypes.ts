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
  | "Other"
  | "ReferenceStandard"
  | "WorkingStandard"
  | "PrimaryStandard";

export const MATERIAL_TYPE_LABELS: Record<MaterialType, string> = {
  DehydratedMedia: "Dehydrated Media",
  LyophilizedMicroorganism: "Lyophilized Microorganism",
  Supplement: "Supplement",
  AntibioticDisc: "Antibiotic Disc",
  IdentificationKit: "Identification Kit",
  IdentificationReagent: "Identification Reagent",
  Chemical: "Chemical",
  Indicator: "Indicator",
  ReferenceBuffer: "Reference Buffer",
  DisposableTool: "Disposable Tool",
  Other: "Other",
  ReferenceStandard: "Reference Standard",
  WorkingStandard: "Working Standard",
  PrimaryStandard: "Primary Standard"
};

// Types that became categories of Other materials. New lots are never received under
// them, but lots received before keep the stored type.
export const LEGACY_MATERIAL_TYPES: MaterialType[] = [
  "Supplement",
  "AntibioticDisc",
  "IdentificationKit",
  "IdentificationReagent",
  "ReferenceBuffer",
  "DisposableTool"
];

export type MaterialApprovalStatus = "PendingReview" | "Approved" | "Rejected";

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

export const MATERIAL_UNITS: MaterialUnit[] = [
  "Gram",
  "Kilogram",
  "Milliliter",
  "Liter",
  "Disc",
  "Vial",
  "Kit",
  "Piece",
  "Bottle",
  "Pack"
];

export type StockStatus = "InStock" | "Depleted" | "Expired";

export interface OrganismSummary {
  id: number;
  scientificName: string;
  atccNumber?: string | null;
  strainNumber?: string;
  description?: string | null;
}

export interface MaterialItem {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  // Owning laboratory - Material.SectionId is required server-side
  // (unlike EquipmentInventory's still-nullable column), used here only to
  // honour the ?lab= query param from the menu links (design.md §6).
  sectionId: number;
  materialType: MaterialType;
  customType: string | null;
  materialName: string;
  manufacturerName: string;
  batchNumber: string;
  receivingDate: string;
  expiryDate: string | null;
  code: string | null;
  location: string;
  mediaProductId: number | null;
  mediaProduct?: { id: number; name: string; code: string } | null;
  organismId: number | null;
  organism?: OrganismSummary | null;
  atccNumber: string | null;
  quantityReceived: number;
  quantityRemaining: number;
  unit: MaterialUnit;
  minimumStockLevel: number | null;
  purity?: number | null;
  status: StockStatus;
  materialMasterEntryId?: number | null;
  materialMasterEntryCode?: string | null;
  moisturePercent?: number | null;
  storageCondition?: string | null;
  // Release gate: null when the lot's category needs no approval before use.
  approvalStatus?: MaterialApprovalStatus | null;
  approvedByUserId?: number | null;
  approvedAt?: string | null;
  materialCategoryId?: number | null;
  categoryName?: string | null;
  labelRequired?: boolean;
  isUsable?: boolean;
}

export interface MaterialFormState {
  materialType: MaterialType;
  customType: string;
  materialName: string;
  manufacturerName: string;
  batchNumber: string;
  receivingDate: string;
  expiryDate: string;
  code: string;
  location: string;
  storageCondition: string;
  quantityReceived: number | string;
  unit: MaterialUnit;
  minimumStockLevel: number | string;
  purity: number | string;
  moisturePercent: number | string;
  atccNumber: string;
  organismId: number | null;
  mediaProductId: number | null;
  materialMasterEntryId: number | null;
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
