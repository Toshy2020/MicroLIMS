import React from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Button,
  Tooltip,
  Typography,
  Box,
  useTheme
} from "@mui/material";
import AutorenewIcon from "@mui/icons-material/Autorenew";
import { tableHeadSx } from "../../../theme";
import { Ltr } from "../../../components/Ltr";
import { StatusBadge } from "../../../components/StatusBadge";
import { useTranslation } from "../../../i18n";
import { formatLabCalendarDate } from "../../../utils/formatDate";
import type { WorkingStandardLotDto } from "../types";

interface WorkingStandardLotsTableProps {
  lots: WorkingStandardLotDto[];
  canQualify: boolean;
  onRequalify: (lot: WorkingStandardLotDto) => void;
}

export const WorkingStandardLotsTable: React.FC<WorkingStandardLotsTableProps> = ({
  lots,
  canQualify,
  onRequalify
}) => {
  const theme = useTheme();
  const { t } = useTranslation("workingStandards");

  if (lots.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: "center" }}>
        <Typography color="text.secondary">{t("lotsTable.empty")}</Typography>
      </Paper>
    );
  }

  return (
    <TableContainer component={Paper} sx={{ borderRadius: 2 }}>
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700 }}>{t("lotsTable.columns.code")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("lotsTable.columns.material")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("lotsTable.columns.batch")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">{t("lotsTable.columns.potency")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">{t("lotsTable.columns.moisture")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">{t("lotsTable.columns.remaining")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("lotsTable.columns.expiry")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("lotsTable.columns.status")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="center">{t("lotsTable.columns.actions")}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {lots.map((lot) => {
            const hasOpenQual = Boolean(lot.openQualificationId);
            const isRequalifyDisabled = !canQualify || hasOpenQual;
            const tooltipMessage = !canQualify
              ? t("actions.permissionRequired", { permission: "WorkingStandards.Qualify" })
              : hasOpenQual
                ? t("lotsTable.requalifyInProgress", { id: lot.openQualificationId })
                : t("lotsTable.initiateRequalification");

            return (
              <TableRow key={lot.materialId} hover>
                <TableCell>
                  <Typography sx={{ fontFamily: "monospace", fontWeight: 600, fontSize: 13 }}>
                    <Ltr>{lot.code}</Ltr>
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    <Ltr>{lot.masterEntryCode}</Ltr>
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography sx={{ fontSize: 13, fontWeight: 500 }}>
                    {lot.materialName}
                  </Typography>
                </TableCell>
                <TableCell sx={{ fontSize: 13 }}><Ltr>{lot.batchNumber}</Ltr></TableCell>
                <TableCell align="right" sx={{ fontSize: 13 }}>
                  {lot.potencyPercent != null ? <Ltr>{t("units.percent", { value: lot.potencyPercent.toFixed(2) })}</Ltr> : "—"}
                </TableCell>
                <TableCell align="right" sx={{ fontSize: 13 }}>
                  {lot.moisturePercent != null ? <Ltr>{t("units.percent", { value: lot.moisturePercent.toFixed(2) })}</Ltr> : "—"}
                </TableCell>
                <TableCell align="right" sx={{ fontSize: 13 }}>
                  {lot.quantityRemaining != null ? <Ltr>{t("units.grams", { value: lot.quantityRemaining })}</Ltr> : "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 13 }}>
                  {lot.expiryDate ? <Ltr>{formatLabCalendarDate(lot.expiryDate)}</Ltr> : "—"}
                </TableCell>
                <TableCell>
                  <StatusBadge
                    status={
                      lot.status === "DueSoon"
                        ? "Due Soon"
                        : lot.status === "Depleted"
                          ? "Expired"
                          : lot.status
                    }
                    label={t(`lotStatuses.${lot.status}`, { defaultValue: lot.status })}
                  />
                </TableCell>
                <TableCell align="center">
                  <Tooltip title={tooltipMessage}>
                    <Box component="span">
                      <Button
                        size="small"
                        variant="outlined"
                        startIcon={<AutorenewIcon fontSize="small" />}
                        disabled={isRequalifyDisabled}
                        onClick={() => onRequalify(lot)}
                        sx={{ fontSize: 12, py: 0.5, px: 1 }}
                      >
                        {t("actions.requalify")}
                      </Button>
                    </Box>
                  </Tooltip>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </TableContainer>
  );
};
