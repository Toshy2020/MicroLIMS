import { Box, Typography, Table, TableHead, TableRow, TableCell, TableBody } from "@mui/material";

export interface PrintColumn<T> {
  label: string;
  render: (row: T) => React.ReactNode;
}

interface PrintableTableProps<T> {
  title: string;
  subtitle?: string;
  columns: PrintColumn<T>[];
  rows: T[];
  getRowId: (row: T) => string | number;
}

// Invisible on screen (see .print-only in index.html), rendered only
// inside the browser print dialog. Kept separate from the interactive
// on-screen table so the printed page always reflects the filtered
// "for print" data (expired / out-of-stock / retired rows excluded)
// even while the screen shows the full editable list.
export function PrintableTable<T>({ title, subtitle, columns, rows, getRowId }: PrintableTableProps<T>) {
  return (
    <Box className="print-only" sx={{ p: 2 }}>
      <Typography sx={{ fontSize: 18, fontWeight: 700, mb: 0.25 }}>{title}</Typography>
      {subtitle && <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>{subtitle}</Typography>}
      <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 1.5 }}>Printed {new Date().toLocaleString()}</Typography>
      <Table size="small">
        <TableHead>
          <TableRow>{columns.map((c) => <TableCell key={c.label} sx={{ fontWeight: 700 }}>{c.label}</TableCell>)}</TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={getRowId(row)}>
              {columns.map((c) => <TableCell key={c.label}>{c.render(row)}</TableCell>)}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}
