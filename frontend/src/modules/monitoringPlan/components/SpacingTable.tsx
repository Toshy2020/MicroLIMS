import { Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { CATEGORY_KEY } from "../statusDisplay";
import type { SpacingReport } from "../types";

export function SpacingTable({ report }: { report: SpacingReport | null }) {
  const { t } = useTranslation("monitoring");
  if (!report) return null;
  if (report.rows.length === 0) {
    return (
      <Typography color="text.secondary">
        <Trans i18nKey="spacing.empty" ns="monitoring" values={{ year: report.year }} components={{ ltr: <Ltr>{null}</Ltr> }} />
      </Typography>
    );
  }
  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>{t("spacing.columns.area")}</TableCell>
            <TableCell>{t("spacing.columns.group")}</TableCell>
            <TableCell>{t("spacing.columns.target")}</TableCell>
            <TableCell>{t("spacing.columns.frequency")}</TableCell>
            <TableCell>{t("spacing.columns.from")}</TableCell>
            <TableCell>{t("spacing.columns.to")}</TableCell>
            <TableCell align="right">{t("spacing.columns.gapDays")}</TableCell>
            <TableCell align="right">{t("spacing.columns.allowed")}</TableCell>
            <TableCell />
          </TableRow>
        </TableHead>
        <TableBody>
          {report.rows.map((r, i) => (
            <TableRow key={i}>
              <TableCell>{t(CATEGORY_KEY[r.category])}</TableCell>
              <TableCell>{r.group ?? ""}</TableCell>
              <TableCell><Ltr>{r.target}</Ltr></TableCell>
              <TableCell>{r.frequency}</TableCell>
              <TableCell><Ltr>{r.previousOn}</Ltr></TableCell>
              <TableCell><Ltr>{r.nextOn}</Ltr></TableCell>
              <TableCell align="right"><Ltr>{r.gapDays}</Ltr></TableCell>
              <TableCell align="right"><Ltr>{`${r.minDays}–${r.maxDays}`}</Ltr></TableCell>
              <TableCell>
                {r.flagged && (
                  <Chip
                    size="small"
                    color="warning"
                    label={r.gapDays < r.minDays ? t("spacing.tooClose") : t("spacing.tooFarApart")}
                  />
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
