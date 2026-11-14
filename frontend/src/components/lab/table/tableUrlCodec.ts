import type { TableDefinition } from "./tableRegistry";

export interface TableState {
  filters: Record<string, string | string[]>;
  search: string;
  sort: { key: string; direction: "asc" | "desc" } | null;
  pageSize: 25 | 50 | 100 | "all";
  columns: string[];
  density: "comfortable" | "compact";
}

export interface CodecOptions {
  defaults?: Partial<TableState>;
  prefix?: string;
}

export interface DecodedTableState {
  state: Partial<TableState>;
  page: number;
  viewId: number | null;
  skipped: string[];
}

const VALID_PAGE_SIZES = new Set<string>(["25", "50", "100", "all"]);
const OWNED_BASE_KEYS = new Set<string>(["q", "sort", "page", "size", "view"]);

function paramKey(base: string, prefix?: string): string {
  return prefix ? `${prefix}.${base}` : base;
}

function sameFilterValue(
  a: string | string[] | undefined,
  b: string | string[] | undefined
): boolean {
  if (a === undefined && b === undefined) return true;
  if (a === undefined || b === undefined) return false;
  if (Array.isArray(a) && Array.isArray(b)) {
    return a.length === b.length && a.every((v, i) => v === b[i]);
  }
  return a === b;
}

function sameSort(
  a: TableState["sort"],
  b: TableState["sort"] | undefined
): boolean {
  const normB = b ?? null;
  if (a === null && normB === null) return true;
  if (a === null || normB === null) return false;
  return a.key === normB.key && a.direction === normB.direction;
}

/**
 * Returns true if the URLSearchParams key belongs to this table (given its optional prefix).
 */
export function isOwnedParam(key: string, prefix?: string): boolean {
  if (prefix) {
    return key.startsWith(`${prefix}.`);
  }
  return OWNED_BASE_KEYS.has(key) || key.startsWith("f.");
}

export function encodeState(
  def: TableDefinition,
  s: TableState,
  page: number,
  viewId: number | null,
  options?: CodecOptions
): URLSearchParams {
  const params = new URLSearchParams();
  const defaults = options?.defaults;
  const prefix = options?.prefix;

  const defaultSearch = defaults?.search ?? "";
  if (s.search && s.search !== defaultSearch) {
    params.set(paramKey("q", prefix), s.search);
  }

  if (s.sort && !sameSort(s.sort, defaults?.sort)) {
    params.set(paramKey("sort", prefix), `${s.sort.key}:${s.sort.direction}`);
  }

  if (page > 1) {
    params.set(paramKey("page", prefix), String(page));
  }

  const defaultPageSize = defaults?.pageSize ?? 25;
  if (s.pageSize !== defaultPageSize) {
    params.set(paramKey("size", prefix), String(s.pageSize));
  }

  if (viewId !== null) {
    params.set(paramKey("view", prefix), String(viewId));
  }

  const knownFilters = new Set(def.filters);
  for (const name of def.filters) {
    if (!knownFilters.has(name)) continue;
    const val = s.filters[name];
    const defVal = defaults?.filters?.[name];
    if (val === undefined) continue;
    if (Array.isArray(val)) {
      if (val.length === 0 || sameFilterValue(val, defVal)) continue;
      for (const item of val) {
        if (item !== "") {
          params.append(paramKey(`f.${name}`, prefix), item);
        }
      }
    } else {
      if (val === "" || sameFilterValue(val, defVal)) continue;
      params.set(paramKey(`f.${name}`, prefix), val);
    }
  }

  return params;
}

export function decodeState(
  def: TableDefinition,
  p: URLSearchParams,
  options?: Pick<CodecOptions, "prefix">
): DecodedTableState {
  const prefix = options?.prefix;
  const state: Partial<TableState> = {};
  let page = 1;
  let viewId: number | null = null;
  const skipped: string[] = [];

  const knownColumns = new Set(def.columns.map((c) => c.key));
  const knownFilters = new Set(def.filters);
  const filters: Record<string, string | string[]> = {};
  let hasFilters = false;

  const seenKeys = new Set<string>();
  for (const rawKey of p.keys()) {
    if (seenKeys.has(rawKey)) continue;
    seenKeys.add(rawKey);

    if (prefix) {
      if (!rawKey.startsWith(`${prefix}.`)) continue;
    }

    const key = prefix ? rawKey.slice(prefix.length + 1) : rawKey;

    if (key === "q") {
      const val = p.get(rawKey);
      if (val !== null && val !== "") {
        state.search = val;
      }
      continue;
    }

    if (key === "sort") {
      const val = p.get(rawKey) ?? "";
      const parts = val.split(":");
      if (
        parts.length === 2 &&
        knownColumns.has(parts[0]) &&
        (parts[1] === "asc" || parts[1] === "desc")
      ) {
        state.sort = { key: parts[0], direction: parts[1] };
      } else {
        skipped.push(`sort:${val}`);
      }
      continue;
    }

    if (key === "page") {
      const val = p.get(rawKey) ?? "";
      const parsed = Number(val);
      if (Number.isInteger(parsed) && parsed >= 1) {
        page = parsed;
      } else {
        skipped.push(`page:${val}`);
      }
      continue;
    }

    if (key === "size") {
      const val = p.get(rawKey) ?? "";
      if (VALID_PAGE_SIZES.has(val)) {
        state.pageSize = val === "all" ? "all" : (Number(val) as 25 | 50 | 100);
      } else {
        skipped.push(`size:${val}`);
      }
      continue;
    }

    if (key === "view") {
      const val = p.get(rawKey) ?? "";
      const parsed = Number(val);
      if (Number.isInteger(parsed) && parsed >= 1) {
        viewId = parsed;
      } else {
        skipped.push(`view:${val}`);
      }
      continue;
    }

    if (key.startsWith("f.")) {
      const filterName = key.slice(2);
      if (!knownFilters.has(filterName)) {
        skipped.push(key);
        continue;
      }
      const values = p.getAll(rawKey).filter((v) => v !== "");
      if (values.length === 1) {
        filters[filterName] = values[0];
        hasFilters = true;
      } else if (values.length > 1) {
        filters[filterName] = values;
        hasFilters = true;
      }
      continue;
    }

    skipped.push(key);
  }

  if (hasFilters) {
    state.filters = filters;
  }

  return { state, page, viewId, skipped };
}
