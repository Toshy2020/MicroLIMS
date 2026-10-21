import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { CATEGORY_LABEL } from "../statusDisplay";
import type { CoverageReport, CoverageRow } from "../types";

const cells = (r: CoverageRow) => [r.planned, r.performed, r.performedLate, r.inProgress, r.overdue, r.upcoming, r.skipped];

export function CoverageTable({ report }: { report: CoverageReport | null }) {
  if (!report) return null;
  if (report.rows.length === 0) return <Typography color="text.secondary">Nothing planned for {report.year}.</Typography>;
  return (
    <TableContainer>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
        As of {report.asOf}. Coverage is performed over everything that has fallen due.
      </Typography>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Area</TableCell><TableCell>Group</TableCell><TableCell>Target</TableCell>
            {["Planned", "Performed", "Late", "In progress", "Overdue", "Upcoming", "Skipped", "Coverage"].map((h) => (
              <TableCell key={h} align="right">{h}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {[...report.rows, report.total].map((r, i) => {
            const total = i === report.rows.length;
            return (
              <TableRow key={`${r.target}-${i}`} sx={total ? { "& td": { fontWeight: 700 } } : undefined}>
                <TableCell>{total ? "" : CATEGORY_LABEL[r.category]}</TableCell>
                <TableCell>{r.group ?? ""}</TableCell>
                <TableCell>{r.target}</TableCell>
                {cells(r).map((n, k) => <TableCell key={k} align="right" sx={k === 4 && n > 0 ? { color: "error.main" } : undefined}>{n}</TableCell>)}
                <TableCell align="right">{r.coveragePercent === null ? "–" : `${r.coveragePercent}%`}</TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
