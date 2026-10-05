import { useState } from "react";
import {
  Box,
  Paper,
  Typography,
  TextField,
  Button,
  Grid,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Alert,
  CircularProgress,
  Stack,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { toast } from "sonner";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { tableHeadSx } from "../../../theme";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpRunDto } from "../types";

export interface IcpCcvPanelProps {
  run: IcpRunDto;
  canOperate: boolean;
  onRunUpdated: () => void;
}

export function IcpCcvPanel({
  run,
  canOperate,
  onRunUpdated
}: IcpCcvPanelProps) {
  const theme = useTheme();
  const method = run.method;
  const cal = run.calibration;

  const isConfirmed = cal?.status === "Confirmed";
  const isExpired = cal?.expiresAt ? new Date(cal.expiresAt).getTime() < Date.now() : true;
  const isRunOpen = run.status === "Open";
  const canAddCcv = isConfirmed && !isExpired && canOperate && isRunOpen;

  const [selectedElementId, setSelectedElementId] = useState<number | "">("");
  const [measuredMgPerL, setMeasuredMgPerL] = useState("");
  const [adding, setAdding] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const readings = run.ccvReadings || [];

  const handleAddReading = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canAddCcv || selectedElementId === "" || !measuredMgPerL.trim()) return;

    setAdding(true);
    setError(null);
    try {
      await IcpWorkspaceService.addCcv(run.id, {
        icpMethodElementId: Number(selectedElementId),
        measuredMgPerL: Number(measuredMgPerL)
      });
      setSelectedElementId("");
      setMeasuredMgPerL("");
      toast.success("CCV reading recorded.");
      onRunUpdated();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not record CCV reading.");
    } finally {
      setAdding(false);
    }
  };

  return (
    <Stack spacing={3}>
      {/* CCV Criteria Summary Card */}
      <Paper
        elevation={0}
        sx={{
          p: 2.5,
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`,
          backgroundColor: theme.palette.background.paper
        }}
      >
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 0.5 }}>
          Continuing Calibration Verification (CCV)
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Periodic quality control checks to verify instrument calibration stability throughout the run.
        </Typography>

        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Box sx={{ p: 1.5, borderRadius: 1.5, border: `1px solid ${theme.palette.divider}` }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Nominal Concentration
              </Typography>
              <Typography variant="body1" sx={{ fontWeight: 700, fontFamily: monospaceFontFamily }}>
                {method.ccvNominalMgPerL != null ? `${method.ccvNominalMgPerL} mg/L` : "—"}
              </Typography>
            </Box>
          </Grid>

          <Grid size={{ xs: 12, sm: 4 }}>
            <Box sx={{ p: 1.5, borderRadius: 1.5, border: `1px solid ${theme.palette.divider}` }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Acceptable Recovery Range
              </Typography>
              <Typography variant="body1" sx={{ fontWeight: 700, fontFamily: monospaceFontFamily }}>
                {method.ccvRecoveryLowPercent}% – {method.ccvRecoveryHighPercent}%
              </Typography>
            </Box>
          </Grid>

          <Grid size={{ xs: 12, sm: 4 }}>
            <Box sx={{ p: 1.5, borderRadius: 1.5, border: `1px solid ${theme.palette.divider}` }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Total Readings
              </Typography>
              <Typography variant="body1" sx={{ fontWeight: 700 }}>
                {readings.length} recorded
              </Typography>
            </Box>
          </Grid>
        </Grid>
      </Paper>

      {/* Disabled Banner when Calibration Not Ready */}
      {!isConfirmed && (
        <Alert severity="warning">
          CCV readings cannot be entered until the initial calibration is confirmed.
        </Alert>
      )}

      {isConfirmed && isExpired && (
        <Alert severity="error">
          Calibration expired at {new Date(cal!.expiresAt!).toLocaleString()}. CCV entry is disabled.
        </Alert>
      )}

      {/* Add CCV Reading Form (Append-Only) */}
      {canAddCcv && (
        <Paper
          elevation={0}
          component="form"
          onSubmit={handleAddReading}
          sx={{
            p: 2.5,
            borderRadius: 2,
            border: `1px solid ${theme.palette.divider}`,
            backgroundColor: theme.palette.background.paper
          }}
        >
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
            Record New CCV Reading
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          <Grid container spacing={2} sx={{ alignItems: "center" }}>
            <Grid size={{ xs: 12, sm: 5 }}>
              <FormControl fullWidth size="small" required>
                <InputLabel id="ccv-element-label">Element</InputLabel>
                <Select
                  labelId="ccv-element-label"
                  label="Element"
                  value={selectedElementId}
                  onChange={(e) => setSelectedElementId(e.target.value as number)}
                >
                  <MenuItem value="">
                    <em>Select Analyte / Element</em>
                  </MenuItem>
                  {method.elements?.map((el) => (
                    <MenuItem key={el.id} value={el.id}>
                      {el.symbol} ({el.wavelengthNm.toFixed(3)} nm)
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>

            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField
                size="small"
                fullWidth
                required
                type="number"
                label="Measured Concentration (mg/L)"
                placeholder="e.g. 5.02"
                value={measuredMgPerL}
                onChange={(e) => setMeasuredMgPerL(e.target.value)}
                slotProps={{
                  htmlInput: { step: "any", min: 0, style: { fontFamily: monospaceFontFamily } }
                }}
              />
            </Grid>

            <Grid size={{ xs: 12, sm: 3 }}>
              <Button
                type="submit"
                variant="contained"
                startIcon={adding ? <CircularProgress size={16} color="inherit" /> : <AddIcon />}
                disabled={adding || selectedElementId === "" || !measuredMgPerL.trim()}
                fullWidth
                sx={{ textTransform: "none", py: 0.9 }}
              >
                {adding ? "Recording..." : "Record Reading"}
              </Button>
            </Grid>
          </Grid>
        </Paper>
      )}

      {/* CCV Readings History Table */}
      <Paper
        elevation={0}
        sx={{
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`,
          overflow: "hidden",
          backgroundColor: theme.palette.background.paper
        }}
      >
        <Box sx={{ p: 2, borderBottom: `1px solid ${theme.palette.divider}` }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Recorded CCV Readings ({readings.length})
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Append-only verification log. Server calculates recovery percentage and checks against specification limits.
          </Typography>
        </Box>

        {readings.length === 0 ? (
          <Box sx={{ p: 4, textAlign: "center" }}>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              No CCV readings have been recorded yet.
            </Typography>
          </Box>
        ) : (
          <TableContainer sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell>Time</TableCell>
                  <TableCell>Element</TableCell>
                  <TableCell align="right">Measured (mg/L)</TableCell>
                  <TableCell align="right">Recovery (%)</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Entered By</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {readings.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{new Date(r.enteredAt).toLocaleTimeString()}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{r.symbol}</TableCell>
                    <TableCell align="right" sx={{ fontFamily: monospaceFontFamily }}>
                      {typeof r.measuredMgPerL === "number" ? r.measuredMgPerL.toFixed(4) : r.measuredMgPerL}
                    </TableCell>
                    <TableCell align="right" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                      {typeof r.recoveryPercent === "number" ? `${r.recoveryPercent.toFixed(1)}%` : `${r.recoveryPercent}%`}
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={r.passed ? "Pass" : "Fail"}
                        color={r.passed ? "success" : "error"}
                        size="small"
                        sx={{ fontWeight: 600 }}
                      />
                    </TableCell>
                    <TableCell>{r.enteredByUserName ?? "Unknown"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Paper>
    </Stack>
  );
}
