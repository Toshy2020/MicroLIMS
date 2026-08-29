import { useEffect, useState } from "react";
import { Button, Typography, Box, Table, TableHead, TableRow, TableCell, TableBody, Tabs, Tab, CircularProgress, Alert, useTheme } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { brandColors } from "../../../theme";
import { CompareResult } from "../types/reportingTypes";
import { ReportingService } from "../services/ReportingService";

interface CompareDialogProps {
  open: boolean;
  onClose: () => void;
  initialMode?: "products" | "locations";
  testCode: string;
  category: string;
  fromDate: string;
  toDate: string;
}

// Real per-subject comparison for the currently-selected Test Code +
// Category + date range (the same criteria the Trending panel itself
// uses) - one shared query (ReportingQueryService.GetCompareBySubjectAsync),
// not per-row mock data. "products" vs "locations" only changes the
// leading column's label: SubjectName plays the product/item role for flat
// categories and the location/point role for the three hierarchy
// categories (Water/EM/After Cleaning) - the underlying rows are identical
// either way, since a category only ever has one of those two identities.
export function CompareDialog({ open, onClose, initialMode = "products", testCode, category, fromDate, toDate }: CompareDialogProps) {
  const theme = useTheme();
  const [mode, setMode] = useState<"products" | "locations">(initialMode);
  const [result, setResult] = useState<CompareResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setMode(initialMode);
  }, [initialMode, open]);

  useEffect(() => {
    if (!open || !testCode || !category) return;
    setLoading(true);
    setError(null);
    ReportingService.getCompare(testCode, category, fromDate, toDate)
      .then(setResult)
      .catch(() => setError("Unable to load comparison data for this test code."))
      .finally(() => setLoading(false));
  }, [open, testCode, category, fromDate, toDate]);

  const subjects = result?.subjects ?? [];
  const isNumeric = result?.isNumeric ?? true;

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      title={
        <Box>
          <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
            Multi-Series Trend Comparison
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            {result ? `${result.testDisplayName} — every subject with results in the selected date range` : "Compare performance and statistical distribution across batches, products, or facility points."}
          </Typography>
          <Tabs
            value={mode}
            onChange={(_, v) => setMode(v)}
            sx={{ mt: 1, minHeight: 36, "& .MuiTab-root": { minHeight: 36, py: 0.5 } }}
          >
            <Tab label="Compare Products / Items" value="products" />
            <Tab label="Compare Sampling Locations / Points" value="locations" />
          </Tabs>
        </Box>
      }
      actions={<Button onClick={onClose}>Close</Button>}
    >
      {loading && (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={28} />
        </Box>
      )}
      {!loading && error && <Alert severity="error">{error}</Alert>}
      {!loading && !error && subjects.length === 0 && (
        <Alert severity="info">No results found for this test code in the selected date range.</Alert>
      )}
      {!loading && !error && subjects.length > 0 && (
        <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 12 } }}>
          <TableHead>
            <TableRow>
              <TableCell>{mode === "products" ? "Product / Item" : "Location / Point"}</TableCell>
              <TableCell align="right">Tests Evaluated</TableCell>
              <TableCell align="right">{isNumeric ? "Mean Result" : "% Detected"}</TableCell>
              <TableCell align="right">Alert / Action</TableCell>
              <TableCell align="right">OOS Count</TableCell>
              <TableCell align="right">{isNumeric ? "% Within Spec" : "% Not Detected"}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {subjects.map((row) => (
              <TableRow key={row.subjectName} hover>
                <TableCell sx={{ fontWeight: 600 }}>{row.subjectName}</TableCell>
                <TableCell align="right">{row.testsEvaluated}</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>
                  {isNumeric ? (row.meanValue ?? "—") : (row.percentDetected != null ? `${row.percentDetected}%` : "—")}
                </TableCell>
                <TableCell align="right" sx={{ color: row.alertActionCount > 0 ? brandColors.badgePM : "inherit" }}>
                  {row.alertActionCount}
                </TableCell>
                <TableCell align="right" sx={{ color: row.oosCount > 0 ? brandColors.err : "inherit", fontWeight: row.oosCount > 0 ? 700 : 400 }}>
                  {row.oosCount}
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 700, color: row.compliancePercent >= 95 ? brandColors.ok : brandColors.badgePM }}>
                  {row.compliancePercent}%
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </FloatingDialog>
  );
}
