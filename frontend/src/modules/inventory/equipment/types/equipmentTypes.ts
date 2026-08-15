export type EquipmentStatus = "InService" | "OutOfService" | "Retired";

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

export interface EquipmentFormState {
  instrumentType: string;
  manufacturerName: string;
  serialNumber: string;
  firmwareVersion: string;
  code: string;
  location: string;
  calibrationDueDate: string;
  status: EquipmentStatus;
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
