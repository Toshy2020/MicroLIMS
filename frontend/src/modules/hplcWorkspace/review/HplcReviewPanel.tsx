import { useState, useEffect } from "react";
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
  Button,
  CircularProgress,
  useTheme
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import ImageIcon from "@mui/icons-material/Image";
import InsertDriveFileIcon from "@mui/icons-material/InsertDriveFile";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import { StatusBadge } from "../../../components/StatusBadge";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { ParameterResultDetail } from "../../testingWorkspace/types/sampleSummaryTypes";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import type { HplcEvidenceDto } from "../types";

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

function parseHplcCalc(json: string | null): HplcMethodAssayCalculationData | null {
  if (!json) return null;
  try {
    return JSON.parse(json) as HplcMethodAssayCalculationData;
  } catch {
    return null;
  }
}

function formatEvidenceKind(kind: string): string {
  switch (kind) {
    case "StandardReport":
      return "Standard Report";
    case "SampleReport":
      return "Sample Report";
    default:
      return kind;
  }
}

function getFileIcon(contentType: string) {
  if (contentType?.includes("pdf")) return <PictureAsPdfIcon color="error" sx={{ fontSize: 16 }} />;
  if (contentType?.includes("image")) return <ImageIcon color="primary" sx={{ fontSize: 16 }} />;
  return <InsertDriveFileIcon color="action" sx={{ fontSize: 16 }} />;
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

  const [evidenceList, setEvidenceList] = useState<HplcEvidenceDto[]>([]);
  const [evidenceLoading, setEvidenceLoading] = useState(false);
  const [evidenceError, setEvidenceError] = useState<string | null>(null);
  const [openingEvidenceId, setOpeningEvidenceId] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    if (!testOrderId) return;
    setEvidenceLoading(true);
    setEvidenceError(null);

    HplcWorkspaceService.getTestOrderEvidence(testOrderId)
      .then((data) => {
        if (active) {
          setEvidenceList(Array.isArray(data) ? data : []);
        }
      })
      .catch(() => {
        if (active) {
          setEvidenceError("Unable to load reports.");
        }
      })
      .finally(() => {
        if (active) {
          setEvidenceLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [testOrderId]);

  const handleOpenEvidence = async (id: number) => {
    setOpeningEvidenceId(id);
    try {
      const svc = HplcWorkspaceService as unknown as Record<string, (id: number) => Promise<unknown>>;
      if (typeof svc.viewEvidence === "function") {
        await svc.viewEvidence(id);
      } else {
        await HplcWorkspaceService.openEvidenceInNewTab(id);
      }
    } catch {
      // Quiet error handling
    } finally {
      setOpeningEvidenceId(null);
    }
  };

  const renderReportsSection = () => (
    <Box sx={{ mt: 1.5, mb: 1.5 }}>
      <Typography
        sx={{
          fontSize: 11,
          fontWeight: 700,
          color: "text.secondary",
          mb: 0.75,
          textTransform: "uppercase"
        }}
      >
        Reports
      </Typography>

      {evidenceLoading && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 0.5 }}>
          <CircularProgress size={14} />
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Loading reports...
          </Typography>
        </Box>
      )}

      {evidenceError && (
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
          {evidenceError}
        </Typography>
      )}

      {!evidenceLoading && !evidenceError && evidenceList.length === 0 && (
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
          No reports available for this test order.
        </Typography>
      )}

      {!evidenceLoading && !evidenceError && evidenceList.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 11 }}>Report / Context</TableCell>
                <TableCell sx={{ fontSize: 11 }}>File Name</TableCell>
                <TableCell sx={{ fontSize: 11 }}>Uploaded By</TableCell>
                <TableCell sx={{ fontSize: 11 }}>Uploaded At</TableCell>
                <TableCell sx={{ fontSize: 11 }} align="right">Action</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {evidenceList.map((e) => (
                <TableRow key={e.id} hover>
                  <TableCell sx={{ fontSize: 11 }}>
                    <Chip
                      label={`${formatEvidenceKind(e.kind)} (${e.context})`}
                      size="small"
                      variant="outlined"
                      sx={{ fontSize: 11, height: 20 }}
                    />
                  </TableCell>
                  <TableCell sx={{ fontSize: 11 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                      {getFileIcon(e.contentType)}
                      <Typography sx={{ fontSize: 11, fontWeight: 600 }}>
                        {e.fileName}
                      </Typography>
                    </Box>
                  </TableCell>
                  <TableCell sx={{ fontSize: 11 }}>
                    {e.uploadedByUserName ?? (e.uploadedByUserId ? `User #${e.uploadedByUserId}` : "—")}
                  </TableCell>
                  <TableCell sx={{ fontSize: 11 }}>
                    {e.uploadedAt ? new Date(e.uploadedAt).toLocaleString() : "—"}
                  </TableCell>
                  <TableCell sx={{ fontSize: 11 }} align="right">
                    <Button
                      size="small"
                      variant="outlined"
                      startIcon={
                        openingEvidenceId === e.id ? (
                          <CircularProgress size={12} color="inherit" />
                        ) : (
                          <VisibilityOutlinedIcon sx={{ fontSize: 14 }} />
                        )
                      }
                      onClick={() => handleOpenEvidence(e.id)}
                      disabled={openingEvidenceId === e.id}
                      sx={{ textTransform: "none", fontSize: 11, py: 0.25, px: 1, minHeight: 24 }}
                    >
                      View
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );

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
        {renderReportsSection()}
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
          <StatusBadge status="Prepared" label={`Basis: ${calc.basis}`} />
          <StatusBadge status="Draft" label={calc.quantity === "AmountPerUnit" ? "Amount per unit" : "% Assay"} />
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
        <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap" }}>
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
                <TableCell align="right" sx={{ fontSize: 11 }}>Actual Weight (mg)</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Peak Response</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Calculated % Assay</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {calc.replicates.map((r) => (
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
      {renderReportsSection()}

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
            Chromatograms and raw evidence stored under HPLC Run <strong>{calc.runCode}</strong>
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
