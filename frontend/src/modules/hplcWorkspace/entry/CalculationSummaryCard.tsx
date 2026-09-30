import {
  Box,
  Paper,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Alert,
  useTheme
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { HplcPreviewResultDto } from "../types";

export interface CalculationSummaryCardProps {
  preview: HplcPreviewResultDto[];
  basis: string;
}

export function CalculationSummaryCard({
  preview,
  basis
}: CalculationSummaryCardProps) {
  const theme = useTheme();

  return (
    <Paper
      elevation={0}
      sx={{
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        overflow: "hidden"
      }}
    >
      <Box
        sx={{
          p: 2,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1.5,
          borderBottom: `1px solid ${theme.palette.divider}`
        }}
      >
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              Calculation Summary
            </Typography>
            <Chip
              icon={<ScienceOutlinedIcon fontSize="small" />}
              label={`Basis: ${basis}`}
              size="small"
              color="primary"
              variant="outlined"
            />
            <Chip
              icon={<VisibilityOutlinedIcon fontSize="small" />}
              label="Preview — not the official result"
              size="small"
              color="warning"
              variant="filled"
              sx={{ fontWeight: 700 }}
            />
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Calculations are computed server-side from current SST calibration and replicate inputs.
          </Typography>
        </Box>
      </Box>

      {preview.length === 0 ? (
        <Box sx={{ p: 3 }}>
          <Alert severity="info">
            No preview results available yet. Enter actual weights and responses above and click <strong>Save Replicates</strong> to generate the preview calculation.
          </Alert>
        </Box>
      ) : (
        <TableContainer>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>Parameter</TableCell>
                <TableCell>Quantity</TableCell>
                <TableCell>Replicate / Level</TableCell>
                <TableCell>Value (Display)</TableCell>
                <TableCell>Unit</TableCell>
                <TableCell>Spec Limit</TableCell>
                <TableCell>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {preview.map((p, idx) => {
                const isReported = p.replicateNo === null || p.replicateNo === undefined;
                const quantityLabel =
                  p.quantity === "AssayPercent"
                    ? "% Assay"
                    : p.quantity === "AmountPerUnit"
                    ? "Amount per Unit"
                    : p.quantity;

                return (
                  <TableRow
                    key={`${p.hplcMethodAnalyteId}-${p.quantity}-${p.replicateNo ?? "rep"}-${idx}`}
                    hover
                    sx={{
                      backgroundColor: isReported ? theme.palette.action.hover : "inherit"
                    }}
                  >
                    <TableCell sx={{ fontWeight: isReported ? 700 : 500 }}>
                      {p.parameterName}
                    </TableCell>
                    <TableCell>{quantityLabel}</TableCell>
                    <TableCell>
                      {isReported ? (
                        <Chip
                          label="Reported Result"
                          size="small"
                          color="info"
                          variant="outlined"
                        />
                      ) : (
                        `Replicate #${p.replicateNo}`
                      )}
                    </TableCell>
                    <TableCell sx={{ fontWeight: 700, fontFamily: "monospace" }}>
                      {p.display}
                    </TableCell>
                    <TableCell>{p.unit || "—"}</TableCell>
                    <TableCell>{p.specLimit ?? "—"}</TableCell>
                    <TableCell>
                      <HplcStatusBadge status={p.status} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {preview.length > 0 && (
        <Box sx={{ p: 1.5, backgroundColor: theme.palette.action.hover, borderTop: `1px solid ${theme.palette.divider}` }}>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            * Preview results are provisional. Official results are finalized when signed and submitted for review.
          </Typography>
        </Box>
      )}
    </Paper>
  );
}
