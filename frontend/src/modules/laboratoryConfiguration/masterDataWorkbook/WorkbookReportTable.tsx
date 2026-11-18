import { Table, TableBody, TableCell, TableHead, TableRow, Typography, Stack } from "@mui/material";
import { useTranslation } from "../../../i18n";
import type { WorkbookReport } from "./types";

// Result of a dry run or a load: one count row per sheet, then every
// error with the sheet, row and column the lab must fix.
export function WorkbookReportTable({ report }: { report: WorkbookReport }) {
  const { t } = useTranslation("labConfig");
  return (
    <Stack spacing={2}>
      <Table size="small" aria-label={t("masterDataWorkbook.report.rowsPerSheet")}>
        <TableHead>
          <TableRow><TableCell>{t("masterDataWorkbook.report.sheet")}</TableCell><TableCell align="right">{t("masterDataWorkbook.report.new")}</TableCell><TableCell align="right">{t("masterDataWorkbook.report.unchanged")}</TableCell><TableCell align="right">{t("masterDataWorkbook.report.errors")}</TableCell></TableRow>
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
            {t("masterDataWorkbook.report.problems", { count: report.errors.length })}
          </Typography>
          <Table size="small" aria-label={t("masterDataWorkbook.report.problemsToFix")}>
            <TableHead>
              <TableRow><TableCell>{t("masterDataWorkbook.report.sheet")}</TableCell><TableCell>{t("masterDataWorkbook.report.row")}</TableCell><TableCell>{t("masterDataWorkbook.report.column")}</TableCell><TableCell>{t("masterDataWorkbook.report.problem")}</TableCell></TableRow>
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
