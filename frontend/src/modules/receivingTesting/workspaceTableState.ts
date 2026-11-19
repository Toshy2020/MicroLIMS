import type { TableState } from "../../components/lab/table/tableUrlCodec";
import type { AllWorkloadFilterKey } from "../receiving/components/SampleStatusKpiCards";

// Workload filters a URL may set - the tile keys plus the deep-link-only ones
// dashboards link to (no tile of their own).
export const WHITELISTED_WORKLOADS: readonly AllWorkloadFilterKey[] = [
  "needsPreparation",
  "readyToRead",
  "awaitingReview",
  "overdue",
  "mine",
  "unassigned",
  "retestInProgress",
  "reviewOverdue",
  "approvalOverdue"
];

export const WORKSPACE_SERVER_SORTABLE_COLUMNS: ReadonlySet<string> = new Set([
  "item",
  "receivedAt",
  "sampleId",
  "category",
  "sampledBy",
  "batchControl",
  "sampleStatus",
  "due"
]);

export function isWhitelistedWorkload(key: string | null | undefined): key is AllWorkloadFilterKey {
  return key !== null && key !== undefined && (WHITELISTED_WORKLOADS as readonly string[]).includes(key);
}

// Parameters the workspace used before its table state moved into the URL
// codec. Dashboards, notifications and bookmarks still link with them, so they
// are read once and rewritten as table state; they are then removed from the
// address so they cannot re-apply over a later change.
export const LEGACY_FILTER_PARAMS = [
  "status",
  "scope",
  "workload",
  "search",
  "category",
  "sampleStatus",
  "testStatus",
  "analystId",
  "urgency",
  "fromDate",
  "toDate",
  "pageSize"
] as const;

const DISPLAY_MODES = ["table", "card", "kanban"];

// The page size this table can hold: 25, 50 or 100 (a server page never
// exceeds 100). An old ?pageSize= between those is rounded down.
function legacyPageSize(raw: string): TableState["pageSize"] | null {
  const n = parseInt(raw, 10);
  if (Number.isNaN(n)) return null;
  if (n >= 100) return 100;
  if (n >= 50) return 50;
  return 25;
}

// The table state the old parameters ask for (only what they set), or null
// when the address carries none of them.
export function readLegacyWorkspaceLink(params: URLSearchParams): Partial<TableState> | null {
  const has = LEGACY_FILTER_PARAMS.some((key) => params.has(key));
  const displayMode = params.get("view");
  const legacyDisplay = displayMode !== null && DISPLAY_MODES.includes(displayMode);
  if (!has && !legacyDisplay) return null;

  const filters: Record<string, string | string[]> = {};
  const set = (name: string, value: string | null) => {
    if (value) filters[name] = value;
  };

  const status = params.get("status");
  const category = params.get("category");
  if (category && category !== "ALL") set("category", category);

  // ?status=Active means "no sample status filter"; any other status is one.
  if (status && status !== "Active") set("sampleStatus", status);
  else if (!status) {
    const sampleStatus = params.get("sampleStatus");
    if (sampleStatus && sampleStatus !== "ALL") set("sampleStatus", sampleStatus);
  }

  const testStatus = params.get("testStatus");
  if (testStatus && testStatus !== "ALL") set("testStatus", testStatus);
  set("analystId", params.get("analystId"));
  set("urgency", params.get("urgency"));
  set("fromDate", params.get("fromDate"));
  set("toDate", params.get("toDate"));

  // An explicit scope or workload applies even next to ?status=Active.
  const workload = params.get("workload");
  if (params.get("scope") === "mine") set("workload", "mine");
  else if (isWhitelistedWorkload(workload)) set("workload", workload);

  const state: Partial<TableState> = { filters };
  const search = params.get("search");
  if (search) state.search = search;
  const size = params.get("pageSize");
  const pageSize = size ? legacyPageSize(size) : null;
  if (pageSize !== null) state.pageSize = pageSize;
  return state;
}

// The ?view= display mode (table, card or kanban) of an old link; a numeric
// ?view= is a saved view id and belongs to the table state.
export function legacyDisplayMode(params: URLSearchParams): "table" | "card" | "kanban" | null {
  const v = params.get("view");
  return v === "table" || v === "card" || v === "kanban" ? v : null;
}
