import { useEffect, useState } from "react";
import { Button, Typography, Box, Table, TableHead, TableRow, TableCell, TableBody, Tabs, Tab, CircularProgress, Alert, useTheme, TableContainer } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { brandColors } from "../../../theme";
import { CompareResult } from "../types/reportingTypes";
import { ReportingService } from "../services/ReportingService";
import { useTranslation } from "../../../i18n";

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
// Category + date range (the same criteria the Control Charts page itself
// uses) - one shared query (ReportingQueryService.GetCompareBySubjectAsync),
// not per-row mock data. "products" vs "locations" only changes the
// leading column's label: SubjectName plays the product/item role for flat
// categories and the location/point role for the three hierarchy
// categories (Water/EM/After Cleaning) - the underlying rows are identical
// either way, since a category only ever has one of those two identities.
export function CompareDialog({ open, onClose, initialMode = "products", testCode, category, fromDate, toDate }: CompareDialogProps) {
  const { t } = useTranslation("reports");
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
      .catch(() => setError(t("compare.loadError")))
      .finally(() => setLoading(false));
  }, [open, testCode, category, fromDate, toDate, t]);

  const subjects = result?.subjects ?? [];
  const isNumeric = result?.isNumeric ?? true;

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      title={
        <Box>
          <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
            {t("compare.title")}
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            {result ? t("compare.subtitleWithTest", { test: result.testDisplayName }) : t("compare.subtitleDefault")}
          </Typography>
          <Tabs
            value={mode}
            onChange={(_, v) => setMode(v)}
            sx={{ mt: 1, minHeight: 36, "& .MuiTab-root": { minHeight: 36, py: 0.5 } }}
          >
            <Tab label={t("compare.tabProducts")} value="products" />
            <Tab label={t("compare.tabLocations")} value="locations" />
          </Tabs>
        </Box>
      }
      actions={<Button onClick={onClose}>{t("compare.close")}</Button>}
    >
      {loading && (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={28} />
        </Box>
      )}
      {!loading && error && <Alert severity="error">{error}</Alert>}
      {!loading && !error && subjects.length === 0 && (
        <Alert severity="info">{t("compare.empty")}</Alert>
      )}
      {!loading && !error && subjects.length > 0 && (
        <TableContainer>
          <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 12 } }}>
            <TableHead>
              <TableRow>
                <TableCell>{mode === "products" ? t("compare.productItem") : t("compare.locationPoint")}</TableCell>
                <TableCell align="right">{t("compare.testsEvaluated")}</TableCell>
                <TableCell align="right">{isNumeric ? t("compare.meanResult") : t("compare.percentDetected")}</TableCell>
                <TableCell align="right">{t("compare.alertAction")}</TableCell>
                <TableCell align="right">{t("compare.oosCount")}</TableCell>
                <TableCell align="right">{isNumeric ? t("compare.percentWithinSpec") : t("compare.percentNotDetected")}</TableCell>
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
        </TableContainer>
      )}
    </FloatingDialog>
  );
}
