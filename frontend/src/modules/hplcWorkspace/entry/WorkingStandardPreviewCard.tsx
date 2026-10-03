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
  Stack,
  Paper,
  useTheme
} from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { tableHeadSx } from "../../../theme";
import type { WorkingStandardPreviewDto } from "../types";

export interface WorkingStandardPreviewCardProps {
  preview?: WorkingStandardPreviewDto | null;
  submitted?: boolean;
}

export function WorkingStandardPreviewCard({
  preview,
  submitted
}: WorkingStandardPreviewCardProps) {
  const theme = useTheme();

  const metrics = preview ? [
    { label: "Mean % Assay", value: `${preview.meanAssayPercent}%` },
    { label: "% RSD", value: preview.rsdPercent != null ? `${preview.rsdPercent}%` : "—" },
    { label: "Potency", value: `${preview.potencyPercent}%` },
    {
      label: "Acceptance Criteria",
      value: preview.passed ? "Passed" : "Failed",
      color: preview.passed ? "success.main" : "error.main"
    }
  ] : [];

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1.5 }}>
        {preview && (
          <StatusBadge
            status={preview.passed ? "Passed" : "Failed"}
            label={preview.passed ? "Evaluation: Passed" : "Evaluation: Failed"}
          />
        )}
        <StatusBadge
          status={submitted ? "Completed" : "PendingReview"}
          label={submitted ? "Official, Recorded for Review" : "Provisional Preview"}
        />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Calculations are computed server-side from current SST reference standard calibration.
        </Typography>
      </Box>

      {!preview ? (
        <Box sx={{ p: 2 }}>
          <Alert severity="info">
            No preview results available yet. Enter actual weights and responses above and click <strong>Save Replicates</strong> to generate the preview calculation.
          </Alert>
        </Box>
      ) : (
        <Stack spacing={2}>
          {/* Key metrics grid */}
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(4, 1fr)" },
              gap: 1.5
            }}
          >
            {metrics.map((m) => (
              <Paper
                key={m.label}
                elevation={0}
                sx={{
                  p: 1.5,
                  border: `1px solid ${theme.palette.divider}`,
                  borderRadius: 1,
                  bgcolor: theme.palette.background.paper
                }}
              >
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {m.label}
                </Typography>
                <Typography
                  variant="h6"
                  sx={{
                    fontWeight: 700,
                    fontVariantNumeric: "tabular-nums",
                    ...(m.color ? { color: m.color } : {})
                  }}
                >
                  {m.value}
                </Typography>
              </Paper>
            ))}
          </Box>

          {!preview.passed && preview.failureReasons && (
            <Alert severity="error">
              <strong>Evaluation Failure:</strong> {preview.failureReasons}
            </Alert>
          )}

          {/* Replicate Assay Details Table */}
          {preview.replicateAssayPercents && preview.replicateAssayPercents.length > 0 && (
            <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell>Replicate</TableCell>
                    <TableCell align="right">% Assay</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {preview.replicateAssayPercents.map((val, idx) => (
                    <TableRow key={`rep-assay-${idx}`} hover>
                      <TableCell sx={{ fontWeight: 600 }}>Replicate #{idx + 1}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                        {val}%
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          {!submitted && (
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              * Preview results are provisional. Official results are finalized when signed and submitted for review.
            </Typography>
          )}
        </Stack>
      )}
    </Box>
  );
}
