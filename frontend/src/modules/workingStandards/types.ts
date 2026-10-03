export type WorkingStandardQualificationKind = "Initial" | "Requalification";
export type WorkingStandardQualificationStatus = "Draft" | "Assayed" | "Reviewed" | "Approved" | "Rejected";
export type WorkingStandardDocumentKind = "SourceReport" | "MoistureReport";
export type WorkingStandardLotStatus = "Valid" | "DueSoon" | "Expired" | "Depleted";

export interface CreateQualificationRequest {
  kind: WorkingStandardQualificationKind;
  workingStandardMaterialId?: number | null;
  materialMasterEntryId?: number | null;
  sourceSampleId?: number | null;
  sourceMaterialName?: string | null;
  sourceBatchNumber?: string | null;
  quantityGrams?: number | null;
  location?: string | null;
  moisturePercent?: number | null;
}

export interface UpdateQualificationRequest {
  quantityGrams?: number | null;
  location?: string | null;
  moisturePercent?: number | null;
}

export interface WorkingStandardSignRequest {
  password: string;
  comment?: string | null;
}

export interface WorkingStandardReasonRequest {
  password: string;
  reason: string;
}

export interface WorkingStandardReturnRequest {
  reason: string;
}

export interface EligibleSourceSampleDto {
  sampleId: number;
  referenceNumber: string;
  materialName: string;
  batchNumber?: string | null;
  testCode: string;
}

export interface WorkingStandardDocumentDto {
  id: number;
  kind: WorkingStandardDocumentKind;
  fileName: string;
  contentType: string;
  uploadedAt: string;
  uploadedByUserName?: string | null;
  isCurrent: boolean;
}

export interface WorkingStandardRunLinkDto {
  hplcRunId: number;
  equipmentId: number;
  runCode: string;
  runSampleId: number;
  status: string;
}

export interface WorkingStandardQualificationDto {
  id: number;
  version: number;
  code: string;
  kind: WorkingStandardQualificationKind;
  status: WorkingStandardQualificationStatus;
  sectionId: number;
  workingStandardMaterialId?: number | null;
  workingStandardCode?: string | null;
  materialMasterEntryId: number;
  materialMasterCode: string;
  materialMasterName: string;
  sourceSampleId?: number | null;
  sourceSampleReference?: string | null;
  sourceMaterialName: string;
  sourceBatchNumber: string;
  quantityGrams?: number | null;
  location?: string | null;
  moisturePercent?: number | null;
  replicateAssayPercents?: number[] | null;
  meanAssayPercent?: number | null;
  rsdPercent?: number | null;
  potencyPercent?: number | null;
  passed: boolean;
  failureReasons?: string | null;
  createdByUserName?: string | null;
  createdAt: string;
  preparedByUserName?: string | null;
  preparedAt?: string | null;
  reviewedByUserName?: string | null;
  reviewedAt?: string | null;
  approvedByUserName?: string | null;
  approvedAt?: string | null;
  rejectedByUserName?: string | null;
  rejectedAt?: string | null;
  rejectReason?: string | null;
  returnReason?: string | null;
  run?: WorkingStandardRunLinkDto | null;
  documents: WorkingStandardDocumentDto[];
  runEvidenceIds: number[];
}

export interface WorkingStandardLotDto {
  materialId: number;
  code: string;
  materialName: string;
  batchNumber: string;
  masterEntryCode: string;
  potencyPercent?: number | null;
  moisturePercent?: number | null;
  quantityRemaining: number;
  expiryDate?: string | null;
  status: WorkingStandardLotStatus | string;
  openQualificationId?: number | null;
}
