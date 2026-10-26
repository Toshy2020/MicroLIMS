export type EquipmentStatus = "InService" | "OutOfService" | "Retired";

export const EQUIPMENT_STATUS_LABELS: Record<EquipmentStatus, string> = {
  InService: "In Service",
  OutOfService: "Out of Service",
  Retired: "Retired"
};

export interface EquipmentItem {
  id: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
  instrumentType: string;
  manufacturerName: string;
  serialNumber: string | null;
  firmwareVersion: string | null;
  code: string;
  location: string;
  calibrationDueDate: string | null;
  status: EquipmentStatus;
  isCalibrationOverdue: boolean;
  // Owning laboratory - nullable only for legacy rows not yet assigned
  // (design.md §6: EquipmentInventory.SectionId back-fills before it
  // becomes required). Section navigation isn't eager-loaded by the API,
  // so the name is resolved client-side via useLaboratorySections.
  sectionId: number | null;
  calibrationIntervalValue?: number | null;
  calibrationIntervalUnit?: IntervalUnit | null;
  lastCalibratedOn?: string | null;
  pmIntervalValue?: number | null;
  pmIntervalUnit?: IntervalUnit | null;
  lastPmOn?: string | null;
}

export interface EquipmentStatusHistoryItem {
  id: number;
  equipmentInventoryId: number;
  previousStatus: EquipmentOperationalStatus;
  newStatus: EquipmentOperationalStatus;
  comment: string;
  // null = changed automatically by the system (changedByName is then "System (automatic)").
  changedByUserId: number | null;
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
  sectionId: number | "";
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

export type IntervalUnit = "Weeks" | "Months" | "Years";

export const INTERVAL_UNIT_LABELS: Record<IntervalUnit, string> = {
  Weeks: "Weeks",
  Months: "Months",
  Years: "Years"
};

export interface SaveEquipmentScheduleRequest {
  calibrationIntervalValue: number | null;
  calibrationIntervalUnit: IntervalUnit | null;
  lastCalibratedOn: string | null;
  pmIntervalValue: number | null;
  pmIntervalUnit: IntervalUnit | null;
  lastPmOn: string | null;
}

export interface EquipmentScheduleResponse {
  id: number;
  equipmentId: number;
  calibrationIntervalValue: number | null;
  calibrationIntervalUnit: IntervalUnit | null;
  lastCalibratedOn: string | null;
  pmIntervalValue: number | null;
  pmIntervalUnit: IntervalUnit | null;
  lastPmOn: string | null;
  version: number;
}

export type EquipmentTaskType = "Calibration" | "PreventiveMaintenance";

export const EQUIPMENT_TASK_TYPE_LABELS: Record<EquipmentTaskType, string> = {
  Calibration: "Calibration",
  PreventiveMaintenance: "Preventive Maintenance"
};

export type EquipmentTaskStatus = "Upcoming" | "Due" | "Overdue" | "Done" | "Retired";

export interface EquipmentTaskItem {
  id: number;
  equipmentId: number;
  equipmentCode: string;
  instrumentType: string;
  type: EquipmentTaskType;
  originalDueDate: string;
  dueDate: string;
  completedOn: string | null;
  status: EquipmentTaskStatus;
  evidenceLabel: string | null;
}

export interface EquipmentTaskResponse {
  id: number;
  equipmentInventoryId: number;
  taskType: EquipmentTaskType;
  year: number;
  sequence: number;
  isUnplanned: boolean;
  createdByRevisionId: number | null;
  retiredByRevisionId: number | null;
  originalDueDate: string;
  dueDate: string;
  completedOn: string | null;
  calibrationDocumentId: number | null;
  maintenanceLogEntryId: number | null;
  intervalSnapshot: string;
  version: number;
}

export type MaintenanceEntryType = "PreventiveMaintenance" | "BreakdownRepair" | "Other";

export const MAINTENANCE_ENTRY_TYPE_LABELS: Record<MaintenanceEntryType, string> = {
  PreventiveMaintenance: "Preventive Maintenance",
  BreakdownRepair: "Breakdown Repair",
  Other: "Other"
};

export interface MaintenanceLogEntryResponse {
  id: number;
  equipmentInventoryId: number;
  entryType: MaintenanceEntryType;
  performedOn: string;
  performedByUserId: number;
  performedByUserName: string | null;
  workDone: string;
  partsReplaced: string | null;
  findings: string | null;
  calendarEventId: number | null;
  recordedAt: string;
  voidedAt: string | null;
  voidedByUserId: number | null;
  voidReason: string | null;
  electronicSignatureId: number;
}

export interface AddLogEntryRequest {
  entryType: MaintenanceEntryType;
  performedOn: string;
  workDone: string;
  partsReplaced: string | null;
  findings: string | null;
  calendarEventId: number | null;
  password: string;
}

export interface VoidLogEntryRequest {
  reason: string;
  password: string;
}

export interface RecordCalibrationRequest {
  certificateDocumentId: number;
  calibratedOn: string;
  certificateNextDue: string | null;
  password: string;
}

export interface TaskEventLine {
  taskId: number;
  taskType: EquipmentTaskType;
  oldValue: string | null;
  newValue: string | null;
  reason: string | null;
  performedAt: string;
  performedByUserId: number | null;
  performedByName: string | null;
}

export interface StatusLine {
  id: number;
  previousStatus: EquipmentOperationalStatus;
  newStatus: EquipmentOperationalStatus;
  comment: string;
  changedAt: string;
  changedByUserId: number | null;
  changedByName: string | null;
}

export interface BaselineLine {
  date: string;
  label: string;
}

export interface InstrumentHistoryResponse {
  equipmentId: number;
  equipmentCode: string;
  instrumentType: string;
  manufacturerName: string;
  serialNumber: string | null;
  location: string;
  status: EquipmentOperationalStatus;
  calibrationIntervalDescription: string | null;
  pmIntervalDescription: string | null;
  tasks: EquipmentTaskItem[];
  reschedules: TaskEventLine[];
  statusChanges: StatusLine[];
  logbook: MaintenanceLogEntryResponse[];
  calibrationBaseline: BaselineLine | null;
  pmBaseline: BaselineLine | null;
  nextCalibrationDue: string | null;
  nextPmDue: string | null;
}
