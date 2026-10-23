import type { CalendarEventResponse, CalendarEventType } from "./types";

export interface CalendarEventMatchCandidate {
  id?: number;
  type: CalendarEventType;
  title: string;
  date?: string | null;
  month?: string | null;
  equipmentInventoryId?: number | null;
}

export function isDuplicateCalendarEvent(
  event: CalendarEventResponse,
  candidate: CalendarEventMatchCandidate
): boolean {
  if (candidate.id != null && event.id === candidate.id) {
    return false;
  }
  if (event.status === "Cancelled") {
    return false;
  }
  if (event.type !== candidate.type) {
    return false;
  }
  if (event.title.trim().toLowerCase() !== candidate.title.trim().toLowerCase()) {
    return false;
  }
  const eventEq = event.equipmentInventoryId ?? null;
  const candEq = candidate.equipmentInventoryId ?? null;
  if (eventEq !== candEq) {
    return false;
  }
  if (candidate.date) {
    return event.date === candidate.date;
  }
  if (candidate.month) {
    return event.month === candidate.month;
  }
  return false;
}
