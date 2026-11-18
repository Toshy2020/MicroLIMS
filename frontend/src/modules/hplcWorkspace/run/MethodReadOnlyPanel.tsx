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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { GcMethodDetails } from "./GcMethodDetails";

export interface MethodReadOnlyPanelProps {
  methodId?: number;
  method?: HplcMethodResponse | null;
}

export function MethodReadOnlyPanel({ methodId, method: initialMethod }: MethodReadOnlyPanelProps) {
  const { t } = useTranslation("hplcWorkspace");
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
          setError(e.response?.data?.message ?? e.message ?? t("method.loadError"));
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [methodId, initialMethod, t]);

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
        {t("method.noDefinition")}
      </Typography>
    );
  }

  const isGc = method.technique === "Gc";

  if (isGc) {
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
        <GcMethodDetails method={method} />
      </Paper>
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
          {t("method.title", { name: method.name, abbreviation: method.abbreviation })}
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
              {t("method.chromatographyColumn")}
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.designation")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}><Ltr>{method.columnDesignation}</Ltr></TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.dimensions")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    <Ltr>{`${method.columnLengthMm} mm × ${method.columnInternalDiameterMm} mm, ${method.particleSizeUm} µm`}</Ltr>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.temperature")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}><Ltr>{`${method.columnTemperatureC} °C`}</Ltr></TableCell>
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
              {t("method.instrumentConditions")}
            </Typography>
          </Box>
          <TableContainer>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.flowRate")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}><Ltr>{`${method.flowRateMlPerMin} mL/min`}</Ltr></TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.detector")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}><Ltr>{method.detectorType}</Ltr></TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.injectionVolume")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}><Ltr>{`${method.injectionVolumeUl} µL`}</Ltr></TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ color: "text.secondary", py: 0.5, border: "none" }}>{t("method.runTimeEquil")}</TableCell>
                  <TableCell sx={{ fontWeight: 600, py: 0.5, border: "none" }}>
                    <Ltr>{`${method.runTimeMin} min`}</Ltr> {method.equilibrationMin ? t("method.eqSuffix", { min: method.equilibrationMin }) : ""}
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
            {t("method.solutionsMobilePhases")}
          </Typography>
        </Box>
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("method.diluentSolution")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {method.diluentSolutionName || t("method.solutionFallback", { id: method.diluentSolutionId })}
            </Typography>
          </Grid>
          <Grid size={{ xs: 12, sm: 8 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("method.mobilePhases")}
            </Typography>
            <Stack useFlexGap direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: "wrap" }}>
              {method.mobilePhases.map((mp) => (
                <Chip
                  key={mp.id}
                  size="small"
                  label={
                    mp.ratioPercent != null
                      ? t("method.channelItemRatio", { channel: mp.channel, name: mp.solutionMasterName, ratio: mp.ratioPercent })
                      : t("method.channelItem", { channel: mp.channel, name: mp.solutionMasterName })
                  }
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
              {t("method.gradientProgram")}
            </Typography>
            <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell>{t("method.gradientColumns.time")}</TableCell>
                    <TableCell align="right"><Ltr>%A</Ltr></TableCell>
                    <TableCell align="right"><Ltr>%B</Ltr></TableCell>
                    <TableCell align="right"><Ltr>%C</Ltr></TableCell>
                    <TableCell align="right"><Ltr>%D</Ltr></TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {method.gradientSteps.map((s) => (
                    <TableRow key={s.id}>
                      <TableCell sx={{ py: 0.5 }}><Ltr>{s.timeMin}</Ltr></TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}><Ltr>{s.percentA}%</Ltr></TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}><Ltr>{s.percentB}%</Ltr></TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}><Ltr>{s.percentC}%</Ltr></TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}><Ltr>{s.percentD}%</Ltr></TableCell>
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
          {t("method.analytesTitle")}
        </Typography>
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>{t("method.analytesColumns.analyte")}</TableCell>
                <TableCell>{t("method.analytesColumns.wavelength")}</TableCell>
                <TableCell>{t("method.analytesColumns.standard")}</TableCell>
                <TableCell align="right">{t("method.analytesColumns.thWtStd")}</TableCell>
                <TableCell align="right">{t("method.analytesColumns.thWtTest")}</TableCell>
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

                return (
                  <TableRow key={a.id}>
                    <TableCell sx={{ fontWeight: 600, py: 1 }}><Ltr>{a.name}</Ltr></TableCell>
                    <TableCell sx={{ py: 1 }}><Ltr>{`${a.wavelengthNm} nm`}</Ltr></TableCell>
                    <TableCell sx={{ py: 1 }}><Ltr>{a.standardEntryCode}</Ltr></TableCell>
                    <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                      <Ltr>{a.theoreticalWeightStdMg}</Ltr>
                    </TableCell>
                    <TableCell align="right" sx={{ py: 1, fontWeight: 600, color: "text.primary" }}>
                      <Ltr>{a.theoreticalWeightTestMg}</Ltr>
                    </TableCell>
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
    </Paper>
  );
}
