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
  Link,
  Stack,
  Paper,
  useTheme
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
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

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1.5 }}>
        {preview && (
          <StatusBadge
            status={preview.passed ? "Active" : "OOS"}
            label={preview.passed ? "Evaluation: Passed" : "Evaluation: Failed"}
          />
        )}
        <StatusBadge
          status={submitted ? "Completed" : "Pending Review"}
          label={submitted ? "Official, Recorded for Review" : "Provisional Preview"}
        />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Calculations are computed server-side from current SST reference standard calibration.
        </Typography>
      </Box>

      {submitted && (
        <Alert severity="success" sx={{ mb: 2 }}>
          Submitted — review on the{" "}
          <Link component={RouterLink} to="/working-standards" sx={{ fontWeight: 600, color: "inherit", textDecoration: "underline" }}>
            Working Standards page
          </Link>
          .
        </Alert>
      )}

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
            <Paper
              elevation={0}
              sx={{
                p: 1.5,
                border: `1px solid ${theme.palette.divider}`,
                borderRadius: 1,
                bgcolor: theme.palette.background.paper
              }}
            >
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Mean % Assay
              </Typography>
              <Typography variant="h6" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                {preview.meanAssayPercent}%
              </Typography>
            </Paper>

            <Paper
              elevation={0}
              sx={{
                p: 1.5,
                border: `1px solid ${theme.palette.divider}`,
                borderRadius: 1,
                bgcolor: theme.palette.background.paper
              }}
            >
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                % RSD
              </Typography>
              <Typography variant="h6" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                {preview.rsdPercent != null ? `${preview.rsdPercent}%` : "—"}
              </Typography>
            </Paper>

            <Paper
              elevation={0}
              sx={{
                p: 1.5,
                border: `1px solid ${theme.palette.divider}`,
                borderRadius: 1,
                bgcolor: theme.palette.background.paper
              }}
            >
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Potency
              </Typography>
              <Typography variant="h6" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                {preview.potencyPercent}%
              </Typography>
            </Paper>

            <Paper
              elevation={0}
              sx={{
                p: 1.5,
                border: `1px solid ${theme.palette.divider}`,
                borderRadius: 1,
                bgcolor: theme.palette.background.paper
              }}
            >
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Acceptance Criteria
              </Typography>
              <Typography
                variant="h6"
                sx={{
                  fontWeight: 700,
                  color: preview.passed ? "success.main" : "error.main"
                }}
              >
                {preview.passed ? "Passed" : "Failed"}
              </Typography>
            </Paper>
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
