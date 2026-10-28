// The API refuses every write with 503 and an X-Maintenance-Mode header while
// maintenance mode is on. apiClient raises this event when it sees one so the
// status provider can show the server's message and refresh the banner, without
// apiClient depending on React.
export const MAINTENANCE_EVENT = "microlims:maintenance-refusal";
export const MAINTENANCE_HEADER = "x-maintenance-mode";

export interface MaintenanceRefusalDetail {
  message: string;
}

export function notifyMaintenanceRefusal(message: string): void {
  window.dispatchEvent(new CustomEvent<MaintenanceRefusalDetail>(MAINTENANCE_EVENT, { detail: { message } }));
}
