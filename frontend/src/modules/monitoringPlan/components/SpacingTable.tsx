import { Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { CATEGORY_LABEL } from "../statusDisplay";
import type { SpacingReport } from "../types";

export function SpacingTable({ report }: { report: SpacingReport | null }) {
  if (!report) return null;
  if (report.rows.length === 0) return <Typography color="text.secondary">No gaps to show for {report.year}.</Typography>;
  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Area</TableCell><TableCell>Group</TableCell><TableCell>Target</TableCell><TableCell>Frequency</TableCell>
            <TableCell>From</TableCell><TableCell>To</TableCell><TableCell align="right">Gap (days)</TableCell>
            <TableCell align="right">Allowed</TableCell><TableCell />
          </TableRow>
        </TableHead>
        <TableBody>
          {report.rows.map((r, i) => (
            <TableRow key={i}>
              <TableCell>{CATEGORY_LABEL[r.category]}</TableCell>
              <TableCell>{r.group ?? ""}</TableCell>
              <TableCell>{r.target}</TableCell>
              <TableCell>{r.frequency}</TableCell>
              <TableCell>{r.previousOn}</TableCell>
              <TableCell>{r.nextOn}</TableCell>
              <TableCell align="right">{r.gapDays}</TableCell>
              <TableCell align="right">{r.minDays}–{r.maxDays}</TableCell>
              <TableCell>{r.flagged && <Chip size="small" color="warning" label={r.gapDays < r.minDays ? "Too close" : "Too far apart"} />}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
