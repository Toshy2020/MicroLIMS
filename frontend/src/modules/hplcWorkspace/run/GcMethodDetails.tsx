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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("hplcWorkspace");
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
      : t("method.splitless");

  const detectorDisplay =
    GC_DETECTOR_LABELS[method.detectorType] ?? method.detectorType;

  return (
    <Box>
      {/* Title & Chips */}
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
          {t("method.title", { name: method.name, abbreviation: method.abbreviation })}
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
            label={isResidualSolvents ? t("method.residualSolventsChip") : t("method.assayChip")}
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
              {t("method.gcColumn")}
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.designation")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}><Ltr>{method.columnDesignation || "—"}</Ltr></TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.dimensions")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    <Ltr>{lengthM != null ? `${lengthM} m` : "—"} × {method.columnInternalDiameterMm != null ? `${method.columnInternalDiameterMm} mm` : "—"}</Ltr>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.filmThickness")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.filmThicknessUm != null ? <Ltr>{`${method.filmThicknessUm} µm`}</Ltr> : "—"}
                  </TableCell>
                </TableRow>
                {(method.columnBrand || method.columnPartNumber) && (
                  <TableRow>
                    <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.brandPartNo")}</TableCell>
                    <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                      <Ltr>{[method.columnBrand, method.columnPartNumber].filter(Boolean).join(" / ")}</Ltr>
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
              {t("method.inletDetectorConditions")}
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.carrierGas")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>{method.carrierGas || "—"}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.splitRatio")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.splitRatio != null && method.splitRatio > 0 ? <Ltr>{splitDisplay}</Ltr> : splitDisplay}
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.inletTemp")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.inletTemperatureC != null ? <Ltr>{`${method.inletTemperatureC} °C`}</Ltr> : "—"}
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.detectorTemp")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.detectorTemperatureC != null ? <Ltr>{`${method.detectorTemperatureC} °C`}</Ltr> : "—"}
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.detector")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}><Ltr>{detectorDisplay}</Ltr></TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.injectionVolume")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    {method.injectionVolumeUl != null ? <Ltr>{`${method.injectionVolumeUl} µL`}</Ltr> : "—"}
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.runTimeEquil")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    <Ltr>{method.runTimeMin ?? "—"} min</Ltr> {method.equilibrationMin ? t("method.eqSuffix", { min: method.equilibrationMin }) : ""}
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
              {t("method.ovenProgram")}
            </Typography>
          </Box>
          <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell>{t("method.ovenColumns.step")}</TableCell>
                  <TableCell>{t("method.ovenColumns.rampRate")}</TableCell>
                  <TableCell align="right">{t("method.ovenColumns.temperature")}</TableCell>
                  <TableCell align="right">{t("method.ovenColumns.holdTime")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {method.ovenSteps.map((step, idx) => {
                  const stepNumber = step.stepNo ?? idx + 1;
                  const isInitial = idx === 0 || step.rateCPerMin == null;
                  return (
                    <TableRow key={step.id ?? idx}>
                      <TableCell sx={{ py: 0.75, fontWeight: 600 }}>{t("method.stepNumber", { number: stepNumber })}</TableCell>
                      <TableCell sx={{ py: 0.75 }}>
                        {isInitial ? t("method.initialStep") : <Ltr>{`${step.rateCPerMin} °C/min`}</Ltr>}
                      </TableCell>
                      <TableCell align="right" sx={{ py: 0.75, fontWeight: 600 }}>
                        <Ltr>{`${step.temperatureC} °C`}</Ltr>
                      </TableCell>
                      <TableCell align="right" sx={{ py: 0.75 }}>
                        <Ltr>{`${step.holdMin} min`}</Ltr>
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
              {t("method.headspaceTitle")}
            </Typography>
          </Box>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {t("method.equilibrationTemp")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {method.headspaceEquilibrationTemperatureC != null ? <Ltr>{`${method.headspaceEquilibrationTemperatureC} °C`}</Ltr> : "—"}
              </Typography>
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {t("method.equilibrationTime")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {method.headspaceEquilibrationMin != null ? <Ltr>{`${method.headspaceEquilibrationMin} min`}</Ltr> : "—"}
              </Typography>
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {t("method.transferLineTemp")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {method.headspaceTransferLineTemperatureC != null ? <Ltr>{`${method.headspaceTransferLineTemperatureC} °C`}</Ltr> : "—"}
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
              {t("method.solutionsPreparation")}
            </Typography>
          </Box>
          <Grid container spacing={2}>
            {(method.diluentSolutionName || method.diluentSolutionId) && (
              <Grid size={{ xs: 12, sm: 6 }}>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {t("method.diluentSolution")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {method.diluentSolutionName || t("method.solutionFallback", { id: method.diluentSolutionId })}
                </Typography>
              </Grid>
            )}
            {isResidualSolvents && method.sampleSolutionVolumeMl != null && (
              <Grid size={{ xs: 12, sm: 6 }}>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {t("method.sampleSolutionVolume")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  <Ltr>{`${method.sampleSolutionVolumeMl} mL`}</Ltr>
                </Typography>
              </Grid>
            )}
          </Grid>
        </Box>
      )}

      {/* Analytes Table */}
      <Box>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          {isResidualSolvents ? t("method.residualSolventsTitle") : t("method.analytesTitle")}
        </Typography>
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>{isResidualSolvents ? t("method.analytesColumns.solvent") : t("method.analytesColumns.analyte")}</TableCell>
                <TableCell>{t("method.analytesColumns.standard")}</TableCell>
                {isResidualSolvents ? (
                  <TableCell align="right">{t("method.analytesColumns.stdConc")}</TableCell>
                ) : (
                  <>
                    <TableCell align="right">{t("method.analytesColumns.thWtStd")}</TableCell>
                    <TableCell align="right">{t("method.analytesColumns.thWtTest")}</TableCell>
                  </>
                )}
                <TableCell align="center">{t("method.analytesColumns.injections")}</TableCell>
                <TableCell>{t("method.analytesColumns.sstCriteria")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {method.analytes.map((a) => {
                const criteria = [
                  a.sstMaxRsdPercent != null ? t("method.criteria.rsd", { val: a.sstMaxRsdPercent }) : null,
                  a.sstMinResolution != null ? t("method.criteria.res", { val: a.sstMinResolution }) : null,
                  a.sstMaxTailingFactor != null ? t("method.criteria.tailing", { val: a.sstMaxTailingFactor }) : null,
                  a.sstMinTheoreticalPlates != null ? t("method.criteria.plates", { val: a.sstMinTheoreticalPlates }) : null,
                  a.sstMinRetentionFactor != null ? t("method.criteria.kPrime", { val: a.sstMinRetentionFactor }) : null,
                  a.sstMinSignalToNoise != null ? t("method.criteria.sn", { val: a.sstMinSignalToNoise }) : null,
                  a.sstMinPeakToValley != null ? t("method.criteria.pv", { val: a.sstMinPeakToValley }) : null
                ].filter(Boolean);

                const analyteTitle = a.name;

                return (
                  <TableRow key={a.id}>
                    <TableCell sx={{ fontWeight: 600, py: 1 }}><Ltr>{analyteTitle}</Ltr></TableCell>
                    <TableCell sx={{ py: 1 }}><Ltr>{a.standardEntryCode ?? "—"}</Ltr></TableCell>
                    {isResidualSolvents ? (
                      <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                        {a.standardConcentrationUgPerMl != null ? <Ltr>{`${a.standardConcentrationUgPerMl} µg/mL`}</Ltr> : "—"}
                      </TableCell>
                    ) : (
                      <>
                        <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                          <Ltr>{a.theoreticalWeightStdMg ?? "—"}</Ltr>
                        </TableCell>
                        <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                          <Ltr>{a.theoreticalWeightTestMg ?? "—"}</Ltr>
                        </TableCell>
                      </>
                    )}
                    <TableCell align="center" sx={{ py: 1 }}><Ltr>{a.standardInjections}</Ltr></TableCell>
                    <TableCell sx={{ py: 1 }}>
                      {criteria.length > 0 ? (
                        <Typography variant="body2" sx={{ fontSize: 12 }}>
                          <Ltr>{criteria.join(" · ")}</Ltr>
                        </Typography>
                      ) : (
                        <Typography variant="caption" sx={{ color: "text.secondary" }}>
                          {t("method.noneConfigured")}
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
