import {
  Box,
  Typography,
  Grid,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Divider,
  useTheme
} from "@mui/material";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import TuneIcon from "@mui/icons-material/Tune";
import ThermostatIcon from "@mui/icons-material/Thermostat";
import ScienceIcon from "@mui/icons-material/Science";
import WaterDropIcon from "@mui/icons-material/WaterDrop";
import { tableHeadSx } from "../../../theme";
import type { HplcMethodSnapshot } from "../types";

const GC_DETECTOR_LABELS: Record<string, string> = {
  Fid: "FID",
  Tcd: "TCD",
  Ecd: "ECD",
  Ms: "MS",
  UV: "UV",
  PDA: "PDA",
  FLD: "FLD",
  RI: "RI",
  ELSD: "ELSD"
};

export interface GcMethodDetailsProps {
  method: HplcMethodSnapshot;
}

export function GcMethodDetails({ method }: GcMethodDetailsProps) {
  const theme = useTheme();
  const isResidualSolvents = method.resultMode === "ResidualSolvents";

  // Length in metres (contract: entered in m, stored as mm x1000)
  const lengthM = method.columnLengthMm
    ? method.columnLengthMm >= 100
      ? method.columnLengthMm / 1000
      : method.columnLengthMm
    : null;

  const splitDisplay =
    method.splitRatio != null && method.splitRatio > 0
      ? `${method.splitRatio}:1`
      : "Splitless";

  const detectorDisplay =
    GC_DETECTOR_LABELS[method.detectorType] ?? method.detectorType;

  return (
    <Box>
      {/* Title & Chips */}
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
          Method: {method.name} ({method.abbreviation})
        </Typography>
        <Chip
          label="GC"
          color="primary"
          size="small"
          variant="filled"
          sx={{ fontWeight: 700 }}
        />
        {method.resultMode && (
          <Chip
            label={isResidualSolvents ? "Residual Solvents" : "Assay"}
            color={isResidualSolvents ? "secondary" : "default"}
            size="small"
            variant="outlined"
            sx={{ fontWeight: 600 }}
          />
        )}
      </Box>

      <Divider sx={{ mb: 2.5 }} />

      {/* Grid: Column & Instrument Conditions */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        {/* Column Parameters */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1.5 }}>
            <ViewColumnIcon fontSize="small" sx={{ color: "text.secondary" }} />
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              GC Column (Capillary)
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Designation</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{method.columnDesignation || "—"}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Dimensions</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {lengthM != null ? `${lengthM} m` : "—"} × {method.columnInternalDiameterMm != null ? `${method.columnInternalDiameterMm} mm` : "—"}
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Film Thickness</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.filmThicknessUm != null ? `${method.filmThicknessUm} µm` : "—"}
                  </TableCell>
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
              Inlet & Detector Conditions
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Carrier Gas</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{method.carrierGas || "—"}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Split Ratio</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{splitDisplay}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Inlet Temp</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.inletTemperatureC != null ? `${method.inletTemperatureC} °C` : "—"}
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Detector Temp</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.detectorTemperatureC != null ? `${method.detectorTemperatureC} °C` : "—"}
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Detector</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{detectorDisplay}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Injection Volume</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.injectionVolumeUl != null ? `${method.injectionVolumeUl} µL` : "—"}
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>Run Time / Equil.</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.runTimeMin ?? "—"} min {method.equilibrationMin ? `(${method.equilibrationMin} min eq.)` : ""}
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </TableContainer>
        </Grid>
      </Grid>

      {/* Oven Temperature Program */}
      {method.ovenSteps && method.ovenSteps.length > 0 && (
        <Box sx={{ mb: 3 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1.5 }}>
            <ThermostatIcon fontSize="small" sx={{ color: "text.secondary" }} />
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Oven Temperature Program
            </Typography>
          </Box>
          <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell>Step</TableCell>
                  <TableCell>Ramp Rate (°C/min)</TableCell>
                  <TableCell align="right">Temperature (°C)</TableCell>
                  <TableCell align="right">Hold Time (min)</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {method.ovenSteps.map((step, idx) => {
                  const stepNumber = step.stepNo ?? idx + 1;
                  const isInitial = idx === 0 || step.rateCPerMin == null;
                  return (
                    <TableRow key={step.id ?? idx}>
                      <TableCell sx={{ py: 0.75, fontWeight: 600 }}>Step {stepNumber}</TableCell>
                      <TableCell sx={{ py: 0.75 }}>
                        {isInitial ? "Initial (—)" : `${step.rateCPerMin} °C/min`}
                      </TableCell>
                      <TableCell align="right" sx={{ py: 0.75, fontWeight: 600 }}>
                        {step.temperatureC} °C
                      </TableCell>
                      <TableCell align="right" sx={{ py: 0.75 }}>
                        {step.holdMin} min
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>
      )}

      {/* Headspace Sampler (if enabled) */}
      {method.headspaceEnabled && (
        <Box sx={{ mb: 3 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1.5 }}>
            <ScienceIcon fontSize="small" sx={{ color: "text.secondary" }} />
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Headspace Sampler Parameters
            </Typography>
          </Box>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Equilibration Temperature:
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {method.headspaceEquilibrationTemperatureC != null ? `${method.headspaceEquilibrationTemperatureC} °C` : "—"}
              </Typography>
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Equilibration Time:
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {method.headspaceEquilibrationMin != null ? `${method.headspaceEquilibrationMin} min` : "—"}
              </Typography>
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Transfer Line Temperature:
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {method.headspaceTransferLineTemperatureC != null ? `${method.headspaceTransferLineTemperatureC} °C` : "—"}
              </Typography>
            </Grid>
          </Grid>
        </Box>
      )}

      {/* Solutions & Sample Prep (Residual Solvents) */}
      {(method.diluentSolutionName || method.diluentSolutionId || (isResidualSolvents && method.sampleSolutionVolumeMl != null)) && (
        <Box sx={{ mb: 3 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1.5 }}>
            <WaterDropIcon fontSize="small" sx={{ color: "text.secondary" }} />
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Solutions & Preparation
            </Typography>
          </Box>
          <Grid container spacing={2}>
            {(method.diluentSolutionName || method.diluentSolutionId) && (
              <Grid size={{ xs: 12, sm: 6 }}>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  Diluent Solution:
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {method.diluentSolutionName || `Solution #${method.diluentSolutionId}`}
                </Typography>
              </Grid>
            )}
            {isResidualSolvents && method.sampleSolutionVolumeMl != null && (
              <Grid size={{ xs: 12, sm: 6 }}>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  Sample Solution Volume:
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {method.sampleSolutionVolumeMl} mL
                </Typography>
              </Grid>
            )}
          </Grid>
        </Box>
      )}

      {/* Analytes Table */}
      <Box>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          {isResidualSolvents ? "Residual Solvents & System Suitability Criteria" : "Method Analytes & System Suitability Criteria"}
        </Typography>
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>{isResidualSolvents ? "Solvent" : "Analyte"}</TableCell>
                <TableCell>Standard</TableCell>
                {isResidualSolvents ? (
                  <TableCell align="right">Std Conc (µg/mL)</TableCell>
                ) : (
                  <>
                    <TableCell align="right">Th. Wt. Std (mg)</TableCell>
                    <TableCell align="right">Th. Wt. Test (mg)</TableCell>
                  </>
                )}
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

                const analyteTitle = a.name;

                return (
                  <TableRow key={a.id}>
                    <TableCell sx={{ fontWeight: 600, py: 1 }}>{analyteTitle}</TableCell>
                    <TableCell sx={{ py: 1 }}>{a.standardEntryCode ?? "—"}</TableCell>
                    {isResidualSolvents ? (
                      <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                        {a.standardConcentrationUgPerMl != null ? `${a.standardConcentrationUgPerMl} µg/mL` : "—"}
                      </TableCell>
                    ) : (
                      <>
                        <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                          {a.theoreticalWeightStdMg ?? "—"}
                        </TableCell>
                        <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                          {a.theoreticalWeightTestMg ?? "—"}
                        </TableCell>
                      </>
                    )}
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
    </Box>
  );
}
