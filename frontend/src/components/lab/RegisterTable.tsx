import { Fragment, useEffect, useId, useMemo, useRef, useState } from "react";
import type { KeyboardEvent, MouseEvent, ReactNode, SyntheticEvent } from "react";
import {
  Box, Checkbox, IconButton, ListItemText, Menu, MenuItem, Paper, Select, Skeleton, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TableSortLabel, Typography, useMediaQuery, useTheme
} from "@mui/material";
import type { SxProps, Theme } from "@mui/material/styles";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import { toast } from "sonner";
import { visuallyHidden } from "@mui/utils";
import { Ltr } from "../Ltr";
import { useTranslation } from "../../i18n";
import { useOptionalAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { tableHeadSx } from "../../theme";
import { useBenchMode } from "../../theme/BenchModeContext";
import type { StatusTone } from "../../theme/statusTokens";
import { EmptyState } from "./EmptyState";
import { BulkActionBar, type BulkAction } from "./table/BulkActionBar";
import { exportRegisterRows, MAX_EXPORT_ROWS } from "./table/exportRows";
import type { RowSelection } from "./table/useRowSelection";
import { useTableState, type UseTableState } from "./table/useTableState";
import { VirtualBody } from "./table/VirtualBody";

export interface RegisterColumn<T> {
  key: keyof T | string;
  label: string;
  render?: (row: T) => ReactNode;
  align?: "left" | "right" | "center";
  sortable?: boolean;
  // Value compared when sorting; falls back to row[key].
  sortValue?: (row: T) => string | number | null | undefined;
  width?: number | string;
  // Right-aligned with tabular figures.
  numeric?: boolean;
  // Keeps identifiers (asset codes, lot numbers) on one line.
  nowrap?: boolean;
}

export interface RegisterRowAction {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  // Destructive actions render in the error color.
  danger?: boolean;
  // The reason this action cannot be taken now (the server's own sentence). The item stays focusable, ignores
  // selection and shows the reason as its secondary text, so the reason is never tooltip-only.
  reason?: string;
}

interface RegisterTableProps<T> {
  columns: RegisterColumn<T>[];
  rows: T[];
  getRowId: (row: T) => string | number;
  // A mouse click also reports its event, so a caller can ignore clicks that landed on a control inside the row.
  onRowClick?: (row: T, event?: SyntheticEvent<HTMLElement>) => void;
  rowActions?: (row: T) => RegisterRowAction[];
  // The row whose actions menu is open, or null when it closes - for a caller that asks the server about the
  // row's actions only when someone looks at them.
  onRowActionsOpen?: (row: T | null) => void;
  loading?: boolean;
  empty: { title: string; description?: string; action?: ReactNode };
  // Default 25; options are 25/50/100.
  // "all" shows every row (virtualised above 100 rows on desktop; phones still page by 25).
  pageSize?: 25 | 50 | 100 | "all";
  dense?: boolean;
  // Sort applied before the user clicks a header.
  defaultSort?: { key: string; direction: "asc" | "desc" };
  // Tints the row (and marks its first cell with a left border) so GMP warnings
  // such as low stock or overdue calibration stay visible at a glance.
  rowTone?: (row: T) => StatusTone | undefined;
  tableKey?: string;
  tableState?: UseTableState;
  toolbar?: ReactNode;
  selection?: RowSelection<string | number>;
  isSelectable?: (row: T) => boolean;
  bulkActions?: BulkAction<string | number>[];
  totalMatching?: number;
  loadAllMatching?(): Promise<T[]>;
  // Expandable rows: a toggle button is added when renderExpanded is given.
  // Expanded state is owned by the caller so it survives virtual unmounting.
  renderExpanded?(row: T): ReactNode;
  expandedIds?: ReadonlySet<string | number>;
  onToggleExpand?(id: string | number): void;
  // Only these rows get an expand toggle (default: every row when renderExpanded is given).
  isExpandable?(row: T): boolean;
  // Extra row styling merged after the tone styling (selected row, state edge).
  rowSx?(row: T): SxProps<Theme> | undefined;
  // Accessible names for the selection checkboxes (default "Select row <id>" / "Select all rows on page").
  selectionLabels?: {
    row?(row: T): string;
    page?(count: number, allSelected: boolean): string;
  };
  // Ids the header checkbox toggles for the rows on the page (default: every selectable row on the page).
  pageSelectionIds?(pagedRows: T[]): Array<string | number>;
  // Label of the built-in audited CSV export action (default "Export CSV").
  exportLabel?: string;
  // A last cell on every row (for example a set of icon buttons), after the chosen columns and never hidden.
  trailingCell?: { label: string; render(row: T): ReactNode };
  // Keep the selection checkboxes even when the caller has no bulk action the user may run
  // (the selection also feeds something outside the table, such as a grouped-actions panel).
  alwaysSelectable?: boolean;
  // The rows are already one page from the server: no client slicing, the pager counts the server total.
  // When sortableColumns and onSortChange are provided, header sorting delegates to the server.
  serverPaged?: {
    totalCount: number;
    page?: number;
    onPageChange?: (nextZeroBasedPage: number) => void;
    pageSize?: number;
    onPageSizeChange?: (nextSize: number) => void;
    pageSizeOptions?: number[];
    sortableColumns?: ReadonlySet<string> | readonly string[];
    onSortChange?: (next: { key: string; direction: "asc" | "desc" } | null) => void;
    sort?: { key: string; direction: "asc" | "desc" } | null;
  };
}

const PAGE_SIZES = [25, 50, 100];
const SKELETON_ROWS = 5;
// Above this many rows on a page the table body scrolls inside a bounded
// region on desktop, so the column headers stay in view. Shorter pages keep
// the page itself as the only scroll container (no second scrollbar).
const STICKY_HEADER_MIN_ROWS = 15;
// Above this many rendered rows the desktop table body is windowed.
const VIRTUALISE_ABOVE_ROWS = 100;
const ALL_ROWS_OPTION = -1;
const DESKTOP_PAGE_SIZES = [...PAGE_SIZES, ALL_ROWS_OPTION];

function sxList(sx: SxProps<Theme> | undefined): ReadonlyArray<boolean | object> {
  if (!sx) return [];
  return Array.isArray(sx) ? (sx as ReadonlyArray<boolean | object>) : [sx as object];
}

// The pager's own words, and the "All" option, in the interface language.
function usePaginationText() {
  const { t } = useTranslation();
  return {
    labelRowsPerPage: t("table.rowsPerPage"),
    labelDisplayedRows: ({ from, to, count }: { from: number; to: number; count: number }) =>
      t("table.displayedRows", { from, to, total: count }),
    getItemAriaLabel: (type: "first" | "last" | "next" | "previous") =>
      t(`table.${type}Page`)
  };
}

function compareValues(a: unknown, b: unknown): number {
  // Empty values sort last in ascending order.
  const aEmpty = a === null || a === undefined || a === "";
  const bEmpty = b === null || b === undefined || b === "";
  if (aEmpty || bEmpty) return aEmpty === bEmpty ? 0 : aEmpty ? 1 : -1;
  if (typeof a === "number" && typeof b === "number") return a - b;
  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: "base" });
}

export function RegisterTable<T>(props: RegisterTableProps<T>) {
  if (props.tableKey && !props.tableState) {
    return <ManagedRegisterTable {...props} tableKey={props.tableKey} />;
  }
  return <RegisterTableInner {...props} />;
}

function ManagedRegisterTable<T>({
  tableKey,
  ...rest
}: RegisterTableProps<T> & { tableKey: string }) {
  const tableState = useTableState(tableKey);
  return <RegisterTableInner {...rest} tableKey={tableKey} tableState={tableState} />;
}

function reasonIdOf(menuId: string, index: number): string {
  return `${menuId}-reason-${index}`;
}

function gatedItemProps(reason: string | undefined, menuId: string, index: number) {
  return reason ? { "aria-disabled": true, "aria-describedby": reasonIdOf(menuId, index) } : {};
}

function RegisterTableInner<T>({
  columns, rows, getRowId, onRowClick, rowActions, onRowActionsOpen, loading, empty, pageSize = 25, dense = true, defaultSort, rowTone, tableKey, tableState, toolbar,
  selection, isSelectable, bulkActions, totalMatching, loadAllMatching, renderExpanded, expandedIds, onToggleExpand,
  isExpandable, rowSx, selectionLabels, pageSelectionIds, exportLabel, serverPaged, trailingCell, alwaysSelectable
}: RegisterTableProps<T>) {
  const theme = useTheme();
  const { t } = useTranslation();
  const paginationText = usePaginationText();
  const desktopPageSizeOptions = DESKTOP_PAGE_SIZES.map((value) =>
    value === ALL_ROWS_OPTION ? { label: t("table.all"), value } : value
  );
  const auth = useOptionalAuth();
  const { benchMode } = useBenchMode();
  const isBench = benchMode || Boolean(theme.custom?.bench);
  // Phones get one card per record instead of a table whose columns would be
  // cut off or need sideways scrolling to read a single record.
  const isPhone = useMediaQuery(theme.breakpoints.down("sm"));
  const [localSort, setLocalSort] = useState<{ key: string; direction: "asc" | "desc" } | null>(defaultSort ?? null);
  const [localPage, setLocalPage] = useState(0);
  const [localRowsPerPage, setLocalRowsPerPage] = useState<number | "all">(pageSize);
  const [scrollElement, setScrollElement] = useState<HTMLDivElement | null>(null);
  const [menu, setMenu] = useState<{ anchor: HTMLElement; row: T } | null>(null);
  const menuId = useId();
  const menuRow = menu?.row ?? null;
  const onRowActionsOpenRef = useRef(onRowActionsOpen);
  onRowActionsOpenRef.current = onRowActionsOpen;
  useEffect(() => {
    onRowActionsOpenRef.current?.(menuRow);
  }, [menuRow]);

  // Filtering or reloading changes the row set; a stale page could be past the end.
  useEffect(() => {
    if (!tableState && serverPaged?.page === undefined) {
      setLocalPage(0);
    }
  }, [rows.length, tableState, serverPaged?.page]);

  const serverSortableSet = useMemo(
    () => (serverPaged?.sortableColumns ? new Set(serverPaged.sortableColumns) : null),
    [serverPaged?.sortableColumns]
  );
  const serverSortEnabled = Boolean(serverPaged?.onSortChange && serverSortableSet);
  const sort = serverPaged && serverPaged.sort !== undefined
    ? serverPaged.sort
    : (tableState ? tableState.state.sort : localSort);
  const page = serverPaged?.page !== undefined
    ? serverPaged.page
    : (tableState ? tableState.page - 1 : localPage);
  const requestedAll = tableState ? tableState.state.pageSize === "all" : localRowsPerPage === "all";
  // Phone cards are never windowed, so "all" falls back to the first page size there.
  // A server page is never "all": the server caps a page, so it falls back to the largest page size.
  const isAllPageSize = requestedAll && !isPhone && !serverPaged;
  const requestedSize = serverPaged?.pageSize !== undefined
    ? serverPaged.pageSize
    : (tableState ? tableState.state.pageSize : localRowsPerPage);
  const rowsPerPage = requestedSize === "all"
    ? (isPhone ? PAGE_SIZES[0] : serverPaged ? PAGE_SIZES[PAGE_SIZES.length - 1] : Math.max(rows.length, 1))
    : requestedSize;

  const displayedColumns = useMemo(() => {
    if (!tableState) return columns;
    const byKey = new Map(columns.map((c) => [String(c.key), c]));
    return tableState.state.columns
      .map((key) => byKey.get(key))
      .filter((c): c is RegisterColumn<T> => Boolean(c));
  }, [columns, tableState]);

  const sorted = useMemo(() => {
    // A server-paged list holds one page only: sorting it would look like a sort of every record.
    if (!sort || serverPaged) return rows;
    const col = columns.find((c) => String(c.key) === sort.key);
    if (!col) return rows;
    const valueOf = (row: T) => (col.sortValue ? col.sortValue(row) : (row as Record<string, unknown>)[String(col.key)]);
    const dir = sort.direction === "asc" ? 1 : -1;
    // Empties stay last in both directions, so compare them outside the sign flip.
    return [...rows].sort((x, y) => {
      const a = valueOf(x);
      const b = valueOf(y);
      const aEmpty = a === null || a === undefined || a === "";
      const bEmpty = b === null || b === undefined || b === "";
      if (aEmpty || bEmpty) return compareValues(a, b);
      return dir * compareValues(a, b);
    });
  }, [rows, columns, sort, serverPaged]);

  const paged = serverPaged || isAllPageSize || sorted.length <= PAGE_SIZES[0]
    ? sorted
    : sorted.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
  const showPagination = !loading && (serverPaged ? serverPaged.totalCount > 0 : rows.length > PAGE_SIZES[0]);
  const paginationCount = serverPaged ? serverPaged.totalCount : rows.length;
  const virtualised = !isPhone && !loading && paged.length > VIRTUALISE_ABOVE_ROWS;
  const expandable = Boolean(renderExpanded);

  const allBulkActions = useMemo<BulkAction<string | number>[]>(() => {
    const base = bulkActions ? [...bulkActions] : [];
    if (tableKey && !base.some((a) => a.key === "export-csv")) {
      base.push({
        key: "export-csv",
        label: exportLabel ?? t("table.exportCsv"),
        permission: PERMISSIONS.DATA_EXPORT,
        run: async () => {
          if (!selection) return;
          const selectedCount = selection.allMatching ? (totalMatching ?? rows.length) : selection.selected.size;
          if (selectedCount > MAX_EXPORT_ROWS) {
            toast.error(t("table.exportLimit"));
            return;
          }
          const exportRows = selection.allMatching
            ? (loadAllMatching ? await loadAllMatching() : rows)
            : rows.filter((r) => selection.selected.has(getRowId(r)));
          await exportRegisterRows({
            tableKey,
            rows: exportRows,
            columns: displayedColumns.map((c) => ({ key: String(c.key), label: c.label, sortValue: c.sortValue })),
            getRowId,
            filters: tableState?.state.filters ?? {},
            search: tableState?.state.search ?? "",
            t
          });
        },
      });
    }
    return base;
  }, [bulkActions, tableKey, selection, totalMatching, rows, loadAllMatching, getRowId, displayedColumns, tableState, exportLabel, t]);

  const permittedBulkActions = useMemo(
    () =>
      allBulkActions.filter(
        (a) => !a.permission || (auth?.permissions.includes(a.permission) ?? false)
      ),
    [allBulkActions, auth]
  );

  const showSelection =
    Boolean(selection) && (alwaysSelectable || allBulkActions.length === 0 || permittedBulkActions.length > 0);
  const selectablePagedIds = useMemo(
    () => pageSelectionIds
      ? pageSelectionIds(paged)
      : paged.filter((r) => (isSelectable ? isSelectable(r) : true)).map((r) => getRowId(r)),
    [paged, isSelectable, getRowId, pageSelectionIds]
  );
  const selectedOnPageCount = showSelection && selection
    ? selectablePagedIds.filter((id) => selection.isSelected(id)).length
    : 0;
  const allPageSelected = selectablePagedIds.length > 0 && selectedOnPageCount === selectablePagedIds.length;
  const somePageSelected = selectedOnPageCount > 0 && !allPageSelected;
  const effectiveTotalMatching = totalMatching ?? rows.length;

  const colSpan = displayedColumns.length + (rowActions ? 1 : 0) + (trailingCell ? 1 : 0) + (showSelection ? 1 : 0) + (expandable ? 1 : 0);
  const isCompact = tableState ? (!isBench && tableState.state.density === "compact") : dense;
  const tableSize = isCompact ? "small" : "medium";
  const cellPy = tableState ? (isCompact ? 0.5 : 1.5) : (dense ? 0.75 : 1.5);

  const setSort = (next: { key: string; direction: "asc" | "desc" } | null) => {
    if (serverPaged?.onSortChange) {
      serverPaged.onSortChange(next);
      if (!tableState && serverPaged.sort === undefined) {
        setLocalSort(next);
      }
      return;
    }
    if (tableState) {
      tableState.setSort(next);
    } else {
      setLocalSort(next);
    }
  };

  const toggleSort = (key: string) => {
    const next = sort?.key === key ? (sort.direction === "asc" ? { key, direction: "desc" as const } : null) : { key, direction: "asc" as const };
    setSort(next);
  };

  const handlePageChange = (nextZeroBasedPage: number) => {
    if (serverPaged?.onPageChange) {
      serverPaged.onPageChange(nextZeroBasedPage);
      if (serverPaged.page === undefined && !tableState) {
        setLocalPage(nextZeroBasedPage);
      }
      return;
    }
    if (tableState) {
      tableState.setPage(nextZeroBasedPage + 1);
    } else {
      setLocalPage(nextZeroBasedPage);
    }
  };

  const handleRowsPerPageChange = (nextSize: number) => {
    if (serverPaged?.onPageSizeChange) {
      serverPaged.onPageSizeChange(nextSize);
      return;
    }
    if (tableState) {
      tableState.setPageSize(nextSize === ALL_ROWS_OPTION ? "all" : (nextSize as 25 | 50 | 100));
    } else {
      setLocalRowsPerPage(nextSize === ALL_ROWS_OPTION ? "all" : nextSize);
      setLocalPage(0);
    }
  };

  const handleRowKey = (e: KeyboardEvent<HTMLTableRowElement>, row: T) => {
    if (!onRowClick || e.target !== e.currentTarget) return;
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      onRowClick(row);
    }
  };

  const openMenu = (e: MouseEvent<HTMLElement>, row: T) => {
    e.stopPropagation();
    setMenu({ anchor: e.currentTarget, row });
  };

  const menuActions = menu && rowActions ? rowActions(menu.row) : [];
  // A cell with no renderer shows the raw value; numbers and identifiers (the columns marked
  // numeric or nowrap) keep their left-to-right order inside an Arabic row.
  const cellText = (row: T, col: RegisterColumn<T>): ReactNode => {
    if (col.render) return col.render(row);
    const text = String((row as Record<string, unknown>)[String(col.key)] ?? "—");
    return col.numeric || col.nowrap ? <Ltr>{text}</Ltr> : text;
  };
  const isColumnSortable = (col: RegisterColumn<T>): boolean =>
    Boolean(col.sortable) && (!serverPaged || (serverSortEnabled && Boolean(serverSortableSet?.has(String(col.key)))));
  const sortableColumns = displayedColumns.filter(isColumnSortable);
  const boundedBody = !isPhone && paged.length > STICKY_HEADER_MIN_ROWS;

  // Cells of one desktop row; shared by the plain and the virtualised body.
  const renderRowCells = (row: T, rowId: string | number, isExpanded: boolean): ReactNode => (
    <>
      {expandable && (
        <TableCell padding="checkbox" onClick={(e) => e.stopPropagation()}>
          {(isExpandable ? isExpandable(row) : true) && (
            <IconButton
              size="small"
              aria-expanded={isExpanded}
              aria-label={isExpanded ? t("table.collapseRow", { id: rowId }) : t("table.expandRow", { id: rowId })}
              onClick={() => onToggleExpand?.(rowId)}
            >
              {isExpanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
            </IconButton>
          )}
        </TableCell>
      )}
      {showSelection && selection && (() => {
        const selectable = isSelectable ? isSelectable(row) : true;
        return (
          <TableCell padding="checkbox" onClick={(e) => e.stopPropagation()}>
            <Checkbox
              size="small"
              checked={selectable && selection.isSelected(rowId)}
              disabled={!selectable}
              onChange={() => { if (selectable) selection.toggle(rowId); }}
              slotProps={{ input: { "aria-label": selectionLabels?.row ? selectionLabels.row(row) : t("table.selectRow", { id: rowId }) } }}
            />
          </TableCell>
        );
      })()}
      {displayedColumns.map((col) => {
        const content = cellText(row, col);
        return (
          <TableCell
            key={String(col.key)}
            align={col.align ?? (col.numeric ? "right" : "left")}
            sx={{ py: cellPy, ...(col.numeric ? { fontVariantNumeric: "tabular-nums" } : null), ...(col.nowrap ? { whiteSpace: "nowrap" } : null) }}
          >
            {content}
          </TableCell>
        );
      })}
      {trailingCell && (
        <TableCell align="center" sx={{ py: cellPy }}>
          {trailingCell.render(row)}
        </TableCell>
      )}
      {rowActions && (
        <TableCell align="right" sx={{ py: cellPy, px: 0.5 }}>
          {rowActions(row).length > 0 && (
            <IconButton size="small" aria-label={t("table.rowActions", { id: rowId })} onClick={(e) => openMenu(e, row)}>
              <MoreVertIcon fontSize="small" />
            </IconButton>
          )}
        </TableCell>
      )}
    </>
  );

  if (isPhone) {
    const [titleCol, ...detailCols] = displayedColumns;
    return (
      <Paper elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
        {toolbar}
        {showSelection && selection && (
          <BulkActionBar
            selection={selection}
            actions={allBulkActions}
            totalMatching={effectiveTotalMatching}
          />
        )}
        {sortableColumns.length > 0 && rows.length > 1 && (
          <Box sx={{ px: 1.5, py: 1, borderBottom: "1px solid", borderColor: "divider", display: "flex", alignItems: "center", gap: 1 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("table.sortBy")}</Typography>
            <Select
              size="small"
              value={sort ? `${sort.key}:${sort.direction}` : ""}
              displayEmpty
              onChange={(e) => {
                const v = String(e.target.value);
                if (!v) { setSort(null); return; }
                const [key, direction] = v.split(":");
                setSort({ key, direction: direction as "asc" | "desc" });
              }}
              inputProps={{ "aria-label": t("table.sortRecordsBy") }}
              sx={{ flex: 1, fontSize: 14 }}
            >
              <MenuItem value="">{t("table.defaultOrder")}</MenuItem>
              {sortableColumns.flatMap((c) => [
                <MenuItem key={`${String(c.key)}:asc`} value={`${String(c.key)}:asc`}>{t("table.sortAscending", { column: c.label })}</MenuItem>,
                <MenuItem key={`${String(c.key)}:desc`} value={`${String(c.key)}:desc`}>{t("table.sortDescending", { column: c.label })}</MenuItem>
              ])}
            </Select>
          </Box>
        )}
        {loading ? (
          <Stack divider={<Box sx={{ borderTop: "1px solid", borderColor: "divider" }} />}>
            {Array.from({ length: 3 }).map((_, i) => (
              <Box key={i} sx={{ p: 1.5 }}>
                <Skeleton variant="text" width="60%" height={24} />
                <Skeleton variant="text" width="90%" />
                <Skeleton variant="text" width="80%" />
              </Box>
            ))}
          </Stack>
        ) : rows.length === 0 ? (
          <EmptyState title={empty.title} description={empty.description} action={empty.action} />
        ) : (
          <Box component="ul" sx={{ listStyle: "none", m: 0, p: 0 }}>
            {paged.map((row) => {
              const tone = rowTone?.(row);
              const toneTokens = tone ? theme.custom.status[tone] : undefined;
              const actions = rowActions ? rowActions(row) : [];
              return (
                <Box
                  component="li"
                  key={getRowId(row)}
                  onClick={onRowClick ? (e) => onRowClick(row, e) : undefined}
                  onKeyDown={onRowClick ? (e: KeyboardEvent<HTMLLIElement>) => {
                    if (e.target === e.currentTarget && (e.key === "Enter" || e.key === " ")) {
                      e.preventDefault();
                      onRowClick(row);
                    }
                  } : undefined}
                  tabIndex={onRowClick ? 0 : undefined}
                  sx={{
                    p: 1.5,
                    borderBottom: "1px solid",
                    borderColor: "divider",
                    "&:last-of-type": { borderBottom: 0 },
                    cursor: onRowClick ? "pointer" : undefined,
                    bgcolor: toneTokens?.bg,
                    borderInlineStart: toneTokens ? `3px solid ${toneTokens.border}` : undefined,
                    "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: -2 }
                  }}
                >
                  <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1, mb: detailCols.length ? 0.75 : 0 }}>
                    {showSelection && selection && (() => {
                      const selectable = isSelectable ? isSelectable(row) : true;
                      return (
                        <Checkbox
                          size="small"
                          checked={selectable && selection.isSelected(getRowId(row))}
                          disabled={!selectable}
                          onClick={(e) => e.stopPropagation()}
                          onChange={() => { if (selectable) selection.toggle(getRowId(row)); }}
                          slotProps={{ input: { "aria-label": selectionLabels?.row ? selectionLabels.row(row) : t("table.selectRow", { id: getRowId(row) }) } }}
                          sx={{ p: 0, marginInlineEnd: 0.5 }}
                        />
                      );
                    })()}
                    <Box sx={{ flex: 1, minWidth: 0, fontWeight: 600, fontSize: 15, overflowWrap: "anywhere" }}>
                      {titleCol ? cellText(row, titleCol) : null}
                    </Box>
                    {trailingCell && trailingCell.render(row)}
                    {actions.length > 0 && (
                      <IconButton size="small" aria-label={t("table.rowActions", { id: getRowId(row) })} onClick={(e) => openMenu(e, row)} sx={{ mt: -0.5, marginInlineEnd: -0.5 }}>
                        <MoreVertIcon fontSize="small" />
                      </IconButton>
                    )}
                  </Box>
                  <Box
                    component="dl"
                    sx={{ m: 0, display: "grid", gridTemplateColumns: "minmax(96px, auto) 1fr", columnGap: 1.5, rowGap: 0.5, fontSize: 14 }}
                  >
                    {detailCols.map((col) => (
                      <Box key={String(col.key)} sx={{ display: "contents" }}>
                        <Box component="dt" sx={{ color: "text.secondary", fontSize: 13 }}>{col.label}</Box>
                        <Box component="dd" sx={{ m: 0, minWidth: 0, overflowWrap: "anywhere", ...(col.numeric ? { fontVariantNumeric: "tabular-nums" } : null) }}>
                          {cellText(row, col)}
                        </Box>
                      </Box>
                    ))}
                  </Box>
                </Box>
              );
            })}
          </Box>
        )}
        {showPagination && (
          <TablePagination
            component="div"
            count={paginationCount}
            page={page}
            onPageChange={(_, p) => handlePageChange(p)}
            rowsPerPage={rowsPerPage}
            rowsPerPageOptions={serverPaged?.pageSizeOptions ?? PAGE_SIZES}
            onRowsPerPageChange={(e) => handleRowsPerPageChange(Number(e.target.value))}
            {...paginationText}
          />
        )}
        {renderMenu()}
      </Paper>
    );
  }

  function renderMenu() {
    return (
      <Menu anchorEl={menu?.anchor} open={!!menu} onClose={() => setMenu(null)} onClick={(e) => e.stopPropagation()}>
        {menuActions.map((a, i) => (
          <MenuItem
            key={a.label}
            disabled={a.disabled}
            {...gatedItemProps(a.reason, menuId, i)}
            onClick={() => {
              if (a.reason) return;
              setMenu(null);
              a.onClick();
            }}
            sx={[
              a.danger ? { color: "error.main" } : {},
              a.reason ? { cursor: "not-allowed", alignItems: "flex-start", whiteSpace: "normal" } : {}
            ]}
          >
            <ListItemText
              primary={a.label}
              secondary={a.reason}
              slotProps={{
                primary: a.reason ? { sx: { opacity: 0.65 } } : undefined,
                secondary: { id: reasonIdOf(menuId, i), sx: { color: theme.custom.status.pending.text, fontSize: 12 } }
              }}
            />
          </MenuItem>
        ))}
      </Menu>
    );
  }

  return (
    <Paper elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
      {toolbar}
      {showSelection && selection && (
        <BulkActionBar
          selection={selection}
          actions={allBulkActions}
          totalMatching={effectiveTotalMatching}
        />
      )}
      {/* stickyHeader needs a bounded scroll region to stick within; only long
          pages get one (see STICKY_HEADER_MIN_ROWS). */}
      <TableContainer ref={setScrollElement} sx={boundedBody ? { maxHeight: "calc(100vh - 240px)", minHeight: 320 } : undefined}>
        <Table size={tableSize} stickyHeader aria-rowcount={virtualised ? paged.length + 1 : undefined}>
          <TableHead>
            <TableRow sx={tableHeadSx(theme)} aria-rowindex={virtualised ? 1 : undefined}>
              {expandable && (
                <TableCell padding="checkbox"><Box component="span" sx={visuallyHidden}>{t("table.expandColumn")}</Box></TableCell>
              )}
              {showSelection && selection && (
                <TableCell padding="checkbox">
                  <Checkbox
                    size="small"
                    checked={allPageSelected}
                    indeterminate={somePageSelected}
                    onChange={() => selection.togglePage(selectablePagedIds)}
                    slotProps={{ input: { "aria-label": selectionLabels?.page ? selectionLabels.page(selectablePagedIds.length, allPageSelected) : "Select all rows on page" } }}
                  />
                </TableCell>
              )}
              {displayedColumns.map((col) => {
                const key = String(col.key);
                const align = col.align ?? (col.numeric ? "right" : "left");
                const colSortable = isColumnSortable(col);
                const active = Boolean(colSortable && sort?.key === key);
                return (
                  <TableCell
                    key={key}
                    align={align}
                    sx={{ fontWeight: 600, width: col.width, whiteSpace: "nowrap" }}
                    aria-sort={active && sort ? (sort.direction === "asc" ? "ascending" : "descending") : undefined}
                  >
                    {colSortable ? (
                      <TableSortLabel active={active} direction={active && sort ? sort.direction : "asc"} onClick={() => toggleSort(key)}>
                        {col.label}
                      </TableSortLabel>
                    ) : (
                      col.label
                    )}
                  </TableCell>
                );
              })}
              {trailingCell && <TableCell align="center" sx={{ fontWeight: 600, whiteSpace: "nowrap" }}>{trailingCell.label}</TableCell>}
              {rowActions && <TableCell align="right" sx={{ width: 48 }}><Box component="span" sx={visuallyHidden}>{t("table.actionsColumn")}</Box></TableCell>}
            </TableRow>
          </TableHead>
          {!virtualised && (
          <TableBody>
            {loading ? (
              Array.from({ length: SKELETON_ROWS }).map((_, r) => (
                <TableRow key={`sk-${r}`}>
                  {showSelection && <TableCell padding="checkbox" />}
                  {displayedColumns.map((col, c) => (
                    <TableCell key={String(col.key)} align={col.align ?? (col.numeric ? "right" : "left")} sx={{ py: 1.5 }}>
                      <Skeleton variant="text" width={c === 0 ? "75%" : "50%"} height={20} sx={{ marginInlineStart: col.numeric ? "auto" : 0 }} />
                    </TableCell>
                  ))}
                  {trailingCell && <TableCell />}
                  {rowActions && <TableCell />}
                </TableRow>
              ))
            ) : rows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={colSpan} sx={{ borderBottom: 0 }}>
                  <EmptyState title={empty.title} description={empty.description} action={empty.action} />
                </TableCell>
              </TableRow>
            ) : (
              paged.map((row) => {
                const tone = rowTone?.(row);
                const toneTokens = tone ? theme.custom.status[tone] : undefined;
                const rowId = getRowId(row);
                const isExpanded = Boolean(renderExpanded && expandedIds?.has(rowId));
                return (
                <Fragment key={rowId}>
                <TableRow
                  hover={!!onRowClick}
                  onClick={onRowClick ? (e) => onRowClick(row, e) : undefined}
                  onKeyDown={onRowClick ? (e) => handleRowKey(e, row) : undefined}
                  tabIndex={onRowClick ? 0 : undefined}
                  sx={[
                    {
                      ...(onRowClick ? { cursor: "pointer", "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: -2 } } : null),
                      ...(toneTokens
                        ? {
                            bgcolor: toneTokens.bg,
                            "& > td:first-of-type": { borderInlineStart: `3px solid ${toneTokens.border}` },
                            "&.MuiTableRow-hover:hover": { filter: "brightness(0.97)" }
                          }
                        : null)
                    },
                    ...sxList(rowSx?.(row))
                  ]}
                >
                  {renderRowCells(row, rowId, isExpanded)}
                </TableRow>
                {isExpanded && renderExpanded && (
                  <TableRow>
                    <TableCell colSpan={colSpan} sx={{ py: 1.5, px: 2, bgcolor: "action.hover" }}>
                      {renderExpanded(row)}
                    </TableCell>
                  </TableRow>
                )}
                </Fragment>
                );
              })
            )}
          </TableBody>
          )}
          {virtualised && !loading && rows.length > 0 && (
            <VirtualBody
              rows={paged}
              getRowId={getRowId}
              scrollElement={scrollElement}
              colSpan={colSpan}
              expandedIds={expandedIds}
              renderExpanded={renderExpanded}
              renderRowCells={renderRowCells}
              onRowClick={onRowClick}
              rowTone={rowTone}
              rowSx={rowSx}
            />
          )}
        </Table>
      </TableContainer>
      {showPagination && (
        <TablePagination
          component="div"
          count={paginationCount}
          page={isAllPageSize ? 0 : page}
          onPageChange={(_, p) => handlePageChange(p)}
          rowsPerPage={isAllPageSize ? ALL_ROWS_OPTION : rowsPerPage}
          rowsPerPageOptions={serverPaged ? (serverPaged.pageSizeOptions ?? PAGE_SIZES) : desktopPageSizeOptions}
          onRowsPerPageChange={(e) => handleRowsPerPageChange(Number(e.target.value))}
          {...paginationText}
        />
      )}
      {renderMenu()}
    </Paper>
  );
}
