import type { ReactNode } from "react";
import { Skeleton, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import { tableHeadSx } from "../../theme";
import { EmptyState } from "./EmptyState";

export interface TrendColumn<T> {
  key: string;
  label: string;
  // Right-aligned with tabular figures.
  numeric?: boolean;
  render: (row: T) => ReactNode;
}

interface TrendTableProps<T> {
  columns: TrendColumn<T>[];
  rows: T[];
  getRowId: (row: T) => string | number;
  loading?: boolean;
  emptyTitle: string;
  // Names the table for assistive technology.
  label: string;
}

// A small read-only table for server-grouped report rows: every row is in the order the server sent, no paging,
// no export (trend exports are not offered by the API).
export function TrendTable<T>({ columns, rows, getRowId, loading, emptyTitle, label }: TrendTableProps<T>) {
  if (!loading && rows.length === 0) return <EmptyState title={emptyTitle} />;
  return (
    <TableContainer sx={{ border: 1, borderColor: "divider", borderRadius: 1 }}>
      <Table size="small" aria-label={label}>
        <TableHead sx={tableHeadSx}>
          <TableRow>
            {columns.map((c) => (
              <TableCell key={c.key} align={c.numeric ? "right" : "left"}>
                {c.label}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {loading
            ? [0, 1, 2].map((i) => (
                <TableRow key={i}>
                  <TableCell colSpan={columns.length}>
                    <Skeleton variant="text" />
                  </TableCell>
                </TableRow>
              ))
            : rows.map((row) => (
                <TableRow key={getRowId(row)} hover>
                  {columns.map((c) => (
                    <TableCell key={c.key} align={c.numeric ? "right" : "left"} sx={c.numeric ? { fontVariantNumeric: "tabular-nums" } : undefined}>
                      {c.render(row)}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
