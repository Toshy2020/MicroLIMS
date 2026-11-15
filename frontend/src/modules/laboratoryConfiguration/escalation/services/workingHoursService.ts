import { apiClient } from "../../../../services/apiClient";

interface ApiEnvelope<T> {
  data: T;
}

export type DayOfWeekName =
  | "Sunday"
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday";

export interface WorkingHoursDayDto {
  dayOfWeek: DayOfWeekName;
  isWorkingDay: boolean;
  startMinute: number;
  endMinute: number;
}

export interface WorkingHoursDto {
  timeZoneId: string;
  timeZoneDisplayName: string;
  isConfigured: boolean;
  days: WorkingHoursDayDto[];
}

export interface UpdateWorkingHoursDayRequest {
  dayOfWeek: DayOfWeekName;
  isWorkingDay: boolean;
  startMinute: number;
  endMinute: number;
}

export const workingHoursService = {
  async get(signal?: AbortSignal): Promise<WorkingHoursDto> {
    const res = await apiClient.get<ApiEnvelope<WorkingHoursDto>>("/working-hours", { signal });
    return res.data.data;
  },

  async update(days: UpdateWorkingHoursDayRequest[]): Promise<WorkingHoursDto> {
    const res = await apiClient.put<ApiEnvelope<WorkingHoursDto>>("/working-hours", { days });
    return res.data.data;
  }
};
