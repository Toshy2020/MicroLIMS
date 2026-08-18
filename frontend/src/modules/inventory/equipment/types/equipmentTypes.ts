export type EquipmentStatus = "InService" | "OutOfService" | "Retired";

export const EQUIPMENT_STATUS_LABELS: Record<EquipmentStatus, string> = {
  InService: "In Service",
  OutOfService: "Out of Service",
  Retired: "Retired"
};

export interface EquipmentItem {
  id: number;
  instrumentType: string;
  manufacturerName: string;
  serialNumber: string | null;
  firmwareVersion: string | null;
  code: string;
  location: string;
  calibrationDueDate: string | null;
  status: EquipmentStatus;
  isCalibrationOverdue: boolean;
}

export interface EquipmentStatusHistoryItem {
  id: number;
  equipmentInventoryId: number;
  previousStatus: EquipmentOperationalStatus;
  newStatus: EquipmentOperationalStatus;
  comment: string;
  changedByUserId: number;
  changedByName: string;
  changedAt: string;
}

export type EquipmentOperationalStatus = "InService" | "OutOfService" | "Retired";

export type EquipmentDocumentType = "CalibrationCertificate";

export const EQUIPMENT_DOCUMENT_TYPE_LABELS: Record<EquipmentDocumentType, string> = {
  CalibrationCertificate: "Calibration Certificate"
};

export type EquipmentDocumentStatus = "Current" | "Superseded" | "Voided";

export interface EquipmentDocument {
  id: number;
  equipmentInventoryId: number;
  documentType: EquipmentDocumentType;
  originalFileName: string;
  fileExtension: string;
  contentType: string;
  fileSizeBytes: number;
  contentSha256: string;
  uploadedByUserId: number;
  uploadedByName: string;
  uploadedAt: string;
  status: EquipmentDocumentStatus;
  supersededByDocumentId: number | null;
  supersededAt: string | null;
  supersededByUserId: number | null;
  supersessionReason: string | null;
  voidedAt: string | null;
  voidedByUserId: number | null;
  voidReason: string | null;
}

export interface EquipmentFormState {
  instrumentType: string;
  manufacturerName: string;
  serialNumber: string;
  firmwareVersion: string;
  code: string;
  location: string;
  calibrationDueDate: string;
  status: EquipmentStatus;
  statusChangeComment?: string;
}

export type EquipmentKpiFilter =
  | "all"
  | "in_service"
  | "out_of_service"
  | "calibration_overdue"
  | "calibration_due_soon";

export interface EquipmentFilterState {
  search: string;
  instrumentType: string;
  status: string;
  location: string;
  calibrationRange: string;
}
