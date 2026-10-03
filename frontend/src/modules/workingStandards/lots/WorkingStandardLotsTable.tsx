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
import { StatusBadge } from "../../../components/StatusBadge";
import { formatLabDate } from "../../../utils/formatDate";
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

  if (lots.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: "center" }}>
        <Typography color="text.secondary">No working standard lots found.</Typography>
      </Paper>
    );
  }

  return (
    <TableContainer component={Paper} sx={{ borderRadius: 2 }}>
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700 }}>Code</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Material</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Batch</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">Potency %</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">MC %</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="right">Remaining (g)</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Expiry</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
            <TableCell sx={{ fontWeight: 700 }} align="center">Actions</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {lots.map((lot) => {
            const hasOpenQual = Boolean(lot.openQualificationId);
            const isRequalifyDisabled = !canQualify || hasOpenQual;
            const tooltipMessage = !canQualify
              ? "Permission required: WorkingStandards.Qualify"
              : hasOpenQual
                ? `Qualification already in progress (#${lot.openQualificationId})`
                : "Initiate requalification";

            return (
              <TableRow key={lot.materialId} hover>
                <TableCell>
                  <Typography sx={{ fontFamily: "monospace", fontWeight: 600, fontSize: 13 }}>
                    {lot.code}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {lot.masterEntryCode}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography sx={{ fontSize: 13, fontWeight: 500 }}>
                    {lot.materialName}
                  </Typography>
                </TableCell>
                <TableCell sx={{ fontSize: 13 }}>{lot.batchNumber}</TableCell>
                <TableCell align="right" sx={{ fontSize: 13 }}>
                  {lot.potencyPercent != null ? `${lot.potencyPercent.toFixed(2)} %` : "—"}
                </TableCell>
                <TableCell align="right" sx={{ fontSize: 13 }}>
                  {lot.moisturePercent != null ? `${lot.moisturePercent.toFixed(2)} %` : "—"}
                </TableCell>
                <TableCell align="right" sx={{ fontSize: 13 }}>
                  {lot.quantityRemaining != null ? `${lot.quantityRemaining} g` : "—"}
                </TableCell>
                <TableCell sx={{ fontSize: 13 }}>
                  {lot.expiryDate ? formatLabDate(lot.expiryDate) : "—"}
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
                    label={lot.status === "Depleted" ? "Depleted" : undefined}
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
                        Requalify
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
