import { useState } from "react";
import { Dialog, DialogTitle, DialogContent, DialogActions, Button, Grid, Typography, Box, Table, TableHead, TableRow, TableCell, TableBody, Tabs, Tab , useTheme} from "@mui/material";
import { brandColors } from "../../../theme";

interface CompareDialogProps {
  open: boolean;
  onClose: () => void;
  initialMode?: "products" | "locations";
}

export function CompareDialog({ open, onClose, initialMode = "products" }: CompareDialogProps) {
  const theme = useTheme();
  const [mode, setMode] = useState<"products" | "locations">(initialMode);

  const productComparisons = [
    { name: "Osteocare Liquid", category: "Finished Product", testsEvaluated: 12, meanValue: 8.3, alertCount: 1, oosCount: 0, compliancePercent: 91.7 },
    { name: "Feroglobin Syrup", category: "Finished Product", testsEvaluated: 12, meanValue: 4.2, alertCount: 0, oosCount: 0, compliancePercent: 100.0 },
    { name: "Honey (Raw Material)", category: "Raw Material", testsEvaluated: 12, meanValue: 142.0, alertCount: 2, oosCount: 1, compliancePercent: 75.0 },
    { name: "Purified Water (Loop 1)", category: "Water", testsEvaluated: 24, meanValue: 1.8, alertCount: 0, oosCount: 0, compliancePercent: 100.0 }
  ];

  const locationComparisons = [
    { name: "Point W-01 (Loop 1 Feed)", category: "Water", testsEvaluated: 24, meanValue: 1.2, alertCount: 0, oosCount: 0, compliancePercent: 100.0 },
    { name: "Point W-04 (Sampling Port)", category: "Water", testsEvaluated: 24, meanValue: 3.5, alertCount: 1, oosCount: 0, compliancePercent: 95.8 },
    { name: "Filling Line 1 (Cleanroom B)", category: "EM", testsEvaluated: 30, meanValue: 0.8, alertCount: 0, oosCount: 0, compliancePercent: 100.0 },
    { name: "Autoclave Room (Cleanroom C)", category: "EM", testsEvaluated: 30, meanValue: 2.1, alertCount: 1, oosCount: 0, compliancePercent: 96.7 }
  ];

  const data = mode === "products" ? productComparisons : locationComparisons;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ borderBottom: 1, borderColor: "divider", pb: 1.5 }}>
        <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
          Multi-Series Trend Comparison
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Compare performance and statistical distribution across batches, products, or facility points.
        </Typography>
        <Tabs
          value={mode}
          onChange={(_, v) => setMode(v)}
          sx={{ mt: 1, minHeight: 36, "& .MuiTab-root": { minHeight: 36, py: 0.5 } }}
        >
          <Tab label="Compare Products / Items" value="products" />
          <Tab label="Compare Sampling Locations / Points" value="locations" />
        </Tabs>
      </DialogTitle>

      <DialogContent sx={{ pt: 2.5 }}>
        <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 12 } }}>
          <TableHead>
            <TableRow>
              <TableCell>{mode === "products" ? "Product / Item" : "Location / Point"}</TableCell>
              <TableCell>Category</TableCell>
              <TableCell align="right">Tests Evaluated</TableCell>
              <TableCell align="right">Mean Result</TableCell>
              <TableCell align="right">Alert / Action</TableCell>
              <TableCell align="right">OOS Count</TableCell>
              <TableCell align="right">% Within Spec</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.map((row, idx) => (
              <TableRow key={idx} hover>
                <TableCell sx={{ fontWeight: 600 }}>{row.name}</TableCell>
                <TableCell>{row.category}</TableCell>
                <TableCell align="right">{row.testsEvaluated}</TableCell>
                <TableCell align="right" sx={{ fontWeight: 700 }}>{row.meanValue}</TableCell>
                <TableCell align="right" sx={{ color: row.alertCount > 0 ? brandColors.badgePM : "inherit" }}>
                  {row.alertCount}
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
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 1.5, borderTop: 1, borderColor: "divider" }}>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
