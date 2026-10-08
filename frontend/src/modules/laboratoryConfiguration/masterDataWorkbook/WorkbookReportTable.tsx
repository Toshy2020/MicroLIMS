import { Table, TableBody, TableCell, TableHead, TableRow, Typography, Stack } from "@mui/material";
import type { WorkbookReport } from "./types";

// Result of a dry run or a load: one count row per sheet, then every
// error with the sheet, row and column the lab must fix.
export function WorkbookReportTable({ report }: { report: WorkbookReport }) {
  return (
    <Stack spacing={2}>
      <Table size="small" aria-label="Rows per sheet">
        <TableHead>
          <TableRow><TableCell>Sheet</TableCell><TableCell align="right">New</TableCell><TableCell align="right">Unchanged</TableCell><TableCell align="right">Errors</TableCell></TableRow>
        </TableHead>
        <TableBody>
          {report.sheets.map((s) => (
            <TableRow key={s.sheet}>
              <TableCell>{s.sheet}</TableCell>
              <TableCell align="right">{s.created}</TableCell>
              <TableCell align="right">{s.unchanged}</TableCell>
              <TableCell align="right">{s.errors}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {report.errors.length > 0 && (
        <>
          <Typography variant="subtitle2" color="error">
            {report.errors.length} problem{report.errors.length === 1 ? "" : "s"} to fix in the workbook
          </Typography>
          <Table size="small" aria-label="Problems to fix">
            <TableHead>
              <TableRow><TableCell>Sheet</TableCell><TableCell>Row</TableCell><TableCell>Column</TableCell><TableCell>Problem</TableCell></TableRow>
            </TableHead>
            <TableBody>
              {report.errors.map((e, i) => (
                <TableRow key={`${e.sheet}-${e.row}-${i}`}>
                  <TableCell>{e.sheet}</TableCell>
                  <TableCell>{e.row}</TableCell>
                  <TableCell>{e.column ?? "—"}</TableCell>
                  <TableCell>{e.message}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </>
      )}
    </Stack>
  );
}
