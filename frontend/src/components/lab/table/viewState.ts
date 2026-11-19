import type { TableDefinition } from "./tableRegistry";
import type { TableState } from "./tableUrlCodec";

export interface SerializedTableState {
  v: 1;
  filters: Record<string, string | string[]>;
  search: string;
  sort: { key: string; direction: "asc" | "desc" } | null;
  pageSize: 25 | 50 | 100 | "all";
  columns: string[];
  density: "comfortable" | "compact";
}

const VALID_PAGE_SIZES = new Set<unknown>([25, 50, 100, "all"]);

export function serializeTableState(state: TableState): string {
  const payload: SerializedTableState = {
    v: 1,
    filters: state.filters,
    search: state.search,
    sort: state.sort,
    pageSize: state.pageSize,
    columns: state.columns,
    density: state.density,
  };
  return JSON.stringify(payload);
}

export function sanitizeColumnsForDef(
  def: TableDefinition,
  raw: string[]
): { columns: string[]; skipped: string[] } {
  const known = new Set(def.columns.map((c) => c.key));
  const seen = new Set<string>();
  const valid: string[] = [];
  const skipped: string[] = [];

  for (const key of raw) {
    if (!known.has(key)) {
      skipped.push(`column:${key}`);
      continue;
    }
    if (!seen.has(key)) {
      seen.add(key);
      valid.push(key);
    }
  }

  if (!seen.has(def.idColumn)) {
    valid.unshift(def.idColumn);
  }

  return { columns: valid, skipped };
}

export function parseViewState(
  def: TableDefinition,
  stateJson: string
): { state: Partial<TableState>; skipped: string[] } {
  let raw: unknown;
  try {
    raw = JSON.parse(stateJson);
  } catch {
    return { state: {}, skipped: ["view:invalid"] };
  }

  if (typeof raw !== "object" || raw === null || Array.isArray(raw)) {
    return { state: {}, skipped: ["view:invalid"] };
  }

  const obj = raw as Record<string, unknown>;
  const state: Partial<TableState> = {};
  const skipped: string[] = [];
  const knownColumns = new Set(def.columns.map((c) => c.key));
  const knownFilters = new Set(def.filters);

  if (typeof obj.filters === "object" && obj.filters !== null && !Array.isArray(obj.filters)) {
    const cleanFilters: Record<string, string | string[]> = {};
    for (const [k, v] of Object.entries(obj.filters as Record<string, unknown>)) {
      if (!knownFilters.has(k)) {
        skipped.push(`f.${k}`);
        continue;
      }
      if (typeof v === "string") {
        if (v !== "") cleanFilters[k] = v;
      } else if (Array.isArray(v)) {
        const arr = v.filter((item): item is string => typeof item === "string" && item !== "");
        if (arr.length > 0) cleanFilters[k] = arr;
      }
    }
    state.filters = cleanFilters;
  }

  if (typeof obj.search === "string") {
    state.search = obj.search;
  }

  if (obj.sort === null) {
    state.sort = null;
  } else if (typeof obj.sort === "object" && obj.sort !== null && !Array.isArray(obj.sort)) {
    const s = obj.sort as Record<string, unknown>;
    if (
      typeof s.key === "string" &&
      knownColumns.has(s.key) &&
      (s.direction === "asc" || s.direction === "desc")
    ) {
      state.sort = { key: s.key, direction: s.direction };
    } else if (typeof s.key === "string") {
      skipped.push(`sort:${s.key}`);
    }
  }

  if (VALID_PAGE_SIZES.has(obj.pageSize)) {
    state.pageSize = obj.pageSize as TableState["pageSize"];
  }

  if (Array.isArray(obj.columns)) {
    const rawCols = obj.columns.filter((c): c is string => typeof c === "string");
    const colResult = sanitizeColumnsForDef(def, rawCols);
    state.columns = colResult.columns;
    skipped.push(...colResult.skipped);
  }

  if (obj.density === "comfortable" || obj.density === "compact") {
    state.density = obj.density;
  }

  return { state, skipped };
}

function formatSkippedItem(item: string): string {
  if (item.startsWith("column:")) {
    return `column '${item.slice("column:".length)}'`;
  }
  if (item.startsWith("f.")) {
    return `filter '${item.slice(2)}'`;
  }
  if (item.startsWith("sort:")) {
    return `sort '${item.slice("sort:".length)}'`;
  }
  if (item.startsWith("page:")) {
    return `page '${item.slice("page:".length)}'`;
  }
  if (item.startsWith("size:")) {
    return `page size '${item.slice("size:".length)}'`;
  }
  if (item.startsWith("view:")) {
    return `view '${item.slice("view:".length)}'`;
  }
  return item;
}

export function formatSkippedNotice(skipped: string[]): string {
  const unique = Array.from(new Set(skipped));
  return `Not applied: ${unique.map(formatSkippedItem).join(", ")}`;
}

function sameStringArray(a: string[], b: string[]): boolean {
  return a.length === b.length && a.every((v, i) => v === b[i]);
}

function sameFilters(
  a: Record<string, string | string[]>,
  b: Record<string, string | string[]>
): boolean {
  const keysA = Object.keys(a).filter((k) => {
    const v = a[k];
    return Array.isArray(v) ? v.length > 0 : v !== "";
  });
  const keysB = Object.keys(b).filter((k) => {
    const v = b[k];
    return Array.isArray(v) ? v.length > 0 : v !== "";
  });
  if (keysA.length !== keysB.length) return false;
  return keysA.every((k) => {
    const va = a[k];
    const vb = b[k];
    if (Array.isArray(va) && Array.isArray(vb)) {
      return sameStringArray(va, vb);
    }
    return va === vb;
  });
}

function sameSort(a: TableState["sort"], b: TableState["sort"]): boolean {
  if (a === null && b === null) return true;
  if (a === null || b === null) return false;
  return a.key === b.key && a.direction === b.direction;
}

export function isStateEqualToView(
  def: TableDefinition,
  current: TableState,
  viewStateJson: string,
  defaults: Partial<TableState> | undefined,
  defaultColumns: string[],
  defaultDensity: "comfortable" | "compact",
  isBench: boolean
): boolean {
  const { state: parsed } = parseViewState(def, viewStateJson);
  const expectedFilters = parsed.filters ?? defaults?.filters ?? {};
  const expectedSearch = parsed.search ?? defaults?.search ?? "";
  const expectedSort = parsed.sort !== undefined ? parsed.sort : (defaults?.sort ?? null);
  const expectedPageSize = parsed.pageSize ?? defaults?.pageSize ?? 25;
  const expectedColumns =
    parsed.columns && parsed.columns.length > 0 ? parsed.columns : defaultColumns;
  const expectedDensity: "comfortable" | "compact" = isBench
    ? "comfortable"
    : (parsed.density ?? defaultDensity);

  return (
    sameFilters(current.filters, expectedFilters) &&
    current.search === expectedSearch &&
    sameSort(current.sort, expectedSort) &&
    current.pageSize === expectedPageSize &&
    sameStringArray(current.columns, expectedColumns) &&
    current.density === expectedDensity
  );
}
