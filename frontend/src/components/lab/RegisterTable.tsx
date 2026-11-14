import { Fragment, useEffect, useMemo, useState } from "react";
import type { KeyboardEvent, MouseEvent, ReactNode } from "react";
import {
  Box, Checkbox, IconButton, ListItemText, Menu, MenuItem, Paper, Select, Skeleton, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TableSortLabel, Typography, useMediaQuery, useTheme
} from "@mui/material";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import { toast } from "sonner";
import { useOptionalAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { tableHeadSx } from "../../theme";
import { useBenchMode } from "../../theme/BenchModeContext";
import type { StatusTone } from "../../theme/statusTokens";
import { getErrorMessage } from "../../utils/errorMessage";
import { EmptyState } from "./EmptyState";
import { BulkActionBar, type BulkAction } from "./table/BulkActionBar";
import { buildCsv, downloadCsv } from "./table/exportCsv";
import { exportLogService } from "./table/services/exportLogService";
import { TABLES } from "./table/tableRegistry";
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
}

interface RegisterTableProps<T> {
  columns: RegisterColumn<T>[];
  rows: T[];
  getRowId: (row: T) => string | number;
  onRowClick?: (row: T) => void;
  rowActions?: (row: T) => RegisterRowAction[];
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
}

const PAGE_SIZES = [25, 50, 100];
const SKELETON_ROWS = 5;
const MAX_EXPORT_ROWS = 5000;
// Above this many rows on a page the table body scrolls inside a bounded
// region on desktop, so the column headers stay in view. Shorter pages keep
// the page itself as the only scroll container (no second scrollbar).
const STICKY_HEADER_MIN_ROWS = 15;
// Above this many rendered rows the desktop table body is windowed.
const VIRTUALISE_ABOVE_ROWS = 100;
const ALL_ROWS_OPTION = -1;
const DESKTOP_PAGE_SIZE_OPTIONS = [...PAGE_SIZES, { label: "All", value: ALL_ROWS_OPTION }];

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

function RegisterTableInner<T>({
  columns, rows, getRowId, onRowClick, rowActions, loading, empty, pageSize = 25, dense = true, defaultSort, rowTone, tableKey, tableState, toolbar,
  selection, isSelectable, bulkActions, totalMatching, loadAllMatching, renderExpanded, expandedIds, onToggleExpand
}: RegisterTableProps<T>) {
  const theme = useTheme();
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

  // Filtering or reloading changes the row set; a stale page could be past the end.
  useEffect(() => {
    if (!tableState) {
      setLocalPage(0);
    }
  }, [rows.length, tableState]);

  const sort = tableState ? tableState.state.sort : localSort;
  const page = tableState ? tableState.page - 1 : localPage;
  const requestedAll = tableState ? tableState.state.pageSize === "all" : localRowsPerPage === "all";
  // Phone cards are never windowed, so "all" falls back to the first page size there.
  const isAllPageSize = requestedAll && !isPhone;
  const requestedSize = tableState ? tableState.state.pageSize : localRowsPerPage;
  const rowsPerPage = requestedSize === "all"
    ? (isPhone ? PAGE_SIZES[0] : Math.max(rows.length, 1))
    : requestedSize;

  const displayedColumns = useMemo(() => {
    if (!tableState) return columns;
    const byKey = new Map(columns.map((c) => [String(c.key), c]));
    return tableState.state.columns
      .map((key) => byKey.get(key))
      .filter((c): c is RegisterColumn<T> => Boolean(c));
  }, [columns, tableState]);

  const sorted = useMemo(() => {
    if (!sort) return rows;
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
  }, [rows, columns, sort]);

  const paged = !isAllPageSize && sorted.length > PAGE_SIZES[0]
    ? sorted.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage)
    : sorted;
  const showPagination = !loading && rows.length > PAGE_SIZES[0];
  const virtualised = !isPhone && !loading && paged.length > VIRTUALISE_ABOVE_ROWS;
  const expandable = Boolean(renderExpanded);

  const allBulkActions = useMemo<BulkAction<string | number>[]>(() => {
    const base = bulkActions ? [...bulkActions] : [];
    if (tableKey && !base.some((a) => a.key === "export-csv")) {
      base.push({
        key: "export-csv",
        label: "Export CSV",
        permission: PERMISSIONS.DATA_EXPORT,
        run: async () => {
          if (!selection) return;
          const selectedCount = selection.allMatching ? (totalMatching ?? rows.length) : selection.selected.size;
          if (selectedCount > MAX_EXPORT_ROWS) {
            toast.error("Export is limited to 5,000 rows. Narrow your filters or use the Reports module.");
            return;
          }
          const exportRows = selection.allMatching
            ? (loadAllMatching ? await loadAllMatching() : rows)
            : rows.filter((r) => selection.selected.has(getRowId(r)));
          if (exportRows.length > MAX_EXPORT_ROWS) {
            toast.error("Export is limited to 5,000 rows. Narrow your filters or use the Reports module.");
            return;
          }
          const visibleKeys = displayedColumns.map((c) => String(c.key));
          const visibleLabels = displayedColumns.map((c) => c.label);
          const recordIds = exportRows.map((r) => String(getRowId(r)));
          const filters = tableState?.state.filters ?? {};
          const search = tableState?.state.search ?? "";
          const filterJson = JSON.stringify({ filters, search });

          let logResult;
          try {
            logResult = await exportLogService.log({
              tableKey,
              filterJson,
              columns: visibleKeys,
              rowCount: exportRows.length,
              recordIds,
            });
          } catch (err) {
            toast.error(getErrorMessage(err, "Failed to log CSV export."));
            return;
          }

          const def = TABLES[tableKey];
          const filterParts = Object.entries(filters)
            .filter(([, v]) => (Array.isArray(v) ? v.length > 0 : Boolean(v)))
            .map(([k, v]) => `${k}=${Array.isArray(v) ? v.join("|") : v}`);
          if (search) filterParts.push(`q=${search}`);

          const headerRows: string[][] = [
            ["Exported by", logResult.exportedBy],
            ["Exported at", `${logResult.serverTimeUtc} (${logResult.timeZoneId})`],
            ["List", def?.title ?? tableKey],
            ["Filters", filterParts.length > 0 ? filterParts.join("; ") : "None"],
            ["Rows", String(exportRows.length)],
            ["Export ID", String(logResult.exportId)],
          ];

          const dataRows = exportRows.map((row) =>
            displayedColumns.map((col) => {
              const raw = (row as Record<string, unknown>)[String(col.key)];
              if (raw !== undefined && raw !== null) return String(raw);
              if (col.sortValue) {
                const sv = col.sortValue(row);
                return sv !== undefined && sv !== null ? String(sv) : "";
              }
              return "";
            })
          );

          const csv = buildCsv(headerRows, visibleLabels, dataRows);
          downloadCsv(`${tableKey}-export-${logResult.exportId}.csv`, csv);
        },
      });
    }
    return base;
  }, [bulkActions, tableKey, selection, totalMatching, rows, loadAllMatching, getRowId, displayedColumns, tableState]);

  const permittedBulkActions = useMemo(
    () =>
      allBulkActions.filter(
        (a) => !a.permission || (auth?.permissions.includes(a.permission) ?? false)
      ),
    [allBulkActions, auth]
  );

  const showSelection =
    Boolean(selection) && (allBulkActions.length === 0 || permittedBulkActions.length > 0);
  const selectablePagedIds = useMemo(
    () => paged.filter((r) => (isSelectable ? isSelectable(r) : true)).map((r) => getRowId(r)),
    [paged, isSelectable, getRowId]
  );
  const selectedOnPageCount = showSelection && selection
    ? selectablePagedIds.filter((id) => selection.isSelected(id)).length
    : 0;
  const allPageSelected = selectablePagedIds.length > 0 && selectedOnPageCount === selectablePagedIds.length;
  const somePageSelected = selectedOnPageCount > 0 && !allPageSelected;
  const effectiveTotalMatching = totalMatching ?? rows.length;

  const colSpan = displayedColumns.length + (rowActions ? 1 : 0) + (showSelection ? 1 : 0) + (expandable ? 1 : 0);
  const isCompact = tableState ? (!isBench && tableState.state.density === "compact") : dense;
  const tableSize = isCompact ? "small" : "medium";
  const cellPy = tableState ? (isCompact ? 0.5 : 1.5) : (dense ? 0.75 : 1.5);

  const setSort = (next: { key: string; direction: "asc" | "desc" } | null) => {
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
    if (tableState) {
      tableState.setPage(nextZeroBasedPage + 1);
    } else {
      setLocalPage(nextZeroBasedPage);
    }
  };

  const handleRowsPerPageChange = (nextSize: number) => {
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
  const cellText = (row: T, col: RegisterColumn<T>): ReactNode =>
    col.render ? col.render(row) : String((row as Record<string, unknown>)[String(col.key)] ?? "—");
  const sortableColumns = displayedColumns.filter((c) => c.sortable);
  const boundedBody = !isPhone && paged.length > STICKY_HEADER_MIN_ROWS;

  // Cells of one desktop row; shared by the plain and the virtualised body.
  const renderRowCells = (row: T, rowId: string | number, isExpanded: boolean): ReactNode => (
    <>
      {expandable && (
        <TableCell padding="checkbox" onClick={(e) => e.stopPropagation()}>
          <IconButton
            size="small"
            aria-expanded={isExpanded}
            aria-label={`${isExpanded ? "Collapse" : "Expand"} row ${rowId}`}
            onClick={() => onToggleExpand?.(rowId)}
          >
            {isExpanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
          </IconButton>
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
              slotProps={{ input: { "aria-label": `Select row ${rowId}` } }}
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
      {rowActions && (
        <TableCell align="right" sx={{ py: cellPy, px: 0.5 }}>
          {rowActions(row).length > 0 && (
            <IconButton size="small" aria-label={`Row actions for ${rowId}`} onClick={(e) => openMenu(e, row)}>
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
            <Typography variant="caption" sx={{ color: "text.secondary" }}>Sort by</Typography>
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
              inputProps={{ "aria-label": "Sort records by" }}
              sx={{ flex: 1, fontSize: 14 }}
            >
              <MenuItem value="">Default order</MenuItem>
              {sortableColumns.flatMap((c) => [
                <MenuItem key={`${String(c.key)}:asc`} value={`${String(c.key)}:asc`}>{c.label} (ascending)</MenuItem>,
                <MenuItem key={`${String(c.key)}:desc`} value={`${String(c.key)}:desc`}>{c.label} (descending)</MenuItem>
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
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
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
                    borderLeft: toneTokens ? `3px solid ${toneTokens.border}` : undefined,
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
                          slotProps={{ input: { "aria-label": `Select row ${getRowId(row)}` } }}
                          sx={{ p: 0, mr: 0.5 }}
                        />
                      );
                    })()}
                    <Box sx={{ flex: 1, minWidth: 0, fontWeight: 600, fontSize: 15, overflowWrap: "anywhere" }}>
                      {titleCol ? cellText(row, titleCol) : null}
                    </Box>
                    {actions.length > 0 && (
                      <IconButton size="small" aria-label={`Row actions for ${getRowId(row)}`} onClick={(e) => openMenu(e, row)} sx={{ mt: -0.5, mr: -0.5 }}>
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
            count={rows.length}
            page={page}
            onPageChange={(_, p) => handlePageChange(p)}
            rowsPerPage={rowsPerPage}
            rowsPerPageOptions={PAGE_SIZES}
            onRowsPerPageChange={(e) => handleRowsPerPageChange(Number(e.target.value))}
          />
        )}
        {renderMenu()}
      </Paper>
    );
  }

  function renderMenu() {
    return (
      <Menu anchorEl={menu?.anchor} open={!!menu} onClose={() => setMenu(null)} onClick={(e) => e.stopPropagation()}>
        {menuActions.map((a) => (
          <MenuItem
            key={a.label}
            disabled={a.disabled}
            onClick={() => {
              setMenu(null);
              a.onClick();
            }}
            sx={a.danger ? { color: "error.main" } : undefined}
          >
            <ListItemText>{a.label}</ListItemText>
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
                <TableCell padding="checkbox"><span style={{ position: "absolute", left: -9999 }}>Expand</span></TableCell>
              )}
              {showSelection && selection && (
                <TableCell padding="checkbox">
                  <Checkbox
                    size="small"
                    checked={allPageSelected}
                    indeterminate={somePageSelected}
                    onChange={() => selection.togglePage(selectablePagedIds)}
                    slotProps={{ input: { "aria-label": "Select all rows on page" } }}
                  />
                </TableCell>
              )}
              {displayedColumns.map((col) => {
                const key = String(col.key);
                const align = col.align ?? (col.numeric ? "right" : "left");
                const active = sort?.key === key;
                return (
                  <TableCell
                    key={key}
                    align={align}
                    sx={{ fontWeight: 600, width: col.width, whiteSpace: "nowrap" }}
                    aria-sort={active ? (sort.direction === "asc" ? "ascending" : "descending") : undefined}
                  >
                    {col.sortable ? (
                      <TableSortLabel active={active} direction={active ? sort.direction : "asc"} onClick={() => toggleSort(key)}>
                        {col.label}
                      </TableSortLabel>
                    ) : (
                      col.label
                    )}
                  </TableCell>
                );
              })}
              {rowActions && <TableCell align="right" sx={{ width: 48 }}><span style={{ position: "absolute", left: -9999 }}>Actions</span></TableCell>}
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
                      <Skeleton variant="text" width={c === 0 ? "75%" : "50%"} height={20} sx={{ ml: col.numeric ? "auto" : 0 }} />
                    </TableCell>
                  ))}
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
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  onKeyDown={onRowClick ? (e) => handleRowKey(e, row) : undefined}
                  tabIndex={onRowClick ? 0 : undefined}
                  sx={{
                    ...(onRowClick ? { cursor: "pointer", "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: -2 } } : null),
                    ...(toneTokens
                      ? {
                          bgcolor: toneTokens.bg,
                          "& > td:first-of-type": { borderLeft: `3px solid ${toneTokens.border}` },
                          "&.MuiTableRow-hover:hover": { filter: "brightness(0.97)" }
                        }
                      : null)
                  }}
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
            />
          )}
        </Table>
      </TableContainer>
      {showPagination && (
        <TablePagination
          component="div"
          count={rows.length}
          page={isAllPageSize ? 0 : page}
          onPageChange={(_, p) => handlePageChange(p)}
          rowsPerPage={isAllPageSize ? ALL_ROWS_OPTION : rowsPerPage}
          rowsPerPageOptions={DESKTOP_PAGE_SIZE_OPTIONS}
          onRowsPerPageChange={(e) => handleRowsPerPageChange(Number(e.target.value))}
        />
      )}
      {renderMenu()}
    </Paper>
  );
}
