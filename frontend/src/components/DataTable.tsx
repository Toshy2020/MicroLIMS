import { Box, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, Checkbox, Skeleton } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { tableHeadSx } from "../theme";
import { EmptyState } from "./lab/EmptyState";

export interface Column<T> {
  key: keyof T;
  label: string;
  render?: (row: T) => React.ReactNode;
  align?: "left" | "right" | "center";
}

// Adds a leading checkbox column. The header checkbox reflects
// headerChecked/headerIndeterminate - callers compute those from whatever
// "visible" set makes sense for them (e.g. only Approved rows).
export interface DataTableSelection<T> {
  isSelected: (row: T) => boolean;
  onToggle: (row: T) => void;
  // Rows that fail this render a disabled checkbox instead of being hidden.
  isSelectable?: (row: T) => boolean;
  headerChecked: boolean;
  headerIndeterminate: boolean;
  onToggleAll: () => void;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[];
  getRowId: (row: T) => string | number;
  onRowClick?: (row: T) => void;
  selection?: DataTableSelection<T>;
  // Renders skeleton rows instead of `rows`.
  loading?: boolean;
  // Renders a single spanning row with this content when `rows` is empty
  // (and not loading). Omit to render an empty body, as before.
  emptyMessage?: React.ReactNode;
}

// Reusable table used across every module (Testing Workspace, Review,
// Approval, Reports) so behavior stays consistent everywhere.
// Above this many rows the body scrolls inside a bounded region so the
// column headers stay visible (same rule as RegisterTable).
const STICKY_HEADER_MIN_ROWS = 15;

export function DataTable<T>({ columns, rows, getRowId, onRowClick, selection, loading, emptyMessage }: DataTableProps<T>) {
  const theme = useTheme();
  const colSpan = columns.length + (selection ? 1 : 0);
  const bounded = !loading && rows.length > STICKY_HEADER_MIN_ROWS;

  return (
    <TableContainer
      component={Paper}
      elevation={0}
      sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, ...(bounded ? { maxHeight: "calc(100vh - 240px)", minHeight: 320 } : null) }}
    >
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow sx={tableHeadSx(theme)}>
            {selection && (
              <TableCell padding="checkbox">
                <Checkbox
                  size="small"
                  checked={selection.headerChecked}
                  indeterminate={selection.headerIndeterminate}
                  onChange={selection.onToggleAll}
                  slotProps={{
                    input: { "aria-label": "Select all rows" }
                  }}
                />
              </TableCell>
            )}
            {columns.map((col) => (
              <TableCell key={String(col.key)} align={col.align}>{col.label}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {loading ? (
            Array.from({ length: rows.length > 0 ? Math.min(rows.length, 5) : 5 }).map((_, rIdx) => (
              <TableRow key={`loading-row-${rIdx}`}>
                {selection && (
                  <TableCell padding="checkbox">
                    <Skeleton variant="rounded" width={18} height={18} />
                  </TableCell>
                )}
                {columns.map((col, cIdx) => (
                  <TableCell key={`loading-cell-${rIdx}-${cIdx}`} align={col.align} sx={{ py: 1.5 }}>
                    <Skeleton variant="text" width={cIdx === 0 ? "75%" : "50%"} height={20} />
                  </TableCell>
                ))}
              </TableRow>
            ))
          ) : rows.length === 0 && emptyMessage ? (
            <TableRow>
              <TableCell colSpan={colSpan} sx={{ p: 0, borderBottom: "none" }}>
                {typeof emptyMessage === "string" ? <EmptyState title={emptyMessage} /> : (
                  <Box sx={{ py: 4, px: 2, textAlign: "center", color: "text.secondary" }}>{emptyMessage}</Box>
                )}
              </TableCell>
            </TableRow>
          ) : (
            rows.map((row) => {
              const isSelected = selection?.isSelected(row) ?? false;
              return (
                <TableRow
                  key={getRowId(row)}
                  hover={!!onRowClick}
                  selected={isSelected}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  // Clickable rows are reachable and operable from the keyboard too.
                  tabIndex={onRowClick ? 0 : undefined}
                  onKeyDown={onRowClick ? (e) => {
                    if (e.target === e.currentTarget && (e.key === "Enter" || e.key === " ")) {
                      e.preventDefault();
                      onRowClick(row);
                    }
                  } : undefined}
                  sx={onRowClick ? { cursor: "pointer", "&:focus-visible": { outlineOffset: -2 } } : undefined}
                >
                  {selection && (
                    <TableCell padding="checkbox" onClick={(e) => e.stopPropagation()}>
                      <Checkbox
                        size="small"
                        checked={isSelected}
                        disabled={selection.isSelectable ? !selection.isSelectable(row) : false}
                        onChange={() => selection.onToggle(row)}
                        slotProps={{
                          input: { "aria-label": `Select row ${getRowId(row)}` }
                        }}
                      />
                    </TableCell>
                  )}
                  {columns.map((col) => (
                    <TableCell key={String(col.key)} align={col.align} sx={{ py: 0.75 }}>
                      {col.render ? col.render(row) : String(row[col.key])}
                    </TableCell>
                  ))}
                </TableRow>
              );
            })
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
