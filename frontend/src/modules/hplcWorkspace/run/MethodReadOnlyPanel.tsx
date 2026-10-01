import { useState, useEffect } from "react";
import {
  Box,
  Paper,
  Typography,
  Grid,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  CircularProgress,
  Alert,
  Divider,
  Stack,
  useTheme
} from "@mui/material";
import BiotechIcon from "@mui/icons-material/Biotech";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import WaterDropIcon from "@mui/icons-material/WaterDrop";
import TuneIcon from "@mui/icons-material/Tune";
import {
  HplcMethodService,
  HplcMethodResponse
} from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import { tableHeadSx } from "../../../theme";

export interface MethodReadOnlyPanelProps {
  methodId?: number;
  method?: HplcMethodResponse | null;
}

export function MethodReadOnlyPanel({ methodId, method: initialMethod }: MethodReadOnlyPanelProps) {
  const theme = useTheme();
  const [method, setMethod] = useState<HplcMethodResponse | null>(initialMethod ?? null);
  const [loading, setLoading] = useState(Boolean(methodId && !initialMethod));
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (initialMethod) {
      setMethod(initialMethod);
      return;
    }
    if (!methodId) return;

    let active = true;
    setLoading(true);
    setError(null);
    HplcMethodService.getById(methodId)
      .then((data) => {
        if (active) setMethod(data);
      })
      .catch((err: unknown) => {
        if (active) {
          const e = err as { response?: { data?: { message?: string } }; message?: string };
          setError(e.response?.data?.message ?? e.message ?? "Could not load method snapshot.");
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [methodId, initialMethod]);

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
        <CircularProgress size={32} />
      </Box>
    );
  }

  if (error) {
    return <Alert severity="error">{error}</Alert>;
  }

  if (!method) {
    return (
      <Typography variant="body2" sx={{ color: "text.secondary", py: 2 }}>
        No method definition available.
      </Typography>
    );
  }

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      {/* Title */}
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <BiotechIcon color="primary" />
        <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
          Method: {method.name} ({method.abbreviation})
        </Typography>
        <Chip
          label={method.elutionMode}
          color={method.elutionMode === "Gradient" ? "secondary" : "primary"}
          size="small"
          variant="outlined"
          sx={{ ml: "auto", fontWeight: 600 }}
        />
      </Box>

      <Divider sx={{ mb: 2.5 }} />

      {/* Grid of parameters */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        {/* Column Parameters */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1.5 }}>
            <ViewColumnIcon fontSize="small" sx={{ color: "text.secondary" }} />
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Chromatography Column
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Designation</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{method.columnDesignation}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Dimensions</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.columnLengthMm} mm × {method.columnInternalDiameterMm} mm, {method.particleSizeUm} µm
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Temperature</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{method.columnTemperatureC} °C</TableCell>
                </TableRow>
                {(method.columnBrand || method.columnPartNumber) && (
                  <TableRow>
                    <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Brand / Part No.</TableCell>
                    <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                      {[method.columnBrand, method.columnPartNumber].filter(Boolean).join(" / ")}
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Grid>

        {/* Operating Conditions */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1.5 }}>
            <TuneIcon fontSize="small" sx={{ color: "text.secondary" }} />
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Instrument Conditions
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Flow Rate</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{method.flowRateMlPerMin} mL/min</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Detector</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{method.detectorType}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Injection Volume</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{method.injectionVolumeUl} µL</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Run Time / Equil.</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.runTimeMin} min {method.equilibrationMin ? `(${method.equilibrationMin} min eq.)` : ""}
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </TableContainer>
        </Grid>
      </Grid>

      {/* Solutions */}
      <Box sx={{ mb: 3 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1.5 }}>
          <WaterDropIcon fontSize="small" sx={{ color: "text.secondary" }} />
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Solutions & Mobile Phases
          </Typography>
        </Box>
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Diluent Solution:
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {method.diluentSolutionName || `Solution #${method.diluentSolutionId}`}
            </Typography>
          </Grid>
          <Grid size={{ xs: 12, sm: 8 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Mobile Phases:
            </Typography>
            <Stack useFlexGap direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: "wrap" }}>
              {method.mobilePhases.map((mp) => (
                <Chip
                  key={mp.id}
                  size="small"
                  label={`Channel ${mp.channel}: ${mp.solutionMasterName}${mp.ratioPercent != null ? ` (${mp.ratioPercent}%)` : ""}`}
                  variant="outlined"
                  sx={{ mb: 0.5 }}
                />
              ))}
            </Stack>
          </Grid>
        </Grid>

        {/* Gradient table if gradient mode */}
        {method.elutionMode === "Gradient" && method.gradientSteps?.length > 0 && (
          <Box sx={{ mt: 2 }}>
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block", mb: 0.5 }}>
              Gradient Program:
            </Typography>
            <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell>Time (min)</TableCell>
                    <TableCell align="right">%A</TableCell>
                    <TableCell align="right">%B</TableCell>
                    <TableCell align="right">%C</TableCell>
                    <TableCell align="right">%D</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {method.gradientSteps.map((s) => (
                    <TableRow key={s.id}>
                      <TableCell sx={{ py: 0.5 }}>{s.timeMin}</TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}>{s.percentA}%</TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}>{s.percentB}%</TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}>{s.percentC}%</TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}>{s.percentD}%</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        )}
      </Box>

      {/* Analytes */}
      <Box>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Method Analytes & System Suitability Criteria
        </Typography>
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>Analyte</TableCell>
                <TableCell>Wavelength</TableCell>
                <TableCell>Standard</TableCell>
                <TableCell align="right">Th. Wt. Std (mg)</TableCell>
                <TableCell align="right">Th. Wt. Test (mg)</TableCell>
                <TableCell align="center">Injections (n)</TableCell>
                <TableCell>SST Criteria</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {method.analytes.map((a) => {
                const criteria = [
                  a.sstMaxRsdPercent != null ? `%RSD ≤ ${a.sstMaxRsdPercent}%` : null,
                  a.sstMinResolution != null ? `Res ≥ ${a.sstMinResolution}` : null,
                  a.sstMaxTailingFactor != null ? `Tailing ≤ ${a.sstMaxTailingFactor}` : null,
                  a.sstMinTheoreticalPlates != null ? `Plates ≥ ${a.sstMinTheoreticalPlates}` : null,
                  a.sstMinRetentionFactor != null ? `k' ≥ ${a.sstMinRetentionFactor}` : null,
                  a.sstMinSignalToNoise != null ? `S/N ≥ ${a.sstMinSignalToNoise}` : null,
                  a.sstMinPeakToValley != null ? `P/V ≥ ${a.sstMinPeakToValley}` : null
                ].filter(Boolean);

                return (
                  <TableRow key={a.id}>
                    <TableCell sx={{ fontWeight: 600, py: 1 }}>{a.name}</TableCell>
                    <TableCell sx={{ py: 1 }}>{a.wavelengthNm} nm</TableCell>
                    <TableCell sx={{ py: 1 }}>{a.standardEntryCode}</TableCell>
                    <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                      {a.theoreticalWeightStdMg}
                    </TableCell>
                    <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                      {a.theoreticalWeightTestMg}
                    </TableCell>
                    <TableCell align="center" sx={{ py: 1 }}>{a.standardInjections}</TableCell>
                    <TableCell sx={{ py: 1 }}>
                      {criteria.length > 0 ? (
                        <Typography variant="body2" sx={{ fontSize: 12 }}>
                          {criteria.join(" · ")}
                        </Typography>
                      ) : (
                        <Typography variant="caption" sx={{ color: "text.secondary" }}>
                          None configured
                        </Typography>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>
    </Paper>
  );
}
