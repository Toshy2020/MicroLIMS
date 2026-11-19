import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { CATEGORY_KEY } from "../statusDisplay";
import type { CoverageReport, CoverageRow } from "../types";

const cells = (r: CoverageRow) => [r.planned, r.performed, r.performedLate, r.inProgress, r.overdue, r.upcoming, r.skipped];

export function CoverageTable({ report }: { report: CoverageReport | null }) {
  const { t } = useTranslation("monitoring");
  if (!report) return null;
  if (report.rows.length === 0) {
    return (
      <Typography color="text.secondary">
        <Trans i18nKey="coverage.empty" ns="monitoring" values={{ year: report.year }} components={{ ltr: <Ltr>{null}</Ltr> }} />
      </Typography>
    );
  }
  const numericHeaders = [
    t("coverage.columns.planned"),
    t("coverage.columns.performed"),
    t("coverage.columns.late"),
    t("coverage.columns.inProgress"),
    t("coverage.columns.overdue"),
    t("coverage.columns.upcoming"),
    t("coverage.columns.skipped"),
    t("coverage.columns.coverage")
  ];
  return (
    <TableContainer>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
        <Trans i18nKey="coverage.asOf" ns="monitoring" values={{ date: report.asOf }} components={{ ltr: <Ltr>{null}</Ltr> }} />
      </Typography>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>{t("coverage.columns.area")}</TableCell>
            <TableCell>{t("coverage.columns.group")}</TableCell>
            <TableCell>{t("coverage.columns.target")}</TableCell>
            {numericHeaders.map((h) => (
              <TableCell key={h} align="right">{h}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {[...report.rows, report.total].map((r, i) => {
            const total = i === report.rows.length;
            return (
              <TableRow key={`${r.target}-${i}`} sx={total ? { "& td": { fontWeight: 700 } } : undefined}>
                <TableCell>{total ? "" : t(CATEGORY_KEY[r.category])}</TableCell>
                <TableCell>{r.group ?? ""}</TableCell>
                <TableCell><Ltr>{r.target}</Ltr></TableCell>
                {cells(r).map((n, k) => (
                  <TableCell key={k} align="right" sx={k === 4 && n > 0 ? { color: "error.main" } : undefined}>
                    <Ltr>{n}</Ltr>
                  </TableCell>
                ))}
                <TableCell align="right">
                  {r.coveragePercent === null ? "–" : <Ltr>{`${r.coveragePercent}%`}</Ltr>}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
