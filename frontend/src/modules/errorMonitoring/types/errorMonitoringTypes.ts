// Enums are serialized as strings by the API's JsonStringEnumConverter.
export type ErrorSeverity = "Info" | "Warning" | "Error" | "Critical";
export type ErrorSource = "Backend" | "Frontend" | "Database";
export type IncidentStatus = "Open" | "Resolved";

export const SEVERITIES: ErrorSeverity[] = ["Info", "Warning", "Error", "Critical"];
export const SOURCES: ErrorSource[] = ["Backend", "Frontend", "Database"];
export const STATUSES: IncidentStatus[] = ["Open", "Resolved"];

export const SEVERITY_COLORS: Record<ErrorSeverity, "info" | "warning" | "error" | "default"> = {
  Info: "info",
  Warning: "warning",
  Error: "error",
  Critical: "error"
};

export interface IncidentListItem {
  id: string;
  correlationId: string;
  severity: ErrorSeverity;
  status: IncidentStatus;
  summary: string;
  firstSeenUtc: string;
  lastSeenUtc: string;
  occurrenceCount: number;
  sources: ErrorSource[];
  resolvedAtUtc: string | null;
  resolvedByUserName: string | null;
  resolutionNotes: string | null;
}

export interface IncidentListResult {
  items: IncidentListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ErrorLogEntry {
  id: string;
  source: ErrorSource;
  severity: ErrorSeverity;
  severityOverride: ErrorSeverity | null;
  effectiveSeverity: ErrorSeverity;
  exceptionType: string;
  message: string;
  stackTrace: string | null;
  requestPath: string | null;
  httpMethod: string | null;
  statusCode: number | null;
  userId: number | null;
  userName: string | null;
  occurredAtUtc: string;
  rawContext: string | null;
}

export interface IncidentDetail {
  id: string;
  correlationId: string;
  severity: ErrorSeverity;
  status: IncidentStatus;
  summary: string;
  firstSeenUtc: string;
  lastSeenUtc: string;
  resolvedAtUtc: string | null;
  resolvedByUserName: string | null;
  resolutionNotes: string | null;
  errorLogs: ErrorLogEntry[];
}

export interface IncidentFilterState {
  severity: ErrorSeverity | "";
  source: ErrorSource | "";
  status: IncidentStatus | "";
  fromDate: string;
  toDate: string;
  search: string;
}

export const INITIAL_INCIDENT_FILTERS: IncidentFilterState = {
  severity: "",
  source: "",
  status: "",
  fromDate: "",
  toDate: "",
  search: ""
};
