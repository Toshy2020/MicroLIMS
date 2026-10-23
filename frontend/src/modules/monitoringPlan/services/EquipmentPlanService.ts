import { apiClient } from "../../../services/apiClient";
import type {
  CalendarEventResponse,
  CreateCalendarEventRequest,
  EquipmentCalendarResponse,
  EquipmentMonthSummary,
  EquipmentPlanPreview,
  EquipmentPlanRevision,
  UpdateCalendarEventRequest
} from "../types";

// Row versions travel in the If-Match header like the other editable records.
const ifMatch = (version: number) => ({ headers: { "If-Match": String(version) } });

export const EquipmentPlanService = {
  getRevisions: (year: number) =>
    apiClient.get("/equipment-plan/revisions", { params: { year } }).then((r) => r.data.data as EquipmentPlanRevision[]),

  // goLiveDate: when the plan starts. Omitted means today (or 1 January of a future year).
  generateDraft: (year: number, goLiveDate?: string) =>
    apiClient.post("/equipment-plan/drafts", { year, goLiveDate: goLiveDate || null }).then((r) => r.data.data as EquipmentPlanPreview),

  getPreview: (id: number) =>
    apiClient.get(`/equipment-plan/revisions/${id}/preview`).then((r) => r.data.data as EquipmentPlanPreview),

  approve: (id: number, password: string, reason?: string) =>
    apiClient.post(`/equipment-plan/revisions/${id}/approve`, { password, reason: reason || null }).then((r) => r.data.data as EquipmentPlanRevision),

  getCalendar: (from: string, to: string) =>
    apiClient.get("/equipment-plan/calendar", { params: { from, to } }).then((r) => r.data.data as EquipmentCalendarResponse),

  getYear: (year: number) =>
    apiClient.get("/equipment-plan/year", { params: { year } }).then((r) => r.data.data as EquipmentMonthSummary[]),

  getEvents: (from: string, to: string) =>
    apiClient.get("/calendar-events", { params: { from, to } }).then((r) => r.data.data as CalendarEventResponse[]),

  createEvent: (body: CreateCalendarEventRequest) =>
    apiClient.post("/calendar-events", body).then((r) => r.data.data as CalendarEventResponse),

  updateEvent: (id: number, version: number, body: UpdateCalendarEventRequest) =>
    apiClient.put(`/calendar-events/${id}`, body, ifMatch(version)).then((r) => r.data.data as CalendarEventResponse),

  cancelEvent: (id: number, reason: string) =>
    apiClient.post(`/calendar-events/${id}/cancel`, { reason }).then((r) => r.data.data as CalendarEventResponse),

  uploadEventDocument: (id: number, file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return apiClient.post(`/calendar-events/${id}/documents`, formData).then((r) => r.data.data as CalendarEventResponse);
  },

  getDocumentContentUrl: (docId: number) => `/api/calendar-event-documents/${docId}/content`
};
