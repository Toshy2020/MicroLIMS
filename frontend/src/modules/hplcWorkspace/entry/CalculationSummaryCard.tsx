import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  useTheme
} from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
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
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1 }}>
        {basis && <StatusBadge status="Prepared" label={`Basis: ${basis}`} />}
        <StatusBadge status="Completed" label="Official — Recorded for Review" />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          These are the recorded results sent for review.
        </Typography>
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
                <TableCell align="right">Result</TableCell>
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
                        <StatusBadge status="Assigned" label="Reported Result" />
                      ) : (
                        `Replicate #${o.replicateNo}`
                      )}
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
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

    </Box>
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
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1 }}>
        <StatusBadge status="Prepared" label={`Basis: ${basis}`} />
        <StatusBadge status="Pending Review" label="Preview — not the official result" />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Calculations are computed server-side from current SST calibration and replicate inputs.
        </Typography>
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
                <TableCell align="right">Value</TableCell>
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
                        <StatusBadge status="Assigned" label="Reported Result" />
                      ) : (
                        `Replicate #${p.replicateNo}`
                      )}
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
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
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
          * Preview results are provisional. Official results are finalized when signed and submitted for review.
        </Typography>
      )}
    </Box>
  );
}
