import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  Chip,
  Box,
  Paper,
  Tooltip,
  useTheme
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import HelpOutlinedIcon from "@mui/icons-material/HelpOutlined";
import { tableHeadSx } from "../../../theme";
import type { HplcSstAnalyteDto } from "../types";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";

export interface SstValuesTableProps {
  analytes: HplcSstAnalyteDto[];
  method?: HplcMethodResponse | null;
}

interface CriteriaRow {
  param: string;
  criterionText: string;
  reportedValue: string | number;
  computedValue?: string | number;
}

export function SstValuesTable({ analytes, method }: SstValuesTableProps) {
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
      <Box sx={{ p: 2, borderBottom: `1px solid ${theme.palette.divider}` }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Acceptance Criteria vs Entered / Computed Values
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Pass/Fail verdicts are evaluated strictly by the laboratory backend engine.
        </Typography>
      </Box>

      <TableContainer>
        <Table size="small">
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell>Analyte</TableCell>
              <TableCell>Standard Lot</TableCell>
              <TableCell align="right">Std Weight (mg)</TableCell>
              <TableCell align="right">Mean Response</TableCell>
              <TableCell align="right">Computed %RSD</TableCell>
              <TableCell>Reported Criteria Values</TableCell>
              <TableCell align="center">Verdict</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {analytes.map((a) => {
              const methodAnalyte = method?.analytes.find(
                (ma) => ma.id === a.hplcMethodAnalyteId || ma.name === a.analyteName
              );

              const rows: CriteriaRow[] = [];
              if (methodAnalyte?.sstMaxRsdPercent != null || a.reportedRsdPercent != null) {
                rows.push({
                  param: "%RSD",
                  criterionText: methodAnalyte?.sstMaxRsdPercent != null ? `≤ ${methodAnalyte.sstMaxRsdPercent}%` : "—",
                  reportedValue: a.reportedRsdPercent != null ? `${a.reportedRsdPercent}%` : "—"
                });
              }
              if (methodAnalyte?.sstMinResolution != null || a.resolution != null) {
                rows.push({
                  param: "Resolution",
                  criterionText: methodAnalyte?.sstMinResolution != null ? `≥ ${methodAnalyte.sstMinResolution}` : "—",
                  reportedValue: a.resolution ?? "—"
                });
              }
              if (methodAnalyte?.sstMaxTailingFactor != null || a.tailingFactor != null) {
                rows.push({
                  param: "Tailing Factor",
                  criterionText: methodAnalyte?.sstMaxTailingFactor != null ? `≤ ${methodAnalyte.sstMaxTailingFactor}` : "—",
                  reportedValue: a.tailingFactor ?? "—"
                });
              }
              if (methodAnalyte?.sstMinTheoreticalPlates != null || a.theoreticalPlates != null) {
                rows.push({
                  param: "Theoretical Plates",
                  criterionText: methodAnalyte?.sstMinTheoreticalPlates != null ? `≥ ${methodAnalyte.sstMinTheoreticalPlates}` : "—",
                  reportedValue: a.theoreticalPlates ?? "—"
                });
              }
              if (methodAnalyte?.sstMinRetentionFactor != null || a.retentionFactor != null) {
                rows.push({
                  param: "Retention Factor (k')",
                  criterionText: methodAnalyte?.sstMinRetentionFactor != null ? `≥ ${methodAnalyte.sstMinRetentionFactor}` : "—",
                  reportedValue: a.retentionFactor ?? "—"
                });
              }
              if (methodAnalyte?.sstMinSignalToNoise != null || a.signalToNoise != null) {
                rows.push({
                  param: "Signal-to-Noise",
                  criterionText: methodAnalyte?.sstMinSignalToNoise != null ? `≥ ${methodAnalyte.sstMinSignalToNoise}` : "—",
                  reportedValue: a.signalToNoise ?? "—"
                });
              }
              if (methodAnalyte?.sstMinPeakToValley != null || a.peakToValley != null) {
                rows.push({
                  param: "Peak-to-Valley",
                  criterionText: methodAnalyte?.sstMinPeakToValley != null ? `≥ ${methodAnalyte.sstMinPeakToValley}` : "—",
                  reportedValue: a.peakToValley ?? "—"
                });
              }

              return (
                <TableRow key={a.id} sx={{ verticalAlign: "top" }}>
                  <TableCell sx={{ fontWeight: 700, py: 1.5 }}>
                    {a.analyteName}
                  </TableCell>
                  <TableCell sx={{ py: 1.5 }}>
                    {a.standardMaterialBatch ? (
                      <Box>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {a.standardMaterialBatch}
                        </Typography>
                        <Typography variant="caption" sx={{ color: "text.secondary" }}>
                          P: {a.standardPurityPercent ?? "—"}% | MC: {a.standardMoisturePercent ?? "—"}%
                        </Typography>
                      </Box>
                    ) : (
                      <Typography variant="caption" sx={{ color: "text.secondary" }}>
                        Not selected
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell align="right" sx={{ py: 1.5, fontWeight: 600 }}>
                    {a.standardWeightMg ?? "—"}
                  </TableCell>
                  <TableCell align="right" sx={{ py: 1.5 }}>
                    {a.meanResponse != null ? (
                      <Box>
                        <Typography variant="body2" sx={{ fontWeight: 500 }}>
                          {a.meanResponse.toLocaleString(undefined, { maximumFractionDigits: 2 })}
                        </Typography>
                        {!a.passed && !a.failureReasons && (
                          <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 10, display: "block" }}>
                            pending confirmation
                          </Typography>
                        )}
                      </Box>
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell align="right" sx={{ py: 1.5, fontWeight: 600 }}>
                    {a.computedRsdPercent != null ? (
                      <Box>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {`${a.computedRsdPercent.toFixed(2)}%`}
                        </Typography>
                        {!a.passed && !a.failureReasons && (
                          <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 10, display: "block" }}>
                            pending confirmation
                          </Typography>
                        )}
                      </Box>
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell sx={{ py: 1.5 }}>
                    {rows.length === 0 ? (
                      <Typography variant="caption" sx={{ color: "text.secondary" }}>
                        No criteria configured
                      </Typography>
                    ) : (
                      <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
                        {rows.map((r, i) => (
                          <Typography key={i} variant="caption" sx={{ display: "block", fontSize: 12 }}>
                            <strong>{r.param}:</strong> {r.reportedValue} (Criterion: {r.criterionText})
                          </Typography>
                        ))}
                      </Box>
                    )}
                  </TableCell>
                  <TableCell align="center" sx={{ py: 1.5 }}>
                    {a.passed ? (
                      <Chip
                        size="small"
                        color="success"
                        icon={<CheckCircleIcon sx={{ fontSize: 14 }} />}
                        label="Pass"
                        sx={{ fontWeight: 600 }}
                      />
                    ) : a.failureReasons ? (
                      <Tooltip title={a.failureReasons} arrow placement="left">
                        <Chip
                          size="small"
                          color="error"
                          icon={<CancelIcon sx={{ fontSize: 14 }} />}
                          label="Fail"
                          sx={{ fontWeight: 600 }}
                        />
                      </Tooltip>
                    ) : (
                      <Chip
                        size="small"
                        color="default"
                        icon={<HelpOutlinedIcon sx={{ fontSize: 14 }} />}
                        label="Pending"
                        sx={{ fontWeight: 600 }}
                      />
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}
