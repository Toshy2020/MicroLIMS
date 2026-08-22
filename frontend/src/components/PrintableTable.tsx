import React from "react";
import { Box, Typography } from "@mui/material";

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
// on-screen table so the printed page always reflects the full dataset
// even while the screen shows a paginated/scrollable view.
export function PrintableTable<T>({ title, subtitle, columns, rows, getRowId }: PrintableTableProps<T>) {
  return (
    <Box
      className="print-only"
      sx={{
        p: 0,
        m: 0,
        width: "100%",
        fontFamily: "'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
        color: "#111827"
      }}
    >
      {/* Report Header */}
      <Box sx={{ mb: 2, pb: 1.5, borderBottom: "2px solid #374151" }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 0.5 }}>
          <Typography sx={{ fontSize: "16pt", fontWeight: 800, color: "#111827", letterSpacing: "-0.5px" }}>
            MicroLIMS
          </Typography>
          <Typography sx={{ fontSize: "9pt", color: "#6b7280" }}>
            Printed: {new Date().toLocaleString()}
          </Typography>
        </Box>
        <Typography sx={{ fontSize: "13pt", fontWeight: 700, color: "#1f2937", mb: 0.25 }}>
          {title}
        </Typography>
        {subtitle && (
          <Typography sx={{ fontSize: "9.5pt", color: "#4b5563" }}>
            {subtitle}
          </Typography>
        )}
        <Typography sx={{ fontSize: "8.5pt", color: "#6b7280", mt: 0.5 }}>
          Total Records: {rows.length}
        </Typography>
      </Box>

      {/* Full-width table */}
      <table
        style={{
          width: "100%",
          borderCollapse: "collapse",
          fontSize: "8.5pt",
          textAlign: "left"
        }}
      >
        <thead>
          <tr style={{ backgroundColor: "#f3f4f6" }}>
            {columns.map((c) => (
              <th
                key={c.label}
                style={{
                  border: "1px solid #d1d5db",
                  padding: "5px 7px",
                  fontWeight: 700,
                  color: "#111827",
                  whiteSpace: "nowrap"
                }}
              >
                {c.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={getRowId(row)} style={{ pageBreakInside: "avoid", breakInside: "avoid" }}>
              {columns.map((c) => (
                <td
                  key={c.label}
                  style={{
                    border: "1px solid #e5e7eb",
                    padding: "4px 7px",
                    color: "#1f2937",
                    verticalAlign: "top"
                  }}
                >
                  {c.render(row)}
                </td>
              ))}
            </tr>
          ))}
          {rows.length === 0 && (
            <tr>
              <td
                colSpan={columns.length}
                style={{
                  border: "1px solid #e5e7eb",
                  padding: "16px",
                  textAlign: "center",
                  color: "#6b7280"
                }}
              >
                No records available for print.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </Box>
  );
}

