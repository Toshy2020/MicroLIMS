import {
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer,
  Paper,
  Box,
  useTheme
} from "@mui/material";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import { PreparationStatusBadge } from "../components/PreparationStatusBadge";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import type { SolutionPreparationStatusHistoryResponse } from "../types";

interface Props {
  history: SolutionPreparationStatusHistoryResponse[];
}

export function PreparationHistoryTable({ history }: Props) {
  const { t } = useTranslation("solutionPreparation");
  const theme = useTheme();

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700 }}>{t("historyTable.columns.transition")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("historyTable.columns.changedBy")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("historyTable.columns.changedAt")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("historyTable.columns.reason")}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {history.map((h, i) => (
            <TableRow key={i}>
              <TableCell>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  {h.fromStatus && <PreparationStatusBadge status={h.fromStatus} />}
                  {h.fromStatus && <span>→</span>}
                  <PreparationStatusBadge status={h.toStatus} />
                </Box>
              </TableCell>
              <TableCell>{h.changedByUserName || t("historyTable.automaticSystem")}</TableCell>
              <TableCell><Ltr>{formatDateTimeWithTimeZone(h.changedAt)}</Ltr></TableCell>
              <TableCell>{h.reason || "—"}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
