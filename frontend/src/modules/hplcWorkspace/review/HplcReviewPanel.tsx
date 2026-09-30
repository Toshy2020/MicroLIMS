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
  Chip,
  Alert,
  Stack,
  useTheme
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import LinkIcon from "@mui/icons-material/Link";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { ParameterResultDetail } from "../../testingWorkspace/types/sampleSummaryTypes";

export interface HplcMethodAssayReplicateData {
  replicateNo: number;
  actualWeightMg: number;
  response: number;
  assayPercent: number;
}

export interface HplcMethodAssayCalculationData {
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
}

function parseHplcCalc(json: string | null): HplcMethodAssayCalculationData | null {
  if (!json) return null;
  try {
    return JSON.parse(json) as HplcMethodAssayCalculationData;
  } catch {
    return null;
  }
}

export function HplcReviewPanel({ parameter }: HplcReviewPanelProps) {
  const theme = useTheme();
  const calc = parseHplcCalc(parameter.calculationJson);

  if (!calc) {
    return (
      <Box sx={{ mt: 1.5, p: 1.5, border: "1px dashed", borderColor: "divider", borderRadius: 1.5 }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>
          HPLC Workspace Assay · {parameter.parameterName}
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
          Recorded via HPLC Workspace. Parameter result: <strong>{parameter.reportedDisplay} {parameter.unit ?? ""}</strong> ({parameter.comparisonStatus}).
        </Typography>
        <Alert severity="info" sx={{ fontSize: 11, py: 0.5 }}>
          Detailed chromatographic calculation payload was not found or is in historical format.
        </Alert>
      </Box>
    );
  }

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
            HPLC Assay Review · {calc.analyte}
          </Typography>
          <Chip
            icon={<ScienceOutlinedIcon fontSize="small" />}
            label={`Basis: ${calc.basis}`}
            size="small"
            color="primary"
            variant="outlined"
          />
          <Chip
            label={calc.quantity === "AmountPerUnit" ? "Amount per unit" : "% Assay"}
            size="small"
            variant="outlined"
          />
        </Box>

        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Run: <strong>{calc.runCode}</strong> · SST: <strong>{calc.sstCode}</strong>
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
        <Stack direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap" }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Theoretical Weights
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              Std: {calc.thWtStdMg} mg · Test: {calc.thWtTestMg} mg
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Standard Reference
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              Act.Wt: {calc.standardWeightMg} mg · Purity: {calc.standardPurityPercent}% · MC: {calc.standardMoisturePercent}%
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Standard Mean Response
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
              {calc.standardMeanResponse}
            </Typography>
          </Box>
          {calc.labelClaim != null && (
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Label Claim
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {calc.labelClaim}
              </Typography>
            </Box>
          )}
        </Stack>
      </Paper>

      {/* Replicate Injections & Calculations Table */}
      {calc.replicates && calc.replicates.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1, mb: 1.5 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 11 }}>Replicate</TableCell>
                <TableCell sx={{ fontSize: 11 }}>Actual Weight (mg)</TableCell>
                <TableCell sx={{ fontSize: 11 }}>Peak Response</TableCell>
                <TableCell sx={{ fontSize: 11 }}>Calculated % Assay</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {calc.replicates.map((r) => (
                <TableRow key={r.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 600, fontSize: 12 }}>#{r.replicateNo}</TableCell>
                  <TableCell sx={{ fontSize: 12 }}>{r.actualWeightMg}</TableCell>
                  <TableCell sx={{ fontSize: 12, fontFamily: "monospace" }}>{r.response}</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: 12, fontFamily: "monospace" }}>
                    {r.assayPercent} %
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Final Outcome & GxP Note */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12 }}>
            Reported Result: <strong>{parameter.reportedDisplay} {parameter.unit ?? ""}</strong>
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
            Chromatograms and raw evidence stored under HPLC Run <strong>{calc.runCode}</strong>
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
