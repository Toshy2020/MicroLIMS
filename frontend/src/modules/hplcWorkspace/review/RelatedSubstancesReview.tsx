import { Box, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography, useTheme } from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { tableHeadSx } from "../../../theme";
import { HplcReportsSection } from "./HplcReportsSection";

interface RelatedSubstancePeak {
  label: string;
  relativeRetention: number | null;
  area: number;
  correctionFactor: number;
  percent: number;
  disregarded: boolean;
}

export interface RelatedSubstanceCalculationData {
  quantity: "RelatedSubstance";
  analyte: string;
  runCode: string;
  sstCode: string;
  referenceSolutionPercent: number;
  disregardLimitPercent: number | null;
  replicates: { replicateNo: number; referenceArea: number; peaks: RelatedSubstancePeak[] }[];
}

export function isRelatedSubstanceCalculation(json: string | null): boolean {
  if (!json) return false;
  try {
    return JSON.parse(json)?.quantity === "RelatedSubstance";
  } catch {
    return false;
  }
}

// The peaks behind a related-substances result; the reported rows are in the table above this panel.
export function RelatedSubstancesReview({ calc, testOrderId }: { calc: RelatedSubstanceCalculationData; testOrderId: number }) {
  const theme = useTheme();
  return (
    <Box sx={{ mt: 1.5, p: 2, borderRadius: 2, border: `1px solid ${theme.palette.divider}`, backgroundColor: theme.palette.background.paper }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }} useFlexGap>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>Related Substances Review · {calc.analyte}</Typography>
          <StatusBadge status="Prepared" label={`Reference: ${calc.referenceSolutionPercent}%`} />
          <StatusBadge status="Prepared" label={calc.disregardLimitPercent == null ? "No disregard limit" : `Disregard: ${calc.disregardLimitPercent}%`} />
        </Stack>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Run: <strong>{calc.runCode}</strong> · SST: <strong>{calc.sstCode}</strong>
        </Typography>
      </Box>

      {calc.replicates.map((r) => (
        <Box key={r.replicateNo} sx={{ mb: 1.5 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, mb: 0.5 }}>
            Replicate #{r.replicateNo} · Reference solution area: {r.referenceArea}
          </Typography>
          <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell sx={{ fontSize: 12 }}>Peak</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12 }}>RRT</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12 }}>Area</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12 }}>Correction Factor</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12 }}>%</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {r.peaks.map((p, i) => (
                  <TableRow key={i} hover>
                    <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>{p.label}</TableCell>
                    <TableCell align="right" sx={{ fontSize: 12 }}>{p.relativeRetention ?? "—"}</TableCell>
                    <TableCell align="right" sx={{ fontSize: 12 }}>{p.area}</TableCell>
                    <TableCell align="right" sx={{ fontSize: 12 }}>{p.correctionFactor}</TableCell>
                    <TableCell align="right" sx={{ fontSize: 12 }}>
                      {p.percent}
                      {p.disregarded ? " (disregarded)" : ""}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>
      ))}

      <HplcReportsSection testOrderId={testOrderId} />
    </Box>
  );
}
