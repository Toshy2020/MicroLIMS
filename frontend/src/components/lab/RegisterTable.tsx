import { useEffect, useMemo, useState } from "react";
import type { KeyboardEvent, MouseEvent, ReactNode } from "react";
import {
  Box, IconButton, ListItemText, Menu, MenuItem, Paper, Select, Skeleton, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TableSortLabel, Typography, useMediaQuery, useTheme
} from "@mui/material";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import { tableHeadSx } from "../../theme";
import type { StatusTone } from "../../theme/statusTokens";
import { EmptyState } from "./EmptyState";

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
  pageSize?: 25 | 50 | 100;
  dense?: boolean;
  // Sort applied before the user clicks a header.
  defaultSort?: { key: string; direction: "asc" | "desc" };
  // Tints the row (and marks its first cell with a left border) so GMP warnings
  // such as low stock or overdue calibration stay visible at a glance.
  rowTone?: (row: T) => StatusTone | undefined;
}

const PAGE_SIZES = [25, 50, 100];
const SKELETON_ROWS = 5;
// Above this many rows on a page the table body scrolls inside a bounded
// region on desktop, so the column headers stay in view. Shorter pages keep
// the page itself as the only scroll container (no second scrollbar).
const STICKY_HEADER_MIN_ROWS = 15;

function compareValues(a: unknown, b: unknown): number {
  // Empty values sort last in ascending order.
  const aEmpty = a === null || a === undefined || a === "";
  const bEmpty = b === null || b === undefined || b === "";
  if (aEmpty || bEmpty) return aEmpty === bEmpty ? 0 : aEmpty ? 1 : -1;
  if (typeof a === "number" && typeof b === "number") return a - b;
  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: "base" });
}

export function RegisterTable<T>({
  columns, rows, getRowId, onRowClick, rowActions, loading, empty, pageSize = 25, dense = true, defaultSort, rowTone
}: RegisterTableProps<T>) {
  const theme = useTheme();
  // Phones get one card per record instead of a table whose columns would be
  // cut off or need sideways scrolling to read a single record.
  const isPhone = useMediaQuery(theme.breakpoints.down("sm"));
  const [sort, setSort] = useState<{ key: string; direction: "asc" | "desc" } | null>(defaultSort ?? null);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState<number>(pageSize);
  const [menu, setMenu] = useState<{ anchor: HTMLElement; row: T } | null>(null);

  // Filtering or reloading changes the row set; a stale page could be past the end.
  useEffect(() => { setPage(0); }, [rows.length]);

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

  const paged = sorted.length > PAGE_SIZES[0]
    ? sorted.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage)
    : sorted;
  const showPagination = !loading && rows.length > PAGE_SIZES[0];
  const colSpan = columns.length + (rowActions ? 1 : 0);
  const cellPy = dense ? 0.75 : 1.5;

  const toggleSort = (key: string) =>
    setSort((s) => (s?.key === key ? (s.direction === "asc" ? { key, direction: "desc" } : null) : { key, direction: "asc" }));

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
  const sortableColumns = columns.filter((c) => c.sortable);
  const boundedBody = !isPhone && paged.length > STICKY_HEADER_MIN_ROWS;

  if (isPhone) {
    const [titleCol, ...detailCols] = columns;
    return (
      <Paper elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
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
                    <Box sx={{ flex: 1, minWidth: 0, fontWeight: 600, fontSize: 15, overflowWrap: "anywhere" }}>
                      {titleCol ? cellText(row, titleCol) : null}
                    </Box>
                    {actions.length > 0 && (
                      <IconButton size="small" aria-label="Row actions" onClick={(e) => openMenu(e, row)} sx={{ mt: -0.5, mr: -0.5 }}>
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
            onPageChange={(_, p) => setPage(p)}
            rowsPerPage={rowsPerPage}
            rowsPerPageOptions={PAGE_SIZES}
            onRowsPerPageChange={(e) => {
              setRowsPerPage(Number(e.target.value));
              setPage(0);
            }}
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
      {/* stickyHeader needs a bounded scroll region to stick within; only long
          pages get one (see STICKY_HEADER_MIN_ROWS). */}
      <TableContainer sx={boundedBody ? { maxHeight: "calc(100vh - 240px)", minHeight: 320 } : undefined}>
        <Table size={dense ? "small" : "medium"} stickyHeader>
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              {columns.map((col) => {
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
          <TableBody>
            {loading ? (
              Array.from({ length: SKELETON_ROWS }).map((_, r) => (
                <TableRow key={`sk-${r}`}>
                  {columns.map((col, c) => (
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
                return (
                <TableRow
                  key={getRowId(row)}
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
                  {columns.map((col) => {
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
                        <IconButton size="small" aria-label="Row actions" onClick={(e) => openMenu(e, row)}>
                          <MoreVertIcon fontSize="small" />
                        </IconButton>
                      )}
                    </TableCell>
                  )}
                </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
      {showPagination && (
        <TablePagination
          component="div"
          count={rows.length}
          page={page}
          onPageChange={(_, p) => setPage(p)}
          rowsPerPage={rowsPerPage}
          rowsPerPageOptions={PAGE_SIZES}
          onRowsPerPageChange={(e) => {
            setRowsPerPage(Number(e.target.value));
            setPage(0);
          }}
        />
      )}
      {renderMenu()}
    </Paper>
  );
}
