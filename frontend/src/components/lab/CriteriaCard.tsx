import { Paper, Table, TableBody, TableCell, TableHead, TableRow, Typography, useTheme, TableContainer } from "@mui/material";
import { useTranslation } from "../../i18n";
import { tableHeadSx } from "../../theme";

export interface CriteriaRow {
  parameter: string;
  criterion: string;
  unit?: string;
  // Where the limit comes from (monograph, method, specification).
  source?: string;
}

// Acceptance criteria stay visible above result entry so the analyst sees
// the governing limit before typing a value.
export function CriteriaCard({ rows }: { rows: CriteriaRow[] }) {
  const { t } = useTranslation();
  const theme = useTheme();
  return (
    <Paper variant="outlined" sx={{ overflow: "hidden" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, px: 1.5, py: 1 }}>{t("components.criteriaCard.title")}</Typography>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              <TableCell>{t("components.criteriaCard.parameter")}</TableCell>
              <TableCell>{t("components.criteriaCard.criterion")}</TableCell>
              <TableCell>{t("components.criteriaCard.unit")}</TableCell>
              <TableCell>{t("components.criteriaCard.source")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r, i) => (
              <TableRow key={`${r.parameter}-${i}`}>
                <TableCell>{r.parameter}</TableCell>
                <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>{r.criterion}</TableCell>
                <TableCell sx={{ color: "text.secondary" }}>{r.unit ?? "—"}</TableCell>
                <TableCell sx={{ color: "text.secondary" }}>{r.source ?? "—"}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}
