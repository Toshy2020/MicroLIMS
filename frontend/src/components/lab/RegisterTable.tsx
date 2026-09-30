import { useEffect, useMemo, useState } from "react";
import type { KeyboardEvent, MouseEvent, ReactNode } from "react";
import {
  IconButton, ListItemText, Menu, MenuItem, Paper, Skeleton, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TableSortLabel, useTheme
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

  return (
    <Paper elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
      <TableContainer sx={{ maxHeight: "calc(100vh - 260px)" }}>
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
                    const content = col.render ? col.render(row) : String((row as Record<string, unknown>)[String(col.key)] ?? "—");
                    return (
                      <TableCell
                        key={String(col.key)}
                        align={col.align ?? (col.numeric ? "right" : "left")}
                        sx={{ py: cellPy, ...(col.numeric ? { fontVariantNumeric: "tabular-nums" } : null) }}
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
    </Paper>
  );
}
