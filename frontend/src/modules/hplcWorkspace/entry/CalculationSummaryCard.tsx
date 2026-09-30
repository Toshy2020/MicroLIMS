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
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { HplcPreviewResultDto, HplcOfficialResultDto } from "../types";

export interface OfficialResultsCardProps {
  official: HplcOfficialResultDto[];
  basis?: string;
}

export function OfficialResultsCard({
  official,
  basis
}: OfficialResultsCardProps) {
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
              Official Results
            </Typography>
            {basis && (
              <Chip
                icon={<ScienceOutlinedIcon fontSize="small" />}
                label={`Basis: ${basis}`}
                size="small"
                color="primary"
                variant="outlined"
              />
            )}
            <Chip
              icon={<CheckCircleOutlinedIcon fontSize="small" />}
              label="Official — Recorded for Review"
              size="small"
              color="success"
              variant="filled"
              sx={{ fontWeight: 700 }}
            />
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            These are the recorded results sent for review.
          </Typography>
        </Box>
      </Box>

      {official.length === 0 ? (
        <Box sx={{ p: 3 }}>
          <Alert severity="info">
            No official results recorded yet.
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
                <TableCell>Result</TableCell>
                <TableCell>Spec Limit</TableCell>
                <TableCell>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {official.map((o, idx) => {
                const isReported = o.replicateNo === null || o.replicateNo === undefined;
                const quantityLabel =
                  o.quantity === "AssayPercent"
                    ? "% Assay"
                    : o.quantity === "AmountPerUnit"
                    ? "Amount per Unit"
                    : o.quantity;

                return (
                  <TableRow
                    key={`${o.parameterName}-${o.quantity}-${o.replicateNo ?? "rep"}-${idx}`}
                    hover
                    sx={{
                      backgroundColor: isReported ? theme.palette.action.hover : "inherit"
                    }}
                  >
                    <TableCell sx={{ fontWeight: isReported ? 700 : 500 }}>
                      {o.parameterName}
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
                        `Replicate #${o.replicateNo}`
                      )}
                    </TableCell>
                    <TableCell sx={{ fontWeight: 700, fontFamily: "monospace" }}>
                      {o.display}
                    </TableCell>
                    <TableCell>{o.specLimit ?? "—"}</TableCell>
                    <TableCell>
                      <HplcStatusBadge status={o.status} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Box sx={{ p: 1.5, backgroundColor: theme.palette.action.hover, borderTop: `1px solid ${theme.palette.divider}` }}>
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
          * These are the recorded results sent for review.
        </Typography>
      </Box>
    </Paper>
  );
}

export interface CalculationSummaryCardProps {
  preview: HplcPreviewResultDto[];
  basis: string;
  official?: HplcOfficialResultDto[];
  submitted?: boolean;
}

export function CalculationSummaryCard({
  preview,
  basis,
  official,
  submitted
}: CalculationSummaryCardProps) {
  const theme = useTheme();

  if (submitted) {
    return <OfficialResultsCard official={official || []} basis={basis} />;
  }

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
