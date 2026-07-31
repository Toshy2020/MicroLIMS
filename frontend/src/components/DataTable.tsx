import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper } from "@mui/material";

export interface Column<T> {
  key: keyof T;
  label: string;
  render?: (row: T) => React.ReactNode;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[];
  getRowId: (row: T) => string | number;
}

// Reusable table used across every module (Testing Workspace, Review,
// Approval, Reports) so behavior stays consistent everywhere.
export function DataTable<T>({ columns, rows, getRowId }: DataTableProps<T>) {
  return (
    <TableContainer component={Paper}>
      <Table>
        <TableHead>
          <TableRow>
            {columns.map((col) => <TableCell key={String(col.key)}>{col.label}</TableCell>)}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={getRowId(row)}>
              {columns.map((col) => (
                <TableCell key={String(col.key)}>
                  {col.render ? col.render(row) : String(row[col.key])}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
