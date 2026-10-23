import { apiClient, ifMatch } from "../../../../services/apiClient";
import type {
  SaveEquipmentScheduleRequest,
  EquipmentScheduleResponse,
  RecordCalibrationRequest,
  EquipmentTaskResponse,
  MaintenanceLogEntryResponse,
  AddLogEntryRequest,
  VoidLogEntryRequest,
  InstrumentHistoryResponse
} from "../types/equipmentTypes";

export const EquipmentPlanService = {
  saveSchedule: (
    equipmentId: number,
    payload: SaveEquipmentScheduleRequest,
    version?: number
  ): Promise<EquipmentScheduleResponse> =>
    apiClient
      .put(`/equipment-plan/instruments/${equipmentId}/schedule`, payload, ifMatch(version))
      .then((r) => r.data.data),

  recordCalibration: (
    equipmentId: number,
    payload: RecordCalibrationRequest
  ): Promise<EquipmentTaskResponse> =>
    apiClient
      .post(`/equipment-plan/instruments/${equipmentId}/calibrations`, payload)
      .then((r) => r.data.data),

  getMaintenanceLog: (equipmentId: number): Promise<MaintenanceLogEntryResponse[]> =>
    apiClient
      .get(`/equipment/${equipmentId}/maintenance-log`)
      .then((r) => r.data.data),

  addLogEntry: (
    equipmentId: number,
    payload: AddLogEntryRequest
  ): Promise<MaintenanceLogEntryResponse> =>
    apiClient
      .post(`/equipment/${equipmentId}/maintenance-log`, payload)
      .then((r) => r.data.data),

  voidLogEntry: (
    entryId: number,
    payload: VoidLogEntryRequest
  ): Promise<{ message: string }> =>
    apiClient
      .post(`/maintenance-log/${entryId}/void`, payload)
      .then((r) => r.data.data),

  getInstrumentHistory: (
    equipmentId: number,
    year: number
  ): Promise<InstrumentHistoryResponse> =>
    apiClient
      .get(`/equipment-plan/instruments/${equipmentId}/history`, {
        params: { year }
      })
      .then((r) => r.data.data)
};
