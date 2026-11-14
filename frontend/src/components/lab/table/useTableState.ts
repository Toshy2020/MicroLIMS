import { useCallback, useMemo, useState } from "react";
import { useTheme } from "@mui/material/styles";
import { useSearchParams } from "react-router-dom";
import { useBenchMode } from "../../../theme/BenchModeContext";
import { getTable, type TableDefinition } from "./tableRegistry";
import {
  decodeState,
  encodeState,
  isOwnedParam,
  type TableState,
} from "./tableUrlCodec";

export interface UseTableState {
  state: TableState;
  page: number;
  viewId: number | null;
  isDefault: boolean;
  skipped: string[];
  setFilter(name: string, value: string | string[] | null): void;
  setSearch(v: string): void;
  setSort(s: TableState["sort"]): void;
  setPage(p: number): void;
  setPageSize(s: TableState["pageSize"]): void;
  setColumns(c: string[]): void;
  setDensity(d: TableState["density"]): void;
  applyState(s: Partial<TableState>, viewId: number | null): void;
  reset(): void;
}

export interface UseTableStateOptions {
  /**
   * Optional URL parameter prefix for pages with multiple tables
   * (e.g. prefix "t2" produces ?t2.q=...&t2.f.status=...).
   */
  prefix?: string;
}

interface StoredTablePrefs {
  columns?: unknown;
  density?: unknown;
}

const STORAGE_PREFIX = "microlims.table.";

function storageKey(tableKey: string): string {
  return `${STORAGE_PREFIX}${tableKey}`;
}

function getDefaultColumns(
  def: TableDefinition,
  defaults?: Partial<TableState>
): string[] {
  if (defaults?.columns && defaults.columns.length > 0) {
    return sanitizeColumns(def, defaults.columns).columns;
  }
  return def.columns.filter((c) => c.defaultVisible).map((c) => c.key);
}

function sanitizeColumns(
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

function readStoredPrefs(
  tableKey: string,
  def: TableDefinition,
  defaultColumns: string[],
  defaultDensity: "comfortable" | "compact"
): { columns: string[]; density: "comfortable" | "compact"; skipped: string[] } {
  try {
    const raw = localStorage.getItem(storageKey(tableKey));
    if (!raw) {
      return { columns: defaultColumns, density: defaultDensity, skipped: [] };
    }
    const parsed = JSON.parse(raw) as StoredTablePrefs;
    let columns = defaultColumns;
    let skipped: string[] = [];
    if (Array.isArray(parsed.columns)) {
      const stringCols = parsed.columns.filter(
        (c): c is string => typeof c === "string"
      );
      const result = sanitizeColumns(def, stringCols);
      columns = result.columns.length > 0 ? result.columns : defaultColumns;
      skipped = result.skipped;
    }
    const density =
      parsed.density === "compact" || parsed.density === "comfortable"
        ? parsed.density
        : defaultDensity;
    return { columns, density, skipped };
  } catch {
    return { columns: defaultColumns, density: defaultDensity, skipped: [] };
  }
}

function writeStoredPrefs(
  tableKey: string,
  columns: string[],
  density: "comfortable" | "compact"
): void {
  try {
    localStorage.setItem(
      storageKey(tableKey),
      JSON.stringify({ columns, density })
    );
  } catch {
    // Storage unavailable or quota exceeded; state still applies in memory.
  }
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

export function useTableState(
  tableKey: string,
  defaults?: Partial<TableState>,
  options?: UseTableStateOptions
): UseTableState {
  const def = getTable(tableKey);
  const prefix = options?.prefix;
  const theme = useTheme();
  const { benchMode } = useBenchMode();
  const isBench = benchMode || Boolean(theme.custom?.bench);

  const defaultColumns = useMemo(
    () => getDefaultColumns(def, defaults),
    [def, defaults]
  );
  const defaultDensity: "comfortable" | "compact" =
    defaults?.density ?? "comfortable";

  const [prefs, setPrefs] = useState(() =>
    readStoredPrefs(tableKey, def, defaultColumns, defaultDensity)
  );
  const [searchParams, setSearchParams] = useSearchParams();

  const decoded = useMemo(
    () => decodeState(def, searchParams, { prefix }),
    [def, searchParams, prefix]
  );

  const effectiveDensity: "comfortable" | "compact" = isBench
    ? "comfortable"
    : prefs.density;

  const state: TableState = useMemo(
    () => ({
      filters: decoded.state.filters ?? defaults?.filters ?? {},
      search: decoded.state.search ?? defaults?.search ?? "",
      sort:
        decoded.state.sort !== undefined
          ? decoded.state.sort
          : (defaults?.sort ?? null),
      pageSize: decoded.state.pageSize ?? defaults?.pageSize ?? 25,
      columns: prefs.columns,
      density: effectiveDensity,
    }),
    [decoded.state, defaults, prefs.columns, effectiveDensity]
  );

  const skipped = useMemo(
    () => [...decoded.skipped, ...prefs.skipped],
    [decoded.skipped, prefs.skipped]
  );

  const isDefault = useMemo(() => {
    const defFilters = defaults?.filters ?? {};
    const defSearch = defaults?.search ?? "";
    const defSort = defaults?.sort ?? null;
    const defPageSize = defaults?.pageSize ?? 25;
    const sortMatches =
      state.sort === null && defSort === null
        ? true
        : state.sort !== null &&
          defSort !== null &&
          state.sort.key === defSort.key &&
          state.sort.direction === defSort.direction;

    return (
      decoded.page === 1 &&
      decoded.viewId === null &&
      state.search === defSearch &&
      sortMatches &&
      state.pageSize === defPageSize &&
      sameFilters(state.filters, defFilters) &&
      sameStringArray(state.columns, defaultColumns) &&
      state.density === (isBench ? "comfortable" : defaultDensity)
    );
  }, [
    decoded.page,
    decoded.viewId,
    state,
    defaults,
    defaultColumns,
    defaultDensity,
    isBench,
  ]);

  const commitUrl = useCallback(
    (
      nextState: TableState,
      nextPage: number,
      nextViewId: number | null,
      replace: boolean
    ) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams();
          prev.forEach((value, key) => {
            if (!isOwnedParam(key, prefix)) {
              next.append(key, value);
            }
          });
          const encoded = encodeState(def, nextState, nextPage, nextViewId, {
            defaults,
            prefix,
          });
          encoded.forEach((value, key) => {
            next.append(key, value);
          });
          return next;
        },
        { replace }
      );
    },
    [def, defaults, prefix, setSearchParams]
  );

  const setFilter = useCallback(
    (name: string, value: string | string[] | null) => {
      const nextFilters = { ...state.filters };
      if (
        value === null ||
        value === "" ||
        (Array.isArray(value) && value.length === 0)
      ) {
        delete nextFilters[name];
      } else {
        nextFilters[name] = value;
      }
      commitUrl(
        { ...state, filters: nextFilters },
        1,
        decoded.viewId,
        false
      );
    },
    [state, decoded.viewId, commitUrl]
  );

  const setSearch = useCallback(
    (v: string) => {
      commitUrl({ ...state, search: v }, 1, decoded.viewId, true);
    },
    [state, decoded.viewId, commitUrl]
  );

  const setSort = useCallback(
    (s: TableState["sort"]) => {
      commitUrl({ ...state, sort: s }, decoded.page, decoded.viewId, false);
    },
    [state, decoded.page, decoded.viewId, commitUrl]
  );

  const setPage = useCallback(
    (p: number) => {
      const nextPage = Number.isInteger(p) && p >= 1 ? p : 1;
      commitUrl(state, nextPage, decoded.viewId, false);
    },
    [state, decoded.viewId, commitUrl]
  );

  const setPageSize = useCallback(
    (s: TableState["pageSize"]) => {
      commitUrl({ ...state, pageSize: s }, 1, decoded.viewId, false);
    },
    [state, decoded.viewId, commitUrl]
  );

  const setColumns = useCallback(
    (c: string[]) => {
      const sanitized = sanitizeColumns(def, c);
      const nextCols =
        sanitized.columns.length > 0 ? sanitized.columns : defaultColumns;
      setPrefs((prev) => {
        writeStoredPrefs(tableKey, nextCols, prev.density);
        return {
          columns: nextCols,
          density: prev.density,
          skipped: sanitized.skipped,
        };
      });
    },
    [def, defaultColumns, tableKey]
  );

  const setDensity = useCallback(
    (d: TableState["density"]) => {
      if (isBench && d === "compact") {
        return;
      }
      setPrefs((prev) => {
        writeStoredPrefs(tableKey, prev.columns, d);
        return { ...prev, density: d };
      });
    },
    [isBench, tableKey]
  );

  const applyState = useCallback(
    (partial: Partial<TableState>, viewId: number | null) => {
      const nextState: TableState = {
        filters: partial.filters ?? defaults?.filters ?? {},
        search: partial.search ?? defaults?.search ?? "",
        sort:
          partial.sort !== undefined
            ? partial.sort
            : (defaults?.sort ?? null),
        pageSize: partial.pageSize ?? defaults?.pageSize ?? 25,
        columns: prefs.columns,
        density: effectiveDensity,
      };

      if (partial.columns || partial.density) {
        let nextCols = prefs.columns;
        let nextSkipped: string[] = [];
        if (partial.columns) {
          const sanitized = sanitizeColumns(def, partial.columns);
          nextCols =
            sanitized.columns.length > 0 ? sanitized.columns : defaultColumns;
          nextSkipped = sanitized.skipped;
        }
        const nextDensity =
          !isBench &&
          (partial.density === "compact" || partial.density === "comfortable")
            ? partial.density
            : prefs.density;

        setPrefs({
          columns: nextCols,
          density: nextDensity,
          skipped: nextSkipped,
        });
        writeStoredPrefs(tableKey, nextCols, nextDensity);
      }

      commitUrl(nextState, 1, viewId, false);
    },
    [
      def,
      defaults,
      defaultColumns,
      prefs.columns,
      prefs.density,
      effectiveDensity,
      isBench,
      tableKey,
      commitUrl,
    ]
  );

  const reset = useCallback(() => {
    const resetState: TableState = {
      filters: defaults?.filters ?? {},
      search: defaults?.search ?? "",
      sort: defaults?.sort ?? null,
      pageSize: defaults?.pageSize ?? 25,
      columns: defaultColumns,
      density: isBench ? "comfortable" : defaultDensity,
    };
    setPrefs({
      columns: defaultColumns,
      density: defaultDensity,
      skipped: [],
    });
    try {
      localStorage.removeItem(storageKey(tableKey));
    } catch {
      // Ignore storage errors.
    }
    commitUrl(resetState, 1, null, false);
  }, [
    defaults,
    defaultColumns,
    defaultDensity,
    isBench,
    tableKey,
    commitUrl,
  ]);

  return {
    state,
    page: decoded.page,
    viewId: decoded.viewId,
    isDefault,
    skipped,
    setFilter,
    setSearch,
    setSort,
    setPage,
    setPageSize,
    setColumns,
    setDensity,
    applyState,
    reset,
  };
}
