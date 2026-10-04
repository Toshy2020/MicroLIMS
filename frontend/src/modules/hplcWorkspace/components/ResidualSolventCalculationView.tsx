import React from "react";
import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Stack,
  useTheme
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import { StatusBadge } from "../../../components/StatusBadge";
import { HplcStatusBadge } from "./HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { ResultStatus } from "../types";

export interface ResidualSolventReplicateData {
  replicateNo: number;
  sampleWeightMg: number;
  response: number;
  ppm: number;
  ppmDisplay: string;
}

export interface ResidualSolventCalculationData {
  analyte: string;
  hplcMethodAnalyteId: number;
  quantity: "ResidualSolventPpm";
  basis: string;
  standardConcentrationUgPerMl: number;
  sampleSolutionVolumeMl: number;
  standardMeanResponse: number;
  replicates: ResidualSolventReplicateData[];
  reportedValue: number;
  notDetected: boolean;
  runCode: string;
  sstCode: string;
  hplcMethodId: number;
  hplcRunId?: number;
  hplcRunSampleId?: number;
}

export interface ResidualSolventCalculationViewProps {
  calc: ResidualSolventCalculationData;
  reportedDisplay?: string | null;
  specLimit?: string | null;
  status?: ResultStatus | string;
  reportsSection?: React.ReactNode;
}

export function ResidualSolventCalculationView({
  calc,
  reportedDisplay,
  specLimit,
  status,
  reportsSection
}: ResidualSolventCalculationViewProps) {
  const theme = useTheme();

  const formattedReportedResult = calc.notDetected
    ? "Not detected"
    : reportedDisplay
      ? (reportedDisplay.toLowerCase().includes("ppm") ? reportedDisplay : `${reportedDisplay} ppm`)
      : `${calc.reportedValue} ppm`;

  return (
    <Box
      sx={{
        mt: 1.5,
        p: 2,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      {/* Title & Run Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
            Residual Solvent Review · {calc.analyte}
          </Typography>
          <StatusBadge status="Prepared" label={`Basis: ${calc.basis || "Mean"}`} />
          <StatusBadge status="Draft" label="Residual solvent (ppm)" />
        </Box>

        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Run: <strong>{calc.runCode}</strong> · SST: <strong>{calc.sstCode}</strong>
        </Typography>
      </Box>

      {/* Snapshot Reference & Standard Calibration */}
      <Paper
        elevation={0}
        sx={{
          p: 1.5,
          mb: 1.5,
          borderRadius: 1,
          backgroundColor: theme.palette.action.hover,
          border: `1px solid ${theme.palette.divider}`
        }}
      >
        <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", mb: 0.5, textTransform: "uppercase" }}>
          Method Snapshot & Standard Calibration
        </Typography>
        <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap", mb: 1 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Standard Concentration (C_std)
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.standardConcentrationUgPerMl} µg/mL
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Sample Solution Volume (V)
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.sampleSolutionVolumeMl} mL
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Standard Mean Response (r̄_std)
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
              {calc.standardMeanResponse}
            </Typography>
          </Box>
        </Stack>
        <Box sx={{ pt: 0.5, borderTop: `1px dashed ${theme.palette.divider}` }}>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", fontStyle: "italic" }}>
            Formula: ppm = (C_std × V / W) × (r_u / r̄_std)
          </Typography>
        </Box>
      </Paper>

      {/* Replicate Injections Table */}
      {calc.replicates && calc.replicates.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1, mb: 1.5 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 11 }}>Replicate</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Sample Weight (mg)</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Area</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Calculated (ppm)</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {calc.replicates.map((r) => (
                <TableRow key={r.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 600, fontSize: 12 }}>#{r.replicateNo}</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {r.sampleWeightMg}
                  </TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {r.response}
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {r.ppmDisplay || `${r.ppm} ppm`}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Optional reports slot */}
      {reportsSection}

      {/* Final Outcome */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12 }}>
            Reported Result: <strong>{formattedReportedResult}</strong>
          </Typography>
          {specLimit && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              (Spec: {specLimit})
            </Typography>
          )}
          {status && <HplcStatusBadge status={status} />}
        </Box>

        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
          <LinkIcon fontSize="small" sx={{ fontSize: 14, color: "text.secondary" }} />
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
            Chromatograms and raw evidence stored under GC Run <strong>{calc.runCode}</strong>
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
