import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, Checkbox, CircularProgress } from "@mui/material";

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
  // Renders a single spanning row with a spinner instead of `rows`.
  loading?: boolean;
  // Renders a single spanning row with this content when `rows` is empty
  // (and not loading). Omit to render an empty body, as before.
  emptyMessage?: React.ReactNode;
}

// Reusable table used across every module (Testing Workspace, Review,
// Approval, Reports) so behavior stays consistent everywhere.
export function DataTable<T>({ columns, rows, getRowId, onRowClick, selection, loading, emptyMessage }: DataTableProps<T>) {
  const colSpan = columns.length + (selection ? 1 : 0);

  return (
    <TableContainer component={Paper}>
      <Table size="small">
        <TableHead>
          <TableRow>
            {selection && (
              <TableCell padding="checkbox">
                <Checkbox
                  size="small"
                  checked={selection.headerChecked}
                  indeterminate={selection.headerIndeterminate}
                  onChange={selection.onToggleAll}
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
            <TableRow>
              <TableCell colSpan={colSpan} align="center" sx={{ py: 4 }}>
                <CircularProgress size={28} />
              </TableCell>
            </TableRow>
          ) : rows.length === 0 && emptyMessage ? (
            <TableRow>
              <TableCell colSpan={colSpan} align="center" sx={{ py: 4 }}>
                {emptyMessage}
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
                  sx={onRowClick ? { cursor: "pointer" } : undefined}
                >
                  {selection && (
                    <TableCell padding="checkbox" onClick={(e) => e.stopPropagation()}>
                      <Checkbox
                        size="small"
                        checked={isSelected}
                        disabled={selection.isSelectable ? !selection.isSelectable(row) : false}
                        onChange={() => selection.onToggle(row)}
                      />
                    </TableCell>
                  )}
                  {columns.map((col) => (
                    <TableCell key={String(col.key)} align={col.align}>
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
