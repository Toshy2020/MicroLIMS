import {
  Box,
  Typography,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  Stack,
  useTheme
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import { StatusBadge } from "../../../components/StatusBadge";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { ParameterResultDetail } from "../../testingWorkspace/types/sampleSummaryTypes";
import {
  ResidualSolventCalculationView,
  type ResidualSolventCalculationData
} from "../components/ResidualSolventCalculationView";
import { HplcReportsSection } from "./HplcReportsSection";

export interface HplcMethodAssayReplicateData {
  replicateNo: number;
  actualWeightMg: number;
  response: number;
  assayPercent: number;
  assayPercentDisplay?: string;
}

export interface HplcMethodAssayCalculationData {
  hplcRunId?: number;
  hplcRunSampleId?: number;
  analyte: string;
  hplcMethodAnalyteId: number;
  quantity: string;
  basis: string;
  replicateNo?: number | null;
  thWtStdMg: number;
  thWtTestMg: number;
  standardWeightMg: number;
  standardPurityPercent: number;
  standardMoisturePercent: number;
  standardMeanResponse: number;
  labelClaim?: number | null;
  replicates: HplcMethodAssayReplicateData[];
  reportedValue: number;
  runCode: string;
  sstCode: string;
  hplcMethodId: number;
}

export interface HplcReviewPanelProps {
  parameter: ParameterResultDetail;
  testOrderId: number;
}

function parseHplcCalc(json: string | null): HplcMethodAssayCalculationData | ResidualSolventCalculationData | null {
  if (!json) return null;
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
}

function formatReplicateAssay(r: HplcMethodAssayReplicateData): string {
  if (r.assayPercentDisplay) {
    return r.assayPercentDisplay;
  }
  if (typeof r.assayPercent === "number") {
    return `${r.assayPercent.toLocaleString(undefined, { maximumFractionDigits: 2 })} %`;
  }
  return "—";
}

export function HplcReviewPanel({ parameter, testOrderId }: HplcReviewPanelProps) {
  const theme = useTheme();
  const calc = parseHplcCalc(parameter.calculationJson);

  if (!calc) {
    return (
      <Box sx={{ mt: 1.5, p: 1.5, border: "1px dashed", borderColor: "divider", borderRadius: 1.5 }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>
          HPLC Workspace Assay · {parameter.parameterName}
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
          Recorded via HPLC Workspace. Parameter result: <strong>{parameter.reportedDisplay}</strong> ({parameter.comparisonStatus}).
        </Typography>
        <Alert severity="info" sx={{ fontSize: 11, py: 0.5, mb: 1 }}>
          Detailed chromatographic calculation payload was not found or is in historical format.
        </Alert>
        <HplcReportsSection testOrderId={testOrderId} />
      </Box>
    );
  }

  if (calc.quantity === "ResidualSolventPpm") {
    return (
      <ResidualSolventCalculationView
        calc={calc as ResidualSolventCalculationData}
        reportedDisplay={parameter.reportedDisplay}
        specLimit={parameter.specLimit}
        status={parameter.comparisonStatus}
        reportsSection={<HplcReportsSection testOrderId={testOrderId} />}
      />
    );
  }

  const assayCalc = calc as HplcMethodAssayCalculationData;

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
            HPLC Assay Review · {assayCalc.analyte}
          </Typography>
          <StatusBadge status="Prepared" label={`Basis: ${assayCalc.basis}`} />
          <StatusBadge status="Draft" label={assayCalc.quantity === "AmountPerUnit" ? "Amount per unit" : "% Assay"} />
        </Box>

        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Run: <strong>{assayCalc.runCode}</strong> · SST: <strong>{assayCalc.sstCode}</strong>
        </Typography>
      </Box>

      {/* Snapshot Reference and Theoretical Parameters */}
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
        <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap" }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Theoretical Weights
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              Std: {assayCalc.thWtStdMg} mg · Test: {assayCalc.thWtTestMg} mg
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Standard Reference
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              Act.Wt: {assayCalc.standardWeightMg} mg · Purity: {assayCalc.standardPurityPercent}% · MC: {assayCalc.standardMoisturePercent}%
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Standard Mean Response
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
              {assayCalc.standardMeanResponse}
            </Typography>
          </Box>
          {assayCalc.labelClaim != null && (
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Label Claim
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {assayCalc.labelClaim}
              </Typography>
            </Box>
          )}
        </Stack>
      </Paper>

      {/* Replicate Injections & Calculations Table */}
      {assayCalc.replicates && assayCalc.replicates.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1, mb: 1.5 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 11 }}>Replicate</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Actual Weight (mg)</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Peak Response</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Calculated % Assay</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {assayCalc.replicates.map((r) => (
                <TableRow key={r.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 600, fontSize: 12 }}>#{r.replicateNo}</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>{r.actualWeightMg}</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>{r.response}</TableCell>
                  <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {formatReplicateAssay(r)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Reports Section */}
      <HplcReportsSection testOrderId={testOrderId} />

      {/* Final Outcome & GxP Note */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12 }}>
            Reported Result: <strong>{parameter.reportedDisplay}</strong>
          </Typography>
          {parameter.specLimit && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              (Spec: {parameter.specLimit})
            </Typography>
          )}
          <HplcStatusBadge status={parameter.comparisonStatus} />
        </Box>

        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
          <LinkIcon fontSize="small" sx={{ fontSize: 14, color: "text.secondary" }} />
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
            Chromatograms and raw evidence stored under HPLC Run <strong>{assayCalc.runCode}</strong>
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
